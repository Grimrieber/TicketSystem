Imports System
Imports System.Collections.Generic
Imports System.Data
Imports System.Data.SqlClient
Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Web

''' <summary>
''' MessagesHelper - data access + render helpers for the in-app direct messaging
''' system. Backs the Messages sidebar view in Default.aspx.
'''
''' Conversations are derived from the Messages(FromUserID, ToUserID) pair at
''' query time — no separate Threads table. The page renders a Slack-style
''' conversation rail on the left + iMessage-style bubble thread on the right.
'''
''' Lives in App_Code/ so JIT-compile picks it up on FTP deploy without a bin/
''' rebuild (matches TicketListHub / NotificationHelper).
''' </summary>
Public Class MessagesHelper

    ''' <summary>
    ''' Cap how many messages we render in a single thread view. Older history
    ''' is reachable later if we add paging; for v1 a 200-message ceiling keeps
    ''' the DOM bounded and the postback payload small.
    ''' </summary>
    Public Const ThreadRenderLimit As Integer = 200

    ''' <summary>
    ''' Body length cap. NVARCHAR(MAX) on the DB side, but reject pathological
    ''' input at the boundary so a single message can't bloat the conversation
    ''' list preview or the postback payload.
    ''' </summary>
    Public Const MaxBodyLength As Integer = 4000

#Region "Conversation list (left rail)"

    ''' <summary>
    ''' Returns one row per peer the user has ever exchanged messages with,
    ''' sorted by most recent activity. Each row carries a preview of the last
    ''' message and the unread-from-peer count for the badge.
    '''
    ''' Columns: PeerUserID, PeerFullName, PeerUsername, LastBody, LastSentOn,
    '''          LastFromMe (Bit), UnreadCount.
    ''' </summary>
    Public Shared Function GetConversations(userId As Guid, companyId As Integer) As DataTable
        ' Per-peer aggregate: pair the current user with the "other end" of each
        ' message (FromUserID when we're the recipient, ToUserID when we're the
        ' sender), then group by that peer and pick the most-recent row.
        Dim sql As String =
            "WITH PeerMsgs AS (" &
            "    SELECT m.MessageID, m.FromUserID, m.ToUserID, m.Body, m.SentOn, m.ReadOn," &
            "           CASE WHEN m.FromUserID = @UserID THEN m.ToUserID ELSE m.FromUserID END AS PeerID," &
            "           CASE WHEN m.FromUserID = @UserID THEN 1 ELSE 0 END AS FromMe" &
            "    FROM dbo.Messages m" &
            "    WHERE (m.FromUserID = @UserID OR m.ToUserID = @UserID)" &
            "      AND m.CompanyID = @CompanyID" &
            ")," &
            "Latest AS (" &
            "    SELECT PeerID, MAX(SentOn) AS LastSentOn FROM PeerMsgs GROUP BY PeerID" &
            ")" &
            "SELECT" &
            "    p.PeerID                                          AS PeerUserID," &
            "    u.Username                                        AS PeerUsername," &
            "    LTRIM(RTRIM(ISNULL(u.FirstName,'') + ' ' + ISNULL(u.LastName,''))) AS PeerFullName," &
            "    pm.Body                                           AS LastBody," &
            "    pm.SentOn                                         AS LastSentOn," &
            "    pm.FromMe                                         AS LastFromMe," &
            "    (SELECT COUNT(*) FROM dbo.Messages um" &
            "     WHERE um.ToUserID = @UserID AND um.FromUserID = p.PeerID AND um.ReadOn IS NULL) AS UnreadCount" &
            " FROM Latest l" &
            " INNER JOIN PeerMsgs p ON p.PeerID = l.PeerID AND p.SentOn = l.LastSentOn" &
            " CROSS APPLY (" &
            "     SELECT TOP 1 pm2.Body, pm2.SentOn, pm2.FromMe" &
            "     FROM PeerMsgs pm2" &
            "     WHERE pm2.PeerID = p.PeerID AND pm2.SentOn = l.LastSentOn" &
            "     ORDER BY pm2.MessageID" &
            " ) pm" &
            " INNER JOIN dbo.Users u ON u.UserID = p.PeerID" &
            " WHERE u.IsActive = 1" &
            " ORDER BY pm.SentOn DESC"

        Return DatabaseHelper.ExecuteDataTable(sql,
            New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId},
            New SqlParameter("@CompanyID", SqlDbType.Int) With {.Value = companyId})
    End Function

    ''' <summary>
    ''' Total unread messages across all conversations (sidebar badge).
    ''' </summary>
    Public Shared Function GetUnreadCount(userId As Guid) As Integer
        Try
            Dim raw As Object = DatabaseHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM dbo.Messages WHERE ToUserID = @UserID AND ReadOn IS NULL",
                New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId})
            If raw Is Nothing OrElse IsDBNull(raw) Then Return 0
            Return Convert.ToInt32(raw)
        Catch
            Return 0
        End Try
    End Function

