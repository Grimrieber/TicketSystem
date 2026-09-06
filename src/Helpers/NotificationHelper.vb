Imports System
Imports System.Configuration
Imports System.Data
Imports System.Data.SqlClient
Imports System.IO
Imports System.Net
Imports System.Net.Mail
Imports System.Web
Imports Microsoft.VisualBasic

''' <summary>
''' NotificationHelper - Handles email notifications and alerts
''' Sends notifications for ticket updates, mentions, and assignments
''' </summary>
Public Class NotificationHelper

#Region "Configuration"

    Private Shared ReadOnly EnableEmailNotifications As Boolean = Convert.ToBoolean(ConfigurationManager.AppSettings("EnableEmailNotifications"))
    Private Shared ReadOnly SmtpServer As String = ConfigurationManager.AppSettings("SmtpServer")
    Private Shared ReadOnly SmtpPort As Integer = Convert.ToInt32(ConfigurationManager.AppSettings("SmtpPort"))
    Private Shared ReadOnly SmtpUsername As String = ConfigurationManager.AppSettings("SmtpUsername")
    Private Shared ReadOnly SmtpPassword As String = ConfigurationManager.AppSettings("SmtpPassword")
    Private Shared ReadOnly SmtpEnableSSL As Boolean = Convert.ToBoolean(ConfigurationManager.AppSettings("SmtpEnableSSL"))
    Private Shared ReadOnly EmailFromAddress As String = ConfigurationManager.AppSettings("EmailFromAddress")
    Private Shared ReadOnly EmailFromName As String = ConfigurationManager.AppSettings("EmailFromName")

#End Region

#Region "Email Sending"

    ''' <summary>
    ''' Sends an email notification
    ''' </summary>
    Private Shared Function SendEmail(toEmail As String, subject As String, body As String, isHtml As Boolean) As Boolean
        If Not EnableEmailNotifications Then
            Return False
        End If

        Try
            Using message As New MailMessage()
                message.From = New MailAddress(EmailFromAddress, EmailFromName)
                message.To.Add(toEmail)
                message.Subject = subject
                message.Body = body
                message.IsBodyHtml = isHtml

                Using smtp As New SmtpClient(SmtpServer, SmtpPort)
                    smtp.Credentials = New NetworkCredential(SmtpUsername, SmtpPassword)
                    smtp.EnableSsl = SmtpEnableSSL

                    smtp.Send(message)
                End Using
            End Using

            LogNotification("Email sent successfully to " & toEmail, subject)
            Return True

        Catch ex As Exception
            LogError("SendEmail", ex)
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Sends an email to multiple recipients
    ''' </summary>
    Private Shared Function SendEmailToMultiple(toEmails As List(Of String), subject As String, body As String, isHtml As Boolean) As Boolean
        If Not EnableEmailNotifications OrElse toEmails Is Nothing OrElse toEmails.Count = 0 Then
            Return False
        End If

        Try
            Using message As New MailMessage()
                message.From = New MailAddress(EmailFromAddress, EmailFromName)

                For Each email As String In toEmails
                    message.To.Add(email)
                Next

                message.Subject = subject
                message.Body = body
                message.IsBodyHtml = isHtml

                Using smtp As New SmtpClient(SmtpServer, SmtpPort)
                    smtp.Credentials = New NetworkCredential(SmtpUsername, SmtpPassword)
                    smtp.EnableSsl = SmtpEnableSSL

                    smtp.Send(message)
                End Using
            End Using

            LogNotification("Email sent successfully to " & String.Join(", ", toEmails), subject)
            Return True

        Catch ex As Exception
            LogError("SendEmailToMultiple", ex)
            Return False
        End Try
    End Function

#End Region