#End Region

#Region "Compose picker (start a new conversation)"

    ''' <summary>
    ''' Returns Workers + Admins in the same company (excluding the current user
    ''' and Clients) for the "new conversation" picker. Same scope as the
    ''' collaborators that can be assigned to a ticket — no client messaging.
    '''
    ''' Columns: UserID, Username, FullName.
    ''' </summary>
    Public Shared Function GetMessageablePeers(userId As Guid, companyId As Integer) As DataTable
        Dim sql As String =
            "SELECT u.UserID, u.Username," &
            "       LTRIM(RTRIM(ISNULL(u.FirstName,'') + ' ' + ISNULL(u.LastName,''))) AS FullName" &
            " FROM dbo.Users u" &
            " WHERE u.CompanyID = @CompanyID" &
            "   AND u.IsActive = 1" &
            "   AND u.UserID <> @UserID" &
            "   AND u.Role IN ('Worker','Admin')" &
            " ORDER BY FullName, u.Username"
        Return DatabaseHelper.ExecuteDataTable(sql,
            New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId},
            New SqlParameter("@CompanyID", SqlDbType.Int) With {.Value = companyId})
    End Function

#End Region

#Region "Thread (right pane)"

    ''' <summary>
    ''' Returns the thread between two users, oldest→newest, capped at
    ''' ThreadRenderLimit. Columns: MessageID, FromUserID, Body, SentOn, FromMe.
    ''' </summary>
    Public Shared Function GetThread(userId As Guid, peerUserId As Guid, companyId As Integer) As DataTable
        Dim sql As String =
            "SELECT TOP (@TopN) MessageID, FromUserID, ToUserID, Body, SentOn, ReadOn," &
            "       CASE WHEN FromUserID = @UserID THEN 1 ELSE 0 END AS FromMe" &
            " FROM dbo.Messages" &
            " WHERE CompanyID = @CompanyID" &
            "   AND ((FromUserID = @UserID AND ToUserID = @PeerID)" &
            "        OR (FromUserID = @PeerID AND ToUserID = @UserID))" &
            " ORDER BY SentOn DESC"

        Dim dt As DataTable = DatabaseHelper.ExecuteDataTable(sql,
            New SqlParameter("@TopN", SqlDbType.Int) With {.Value = ThreadRenderLimit},
            New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId},
            New SqlParameter("@PeerID", SqlDbType.UniqueIdentifier) With {.Value = peerUserId},
            New SqlParameter("@CompanyID", SqlDbType.Int) With {.Value = companyId})

        ' Server-side TOP+ORDER BY DESC then reverse on .NET side to display
        ' oldest→newest with the latest at the bottom (chat convention).
        Dim ordered As DataTable = dt.Clone()
        For i As Integer = dt.Rows.Count - 1 To 0 Step -1
            ordered.ImportRow(dt.Rows(i))
        Next
        Return ordered
    End Function

    ''' <summary>
    ''' Marks every unread message FROM peerUserId TO userId as read.
    ''' Returns the count actually flipped, so the caller can refresh the badge.
    ''' </summary>
    Public Shared Function MarkThreadRead(userId As Guid, peerUserId As Guid) As Integer
        Try
            Return DatabaseHelper.ExecuteNonQuery(
                "UPDATE dbo.Messages SET ReadOn = GETDATE()" &
                " WHERE ToUserID = @UserID AND FromUserID = @PeerID AND ReadOn IS NULL",
                New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId},
                New SqlParameter("@PeerID", SqlDbType.UniqueIdentifier) With {.Value = peerUserId})
        Catch
            Return 0
        End Try
    End Function

    ''' <summary>
    ''' Looks up a peer's display name + username for the thread header.
    ''' Returns (FullName, Username, Email) — FullName falls back to Username
    ''' if first/last name are blank.
    ''' </summary>
    Public Shared Function GetPeerInfo(peerUserId As Guid, companyId As Integer) As DataRow
        Dim dt As DataTable = DatabaseHelper.ExecuteDataTable(
            "SELECT TOP 1 UserID, Username, Email," &
            "       LTRIM(RTRIM(ISNULL(FirstName,'') + ' ' + ISNULL(LastName,''))) AS FullName" &
            " FROM dbo.Users WHERE UserID = @PeerID AND CompanyID = @CompanyID AND IsActive = 1",
            New SqlParameter("@PeerID", SqlDbType.UniqueIdentifier) With {.Value = peerUserId},
            New SqlParameter("@CompanyID", SqlDbType.Int) With {.Value = companyId})
        If dt.Rows.Count = 0 Then Return Nothing
        Return dt.Rows(0)
    End Function

#End Region

#Region "Send"

    ''' <summary>
    ''' Inserts a new direct message, pushes a SignalR cue to the recipient if
    ''' connected, and queues an email fallback if they're offline. Returns the
    ''' new MessageID, or Guid.Empty on failure.
    '''
    ''' Validates that both users belong to the same company and that the
    ''' recipient is a Worker/Admin (no client messaging in v1). Trims the body
    ''' and rejects empty / over-length input.
    ''' </summary>
    Public Shared Function SendMessage(fromUserId As Guid, toUserId As Guid, companyId As Integer,
                                       body As String, fromDisplayName As String) As Guid
        Try
            If fromUserId = Guid.Empty OrElse toUserId = Guid.Empty Then Return Guid.Empty
            If fromUserId = toUserId Then Return Guid.Empty
            If body Is Nothing Then Return Guid.Empty

            Dim trimmed As String = body.Trim()
            If trimmed.Length = 0 Then Return Guid.Empty
            If trimmed.Length > MaxBodyLength Then trimmed = trimmed.Substring(0, MaxBodyLength)

            ' Recipient must be a same-company collaborator (Worker/Admin).
            Dim peer As DataRow = GetPeerInfo(toUserId, companyId)
            If peer Is Nothing Then Return Guid.Empty
            Dim peerEmail As String = If(IsDBNull(peer("Email")), "", peer("Email").ToString())

            Dim newId As Guid = Guid.NewGuid()
            DatabaseHelper.ExecuteNonQuery(
                "INSERT INTO dbo.Messages (MessageID, FromUserID, ToUserID, CompanyID, Body, SentOn)" &
                " VALUES (@MessageID, @FromUserID, @ToUserID, @CompanyID, @Body, GETDATE())",
                New SqlParameter("@MessageID", SqlDbType.UniqueIdentifier) With {.Value = newId},
                New SqlParameter("@FromUserID", SqlDbType.UniqueIdentifier) With {.Value = fromUserId},
                New SqlParameter("@ToUserID", SqlDbType.UniqueIdentifier) With {.Value = toUserId},
                New SqlParameter("@CompanyID", SqlDbType.Int) With {.Value = companyId},
                New SqlParameter("@Body", SqlDbType.NVarChar) With {.Value = trimmed})

            ' Best-effort SignalR push. Wrapped so a hub failure can't break the save.
            Try
                MessagesHub.NotifyMessageReceived(toUserId, fromUserId)
            Catch
            End Try

            ' Email fallback when the recipient hasn't been seen on the hub for
            ' a while. Reusing the existing notification email pipeline.
            Try
                If Not MessagesHub.IsUserOnline(toUserId) AndAlso peerEmail.Length > 0 Then
                    NotificationHelper.NotifyDirectMessage(peerEmail, fromDisplayName, trimmed, fromUserId)
                End If
            Catch
            End Try

            Return newId
        Catch ex As Exception
            LogError("SendMessage", ex)
            Return Guid.Empty
        End Try
    End Function

#End Region

#Region "Logging"

    ' Private file logger mirroring NotificationHelper's pattern. Public
    ' DatabaseHelper.LogError isn't available, and we don't want a hub or
    ' helper failure to bubble up and break the page postback.
    Private Shared Sub LogError(methodName As String, ex As Exception)
        Try
            Dim logPath As String = HttpContext.Current.Server.MapPath("~/App_Data/Logs/")
            If Not Directory.Exists(logPath) Then Directory.CreateDirectory(logPath)
            Dim logFile As String = Path.Combine(logPath, String.Format("MessagesErrors_{0:yyyyMMdd}.txt", DateTime.Now))
            File.AppendAllText(logFile, String.Format("[{0:yyyy-MM-dd HH:mm:ss}] {1}: {2}{3}{4}{3}{3}",
                                                      DateTime.Now, methodName, ex.Message, vbCrLf, ex.StackTrace))
        Catch
        End Try
    End Sub

#End Region

#Region "Render helpers"

    ' Recognizes ticket-number tokens like "#MORe-0004" or "MORe-0004" inside
    ' a message body. The capture group (without the optional leading #) is the
    ' candidate ticket number we'll resolve against the Tickets table.
    Private Shared ReadOnly TicketRefRegex As New Regex(
        "(?<![A-Za-z0-9])#?([A-Za-z][A-Za-z0-9]{1,9}-\d{2,6})(?![A-Za-z0-9])",
        RegexOptions.Compiled)

    ''' <summary>
    ''' Renders a stored message body to safe HTML for the bubble:
    '''   1. HTML-encode everything (no raw user HTML escapes the bubble).
    '''   2. Convert "#TICKET-1234" / "TICKET-1234" tokens to anchor tags
    '''      pointing at the resolved ticket so collaborators can jump
    '''      straight to it from the chat.
    '''   3. Convert newlines to &lt;br&gt; for multi-line messages.
    ''' Unrecognized ticket tokens stay as plain text.
    ''' </summary>
    ''' <param name="resolvedTickets">
    ''' Map of ticket-number-token → TicketID for tokens that actually exist in
    ''' this company. Build with <see cref="ResolveTicketNumbers"/>. Tokens not
    ''' present in the map render as plain text.
    ''' </param>
    ''' <param name="ticketUrlBase">
    ''' Application-relative URL for the ticket page, e.g. "~/Default.aspx"
    ''' resolved via Page.ResolveUrl. The TicketID is appended as a query string.
    ''' </param>
    Public Shared Function RenderBodyHtml(body As String, ticketUrlBase As String,
                                          resolvedTickets As Dictionary(Of String, Guid)) As String
        If String.IsNullOrEmpty(body) Then Return ""

        Dim encoded As String = HttpUtility.HtmlEncode(body)

        Dim withLinks As String = TicketRefRegex.Replace(encoded, Function(m)
                                                                      Dim token As String = m.Groups(1).Value
                                                                      Dim tid As Guid = Guid.Empty
                                                                      If resolvedTickets Is Nothing OrElse
                                                                         Not resolvedTickets.TryGetValue(token, tid) Then
                                                                          Return m.Value
                                                                      End If
                                                                      Return "<a class=""msg-ticket-link"" href=""" &
                                                                             ticketUrlBase & "?ticketId=" & tid.ToString() & """>" &
                                                                             m.Value & "</a>"
                                                                  End Function)

        Return withLinks.Replace(vbCrLf, "<br />").Replace(vbLf, "<br />")
    End Function

    ''' <summary>
    ''' Given a batch of message bodies, returns a map of ticket-number-token →
    ''' TicketID for the tokens that actually exist in this company's Tickets
    ''' table. Lets the renderer link only real references and leave typos /
    ''' coincidental matches alone.
    ''' </summary>
    Public Shared Function ResolveTicketNumbers(bodies As IEnumerable(Of String), companyId As Integer) As Dictionary(Of String, Guid)
        Dim found As New Dictionary(Of String, Guid)(StringComparer.OrdinalIgnoreCase)
        Dim candidates As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        If bodies Is Nothing Then Return found
        For Each b As String In bodies
            If String.IsNullOrEmpty(b) Then Continue For
            For Each m As Match In TicketRefRegex.Matches(b)
                candidates.Add(m.Groups(1).Value)
            Next
        Next
        If candidates.Count = 0 Then Return found

        ' Build a parameterized IN-list — small N (per render = 200 messages
        ' max), so plain inline params are fine and keep us off dynamic SQL.
        Dim names As New List(Of String)
        Dim params As New List(Of SqlParameter)
        params.Add(New SqlParameter("@CompanyID", SqlDbType.Int) With {.Value = companyId})
        Dim i As Integer = 0
        For Each c As String In candidates
            Dim p As String = "@T" & i.ToString()
            names.Add(p)
            params.Add(New SqlParameter(p, SqlDbType.NVarChar, 50) With {.Value = c})
            i += 1
        Next

        Dim sql As String =
            "SELECT t.TicketID, t.TicketNumber FROM dbo.Tickets t" &
            " INNER JOIN dbo.Projects p ON t.ProjectID = p.ProjectID" &
            " INNER JOIN dbo.Clients  c ON p.ClientID  = c.ClientID" &
            " WHERE c.CompanyID = @CompanyID" &
            "   AND t.TicketNumber IN (" & String.Join(",", names) & ")"

        Try
            Dim dt As DataTable = DatabaseHelper.ExecuteDataTable(sql, params.ToArray())
            For Each row As DataRow In dt.Rows
                If IsDBNull(row("TicketNumber")) OrElse IsDBNull(row("TicketID")) Then Continue For
                Dim tn As String = row("TicketNumber").ToString()
                Dim tid As Guid
                If Guid.TryParse(row("TicketID").ToString(), tid) AndAlso Not found.ContainsKey(tn) Then
                    found(tn) = tid
                End If
            Next
        Catch
            ' On error, return whatever we found so far (linking will degrade
            ' gracefully — unresolved tokens render as plain text).
        End Try
        Return found
    End Function

    ''' <summary>
    ''' Short preview of a message body for the conversation rail. Strips
    ''' newlines, collapses whitespace, and truncates with an ellipsis.
    ''' </summary>
    Public Shared Function PreviewBody(body As String, maxChars As Integer) As String
        If String.IsNullOrEmpty(body) Then Return ""
        Dim flat As String = Regex.Replace(body, "\s+", " ").Trim()
        If flat.Length <= maxChars Then Return flat
        Return flat.Substring(0, maxChars - 1).TrimEnd() & "…"
    End Function

    ''' <summary>
    ''' Friendly relative timestamp for the conversation rail
    ''' (e.g. "2:14 PM", "Yesterday", "Mar 12").
    ''' </summary>
    Public Shared Function FormatRelativeTime(t As DateTime) As String
        Dim now As DateTime = DateTime.Now
        If t.Date = now.Date Then Return t.ToString("h:mm tt")
        If t.Date = now.Date.AddDays(-1) Then Return "Yesterday"
        If (now.Date - t.Date).TotalDays < 7 Then Return t.ToString("ddd")
        If t.Year = now.Year Then Return t.ToString("MMM d")
        Return t.ToString("MMM d, yyyy")
    End Function

#End Region

End Class