#Region "Ticket Notifications"

    ''' <summary>
    ''' Sends notification when a ticket is assigned
    ''' </summary>
    Public Shared Sub NotifyTicketAssigned(ticketId As Guid, assignedToUserId As Guid, assignedByUsername As String)
        Try
            ' Get assigned user details
            Dim userRow As DataRow = DatabaseHelper.GetUserById(assignedToUserId)
            If userRow Is Nothing Then Return

            Dim userEmail As String = userRow("Email").ToString()
            Dim firstName As String = If(userRow.Table.Columns.Contains("FirstName") AndAlso Not IsDBNull(userRow("FirstName")), userRow("FirstName").ToString(), "")
            Dim lastName As String = If(userRow.Table.Columns.Contains("LastName") AndAlso Not IsDBNull(userRow("LastName")), userRow("LastName").ToString(), "")
            Dim userName As String = If((firstName & " " & lastName).Trim() <> "", (firstName & " " & lastName).Trim(), userRow("Username").ToString())

            ' Get ticket details
            Dim query As String = "SELECT t.*, p.Name AS ProjectName, c.ClientName FROM Tickets t " &
                                 "INNER JOIN Projects p ON t.ProjectID = p.ProjectID " &
                                 "INNER JOIN Clients c ON p.ClientID = c.ClientID " &
                                 "WHERE t.TicketID = @TicketID"
            Dim dt As DataTable = DatabaseHelper.ExecuteDataTable(query, New SqlParameter("@TicketID", SqlDbType.UniqueIdentifier) With {.Value = ticketId})

            If dt.Rows.Count > 0 Then
                Dim ticketRow As DataRow = dt.Rows(0)
                Dim subject As String = ticketRow("Subject").ToString()
                Dim projectName As String = ticketRow("ProjectName").ToString()
                Dim clientName As String = ticketRow("ClientName").ToString()
                Dim ticketNumber As String = GetTicketNumber(ticketRow, ticketId)

                ' Build email
                Dim emailSubject As String = String.Format("Ticket {0} Assigned to You: {1}", ticketNumber, subject)
                Dim emailBody As String = BuildTicketAssignedEmailBody(ticketId, ticketNumber, subject, projectName, clientName, userName, assignedByUsername)

                SendEmail(userEmail, emailSubject, emailBody, True)
            End If

        Catch ex As Exception
            LogError("NotifyTicketAssigned", ex)
        End Try
    End Sub

    ''' <summary>
    ''' Sends notification when a comment is added with mention
    ''' </summary>
    Public Shared Sub NotifyUserMentioned(ticketId As Guid, mentionedUsername As String, commentText As String, mentionedByUsername As String)
        Try
            ' Get mentioned user details
            Dim userRow As DataRow = DatabaseHelper.GetUserByEmail(mentionedUsername)
            If userRow Is Nothing Then Return

            Dim userEmail As String = userRow("Email").ToString()

            ' Get ticket details
            Dim query As String = "SELECT Subject, TicketNumber FROM Tickets WHERE TicketID = @TicketID"
            Dim dt As DataTable = DatabaseHelper.ExecuteDataTable(query, New SqlParameter("@TicketID", SqlDbType.UniqueIdentifier) With {.Value = ticketId})

            If dt.Rows.Count > 0 Then
                Dim subject As String = dt.Rows(0)("Subject").ToString()
                Dim ticketNumber As String = GetTicketNumber(dt.Rows(0), ticketId)

                ' Build email
                Dim emailSubject As String = String.Format("You were mentioned in Ticket {0}: {1}", ticketNumber, subject)
                Dim emailBody As String = BuildMentionEmailBody(ticketId, ticketNumber, subject, commentText, mentionedUsername, mentionedByUsername)

                SendEmail(userEmail, emailSubject, emailBody, True)
            End If

        Catch ex As Exception
            LogError("NotifyUserMentioned", ex)
        End Try
    End Sub

    ''' <summary>
    ''' Sends notification when added as collaborator
    ''' </summary>
    Public Shared Sub NotifyCollaboratorAdded(ticketId As Guid, collaboratorUserId As Guid, addedByUsername As String)
        Try
            ' Get collaborator user details
            Dim userRow As DataRow = DatabaseHelper.GetUserById(collaboratorUserId)
            If userRow Is Nothing Then Return

            Dim userEmail As String = userRow("Email").ToString()
            Dim collabFirstName As String = If(userRow.Table.Columns.Contains("FirstName") AndAlso Not IsDBNull(userRow("FirstName")), userRow("FirstName").ToString(), "")
            Dim collabLastName As String = If(userRow.Table.Columns.Contains("LastName") AndAlso Not IsDBNull(userRow("LastName")), userRow("LastName").ToString(), "")
            Dim userName As String = If((collabFirstName & " " & collabLastName).Trim() <> "", (collabFirstName & " " & collabLastName).Trim(), userRow("Username").ToString())

            ' Get ticket details
            Dim query As String = "SELECT Subject, TicketNumber FROM Tickets WHERE TicketID = @TicketID"
            Dim dt As DataTable = DatabaseHelper.ExecuteDataTable(query, New SqlParameter("@TicketID", SqlDbType.UniqueIdentifier) With {.Value = ticketId})

            If dt.Rows.Count > 0 Then
                Dim subject As String = dt.Rows(0)("Subject").ToString()
                Dim ticketNumber As String = GetTicketNumber(dt.Rows(0), ticketId)

                ' Build email
                Dim emailSubject As String = String.Format("You've been added as a collaborator on Ticket {0}", ticketNumber)
                Dim emailBody As String = BuildCollaboratorAddedEmailBody(ticketId, ticketNumber, subject, userName, addedByUsername)

                SendEmail(userEmail, emailSubject, emailBody, True)
            End If

        Catch ex As Exception
            LogError("NotifyCollaboratorAdded", ex)
        End Try
    End Sub

    ''' <summary>
    ''' Sends notification when ticket status changes
    ''' </summary>
    Public Shared Sub NotifyTicketStatusChanged(ticketId As Guid, oldStatus As String, newStatus As String, changedByUsername As String)
        Try
            ' Get ticket and user details
            Dim query As String = "SELECT t.Subject, t.TicketNumber, t.AssignedTo, u.Email, u.Username FROM Tickets t " &
                                 "LEFT JOIN Users u ON t.AssignedTo = u.UserID " &
                                 "WHERE t.TicketID = @TicketID"
            Dim dt As DataTable = DatabaseHelper.ExecuteDataTable(query, New SqlParameter("@TicketID", SqlDbType.UniqueIdentifier) With {.Value = ticketId})

            If dt.Rows.Count > 0 AndAlso Not IsDBNull(dt.Rows(0)("Email")) Then
                Dim subject As String = dt.Rows(0)("Subject").ToString()
                Dim assignedToEmail As String = dt.Rows(0)("Email").ToString()
                Dim ticketNumber As String = GetTicketNumber(dt.Rows(0), ticketId)

                ' Build email
                Dim emailSubject As String = String.Format("Ticket {0} Status Changed: {1}", ticketNumber, subject)
                Dim emailBody As String = BuildStatusChangeEmailBody(ticketId, ticketNumber, subject, oldStatus, newStatus, changedByUsername)

                SendEmail(assignedToEmail, emailSubject, emailBody, True)
            End If

        Catch ex As Exception
            LogError("NotifyTicketStatusChanged", ex)
        End Try
    End Sub

    ''' <summary>
    ''' Sends an email-fallback notification for an in-app direct message when
    ''' the recipient has no active SignalR connection. Called from
    ''' MessagesHelper.SendMessage; safe to invoke from a non-page context.
    ''' </summary>
    ''' <param name="toEmail">Recipient's email (already validated non-empty by caller).</param>
    ''' <param name="senderDisplayName">Sender's display name for the subject + greeting.</param>
    ''' <param name="bodyPreview">The message body — already trimmed and length-capped.</param>
    ''' <param name="senderUserId">Sender's UserID, used for the "open conversation" deep link.</param>
    Public Shared Sub NotifyDirectMessage(toEmail As String, senderDisplayName As String,
                                          bodyPreview As String, senderUserId As Guid)
        Try
            If String.IsNullOrEmpty(toEmail) Then Return
            Dim emailSubject As String = String.Format("New message from {0}", senderDisplayName)
            Dim emailBody As String = BuildDirectMessageEmailBody(senderDisplayName, bodyPreview, senderUserId)
            SendEmail(toEmail, emailSubject, emailBody, True)
        Catch ex As Exception
            LogError("NotifyDirectMessage", ex)
        End Try
    End Sub

#End Region

#Region "Email Body Builders"

    ''' <summary>
    ''' Returns the absolute URL to the app root, derived from the current request —
    ''' e.g. https://example.com/apps/TicketSystem — so email
    ''' links resolve under the app's virtual directory, not the server root.
    ''' </summary>
    Private Shared Function GetAppBaseUrl() As String
        Dim req As HttpRequest = HttpContext.Current.Request
        Dim appPath As String = req.ApplicationPath
        If appPath Is Nothing Then appPath = "/"
        Return req.Url.GetLeftPart(UriPartial.Authority) & appPath.TrimEnd("/"c)
    End Function

    ''' <summary>
    ''' Returns the friendly ticket number (e.g. "MORe-0004") from the row, or
    ''' falls back to "#{guid}" if TicketNumber is missing or null.
    ''' </summary>
    Private Shared Function GetTicketNumber(row As DataRow, ticketId As Guid) As String
        If row.Table.Columns.Contains("TicketNumber") AndAlso Not IsDBNull(row("TicketNumber")) Then
            Dim tn As String = row("TicketNumber").ToString()
            If tn <> "" Then Return tn
        End If
        Return "#" & ticketId.ToString()
    End Function

    Private Shared Function BuildTicketAssignedEmailBody(ticketId As Guid, ticketNumber As String, subject As String, projectName As String,
                                                         clientName As String, assignedToName As String, assignedByName As String) As String
        Dim baseUrl As String = GetAppBaseUrl()

        Return String.Format( _
            "<html>" & _
            "<body style='font-family: Arial, sans-serif;'>" & _
            "<div style='max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f5f5f5;'>" & _
            "<div style='background-color: white; padding: 30px; border-radius: 5px;'>" & _
            "<h2 style='color: #333;'>Ticket Assigned</h2>" & _
            "<p>Hi {0},</p>" & _
            "<p>A new ticket has been assigned to you by {1}.</p>" & _
            "<div style='background-color: #f9f9f9; padding: 15px; border-left: 4px solid #007bff; margin: 20px 0;'>" & _
            "<p style='margin: 5px 0;'><strong>Ticket #:</strong> {2}</p>" & _
            "<p style='margin: 5px 0;'><strong>Subject:</strong> {3}</p>" & _
            "<p style='margin: 5px 0;'><strong>Project:</strong> {4}</p>" & _
            "<p style='margin: 5px 0;'><strong>Client:</strong> {5}</p>" & _
            "</div>" & _
            "<p><a href='{6}/Default.aspx?ticketId={7}' style='display: inline-block; padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 3px;'>View Ticket</a></p>" & _
            "<p style='color: #666; font-size: 12px; margin-top: 30px;'>This is an automated notification from the Ticket Management System.</p>" & _
            "</div></div></body></html>", _
            assignedToName, assignedByName, ticketNumber, subject, projectName, clientName, baseUrl, ticketId)
    End Function

    Private Shared Function BuildMentionEmailBody(ticketId As Guid, ticketNumber As String, subject As String, commentText As String,
                                                  mentionedUsername As String, mentionedByUsername As String) As String
        Dim baseUrl As String = GetAppBaseUrl()

        Return String.Format( _
            "<html>" & _
            "<body style='font-family: Arial, sans-serif;'>" & _
            "<div style='max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f5f5f5;'>" & _
            "<div style='background-color: white; padding: 30px; border-radius: 5px;'>" & _
            "<h2 style='color: #333;'>You Were Mentioned</h2>" & _
            "<p>Hi {0},</p>" & _
            "<p>{1} mentioned you in a comment on Ticket {2}.</p>" & _
            "<div style='background-color: #f9f9f9; padding: 15px; border-left: 4px solid #28a745; margin: 20px 0;'>" & _
            "<p style='margin: 5px 0;'><strong>Ticket:</strong> {3}</p>" & _
            "<p style='margin: 10px 0 5px 0;'><strong>Comment:</strong></p>" & _
            "<p style='margin: 5px 0;'>{4}</p>" & _
            "</div>" & _
            "<p><a href='{5}/Default.aspx?ticketId={6}' style='display: inline-block; padding: 10px 20px; background-color: #28a745; color: white; text-decoration: none; border-radius: 3px;'>View Ticket</a></p>" & _
            "<p style='color: #666; font-size: 12px; margin-top: 30px;'>This is an automated notification from the Ticket Management System.</p>" & _
            "</div></div></body></html>", _
            mentionedUsername, mentionedByUsername, ticketNumber, subject, HttpUtility.HtmlEncode(commentText), baseUrl, ticketId)
    End Function

    Private Shared Function BuildCollaboratorAddedEmailBody(ticketId As Guid, ticketNumber As String, subject As String,
                                                           collaboratorName As String, addedByName As String) As String
        Dim baseUrl As String = GetAppBaseUrl()

        Return String.Format( _
            "<html>" & _
            "<body style='font-family: Arial, sans-serif;'>" & _
            "<div style='max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f5f5f5;'>" & _
            "<div style='background-color: white; padding: 30px; border-radius: 5px;'>" & _
            "<h2 style='color: #333;'>Added as Collaborator</h2>" & _
            "<p>Hi {0},</p>" & _
            "<p>{1} has added you as a collaborator on Ticket {2}.</p>" & _
            "<div style='background-color: #f9f9f9; padding: 15px; border-left: 4px solid #17a2b8; margin: 20px 0;'>" & _
            "<p style='margin: 5px 0;'><strong>Ticket #:</strong> {2}</p>" & _
            "<p style='margin: 5px 0;'><strong>Subject:</strong> {3}</p>" & _
            "</div>" & _
            "<p><a href='{4}/Default.aspx?ticketId={5}' style='display: inline-block; padding: 10px 20px; background-color: #17a2b8; color: white; text-decoration: none; border-radius: 3px;'>View Ticket</a></p>" & _
            "<p style='color: #666; font-size: 12px; margin-top: 30px;'>This is an automated notification from the Ticket Management System.</p>" & _
            "</div></div></body></html>", _
            collaboratorName, addedByName, ticketNumber, subject, baseUrl, ticketId)
    End Function

    Private Shared Function BuildStatusChangeEmailBody(ticketId As Guid, ticketNumber As String, subject As String,
                                                       oldStatus As String, newStatus As String, changedByName As String) As String
        Dim baseUrl As String = GetAppBaseUrl()

        Return String.Format( _
            "<html>" & _
            "<body style='font-family: Arial, sans-serif;'>" & _
            "<div style='max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f5f5f5;'>" & _
            "<div style='background-color: white; padding: 30px; border-radius: 5px;'>" & _
            "<h2 style='color: #333;'>Ticket Status Changed</h2>" & _
            "<p>{0} has updated the status of Ticket {1}.</p>" & _
            "<div style='background-color: #f9f9f9; padding: 15px; border-left: 4px solid #ffc107; margin: 20px 0;'>" & _
            "<p style='margin: 5px 0;'><strong>Ticket:</strong> {2}</p>" & _
            "<p style='margin: 5px 0;'><strong>Old Status:</strong> {3}</p>" & _
            "<p style='margin: 5px 0;'><strong>New Status:</strong> <span style='color: #28a745; font-weight: bold;'>{4}</span></p>" & _
            "</div>" & _
            "<p><a href='{5}/Default.aspx?ticketId={6}' style='display: inline-block; padding: 10px 20px; background-color: #ffc107; color: #333; text-decoration: none; border-radius: 3px;'>View Ticket</a></p>" & _
            "<p style='color: #666; font-size: 12px; margin-top: 30px;'>This is an automated notification from the Ticket Management System.</p>" & _
            "</div></div></body></html>", _
            changedByName, ticketNumber, subject, oldStatus, newStatus, baseUrl, ticketId)
    End Function

    Private Shared Function BuildDirectMessageEmailBody(senderName As String, bodyText As String, senderUserId As Guid) As String
        Dim baseUrl As String = GetAppBaseUrl()
        Dim safeBody As String = HttpUtility.HtmlEncode(bodyText).Replace(vbCrLf, "<br />").Replace(vbLf, "<br />")

        Return String.Format( _
            "<html>" & _
            "<body style='font-family: Arial, sans-serif;'>" & _
            "<div style='max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f5f5f5;'>" & _
            "<div style='background-color: white; padding: 30px; border-radius: 5px;'>" & _
            "<h2 style='color: #333; margin-top: 0;'>New Direct Message</h2>" & _
            "<p>{0} sent you a message in the Ticket Management System.</p>" & _
            "<div style='background-color: #f9f9f9; padding: 15px; border-left: 4px solid #6366f1; margin: 20px 0; color: #1f2937;'>" & _
            "{1}" & _
            "</div>" & _
            "<p><a href='{2}/Default.aspx?openMessages=1&peer={3}' style='display: inline-block; padding: 10px 20px; background-color: #6366f1; color: white; text-decoration: none; border-radius: 3px;'>Open Conversation</a></p>" & _
            "<p style='color: #666; font-size: 12px; margin-top: 30px;'>You're getting this because you weren't online when the message was sent. Replies happen in-app — open the conversation to respond.</p>" & _
            "</div></div></body></html>", _
            HttpUtility.HtmlEncode(senderName), safeBody, baseUrl, senderUserId)
    End Function

#End Region

#Region "Logging"

    Private Shared Sub LogNotification(message As String, subject As String)
        Try
            Dim logPath As String = HttpContext.Current.Server.MapPath("~/App_Data/Logs/")
            If Not System.IO.Directory.Exists(logPath) Then
                System.IO.Directory.CreateDirectory(logPath)
            End If

            Dim logFile As String = Path.Combine(logPath, String.Format("Notifications_{0:yyyyMMdd}.txt", DateTime.Now))
            Dim logMessage As String = String.Format("[{0:yyyy-MM-dd HH:mm:ss}] {1} | Subject: {2}" & vbCrLf,
                                                    DateTime.Now, message, subject)

            System.IO.File.AppendAllText(logFile, logMessage)
        Catch
            ' Ignore logging errors
        End Try
    End Sub

    Private Shared Sub LogError(methodName As String, ex As Exception)
        Try
            Dim logPath As String = HttpContext.Current.Server.MapPath("~/App_Data/Logs/")
            If Not System.IO.Directory.Exists(logPath) Then
                System.IO.Directory.CreateDirectory(logPath)
            End If

            Dim logFile As String = Path.Combine(logPath, String.Format("NotificationErrors_{0:yyyyMMdd}.txt", DateTime.Now))
            Dim logMessage As String = String.Format("[{0:yyyy-MM-dd HH:mm:ss}] Method: {1}" & vbCrLf & "Error: {2}" & vbCrLf & "Stack: {3}" & vbCrLf & vbCrLf,
                                                    DateTime.Now, methodName, ex.Message, ex.StackTrace)

            System.IO.File.AppendAllText(logFile, logMessage)
        Catch
            ' Ignore logging errors
        End Try
    End Sub

#End Region

End Class
