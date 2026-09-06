Imports System
Imports System.Configuration
Imports System.Data
Imports System.Data.SqlClient
Imports System.Diagnostics
Imports System.IO
Imports Microsoft.VisualBasic

''' <summary>
''' DatabaseHelper - Provides secure database access with parameterized queries
''' Handles all SQL Server database operations for the Ticket System
''' </summary>
Public Class DatabaseHelper
    Private Shared ReadOnly ConnectionString As String = ConfigurationManager.ConnectionStrings("TicketSystemDB").ConnectionString

#Region "Connection Management"

    ''' <summary>
    ''' Gets a new SQL connection to the database
    ''' </summary>
    Public Shared Function GetConnection() As SqlConnection
        Return New SqlConnection(ConnectionString)
    End Function

    ''' <summary>
    ''' Tests the database connection
    ''' </summary>
    Public Shared Function TestConnection() As Boolean
        Try
            Using conn As SqlConnection = GetConnection()
                conn.Open()
                Return conn.State = ConnectionState.Open
            End Using
        Catch ex As Exception
            ' Log error
            LogError("TestConnection", ex)
            Return False
        End Try
    End Function

#End Region

#Region "Execute Methods"

    ''' <summary>
    ''' Executes a non-query command (INSERT, UPDATE, DELETE)
    ''' </summary>
    Public Shared Function ExecuteNonQuery(query As String, ParamArray parameters() As SqlParameter) As Integer
        Dim sw = Stopwatch.StartNew()
        Try
            Using conn As SqlConnection = GetConnection()
                Using cmd As New SqlCommand(query, conn)
                    cmd.CommandType = CommandType.Text
                    If parameters IsNot Nothing Then
                        cmd.Parameters.AddRange(parameters)
                    End If

                    conn.Open()
                    Return cmd.ExecuteNonQuery()
                End Using
            End Using
        Catch ex As Exception
            LogError("ExecuteNonQuery", ex)
            Throw New ApplicationException("Database error occurred while executing command.", ex)
        Finally
            LogPerf("ExecuteNonQuery", sw.ElapsedMilliseconds, query)
        End Try
    End Function

    ''' <summary>
    ''' Executes a scalar query (returns single value)
    ''' </summary>
    Public Shared Function ExecuteScalar(query As String, ParamArray parameters() As SqlParameter) As Object
        Dim sw = Stopwatch.StartNew()
        Try
            Using conn As SqlConnection = GetConnection()
                Using cmd As New SqlCommand(query, conn)
                    cmd.CommandType = CommandType.Text
                    If parameters IsNot Nothing Then
                        cmd.Parameters.AddRange(parameters)
                    End If

                    conn.Open()
                    Return cmd.ExecuteScalar()
                End Using
            End Using
        Catch ex As Exception
            LogError("ExecuteScalar", ex)
            Throw New ApplicationException("Database error occurred while executing scalar query.", ex)
        Finally
            LogPerf("ExecuteScalar", sw.ElapsedMilliseconds, query)
        End Try
    End Function

    ''' <summary>
    ''' Executes a query and returns a DataTable
    ''' </summary>
    Public Shared Function ExecuteDataTable(query As String, ParamArray parameters() As SqlParameter) As DataTable
        Dim sw = Stopwatch.StartNew()
        Dim dt As New DataTable()
        Try
            Using conn As SqlConnection = GetConnection()
                Using cmd As New SqlCommand(query, conn)
                    cmd.CommandType = CommandType.Text
                    If parameters IsNot Nothing Then
                        cmd.Parameters.AddRange(parameters)
                    End If

                    Using adapter As New SqlDataAdapter(cmd)
                        conn.Open()
                        adapter.Fill(dt)
                    End Using
                End Using
            End Using
            Return dt
        Catch ex As Exception
            LogError("ExecuteDataTable", ex)
            Throw New ApplicationException("Database error occurred while fetching data.", ex)
        Finally
            LogPerf("ExecuteDataTable", sw.ElapsedMilliseconds, query, dt.Rows.Count)
        End Try
    End Function

    ''' <summary>
    ''' Executes a query and returns a SqlDataReader
    ''' NOTE: Caller must dispose of the reader and connection
    ''' </summary>
    Public Shared Function ExecuteReader(query As String, ParamArray parameters() As SqlParameter) As SqlDataReader
        Try
            Dim conn As SqlConnection = GetConnection()
            Dim cmd As New SqlCommand(query, conn)
            cmd.CommandType = CommandType.Text
            If parameters IsNot Nothing Then
                cmd.Parameters.AddRange(parameters)
            End If

            conn.Open()
            Return cmd.ExecuteReader(CommandBehavior.CloseConnection)
        Catch ex As Exception
            LogError("ExecuteReader", ex)
            Throw New ApplicationException("Database error occurred while executing reader.", ex)
        End Try
    End Function

#End Region

#Region "Stored Procedure Methods"

    ''' <summary>
    ''' Executes a stored procedure with parameters
    ''' </summary>
    Public Shared Function ExecuteStoredProcedure(procedureName As String, ParamArray parameters() As SqlParameter) As DataTable
        Dim dt As New DataTable()
        Try
            Using conn As SqlConnection = GetConnection()
                Using cmd As New SqlCommand(procedureName, conn)
                    cmd.CommandType = CommandType.StoredProcedure
                    If parameters IsNot Nothing Then
                        cmd.Parameters.AddRange(parameters)
                    End If

                    Using adapter As New SqlDataAdapter(cmd)
                        conn.Open()
                        adapter.Fill(dt)
                    End Using
                End Using
            End Using
            Return dt
        Catch ex As Exception
            LogError("ExecuteStoredProcedure", ex)
            Throw New ApplicationException("Database error occurred while executing stored procedure.", ex)
        End Try
    End Function

    ''' <summary>
    ''' Executes a stored procedure that returns multiple result sets
    ''' </summary>
    Public Shared Function ExecuteStoredProcedureMultipleResults(procedureName As String, ParamArray parameters() As SqlParameter) As DataSet
        Dim sw = Stopwatch.StartNew()
        Dim ds As New DataSet()
        Try
            Using conn As SqlConnection = GetConnection()
                Using cmd As New SqlCommand(procedureName, conn)
                    cmd.CommandType = CommandType.StoredProcedure
                    If parameters IsNot Nothing Then
                        cmd.Parameters.AddRange(parameters)
                    End If

                    Using adapter As New SqlDataAdapter(cmd)
                        conn.Open()
                        adapter.Fill(ds)
                    End Using
                End Using
            End Using
            Return ds
        Catch ex As Exception
            LogError("ExecuteStoredProcedureMultipleResults", ex)
            Throw New ApplicationException("Database error occurred while executing stored procedure with multiple results.", ex)
        Finally
            Dim totalRows As Integer = 0
            For Each tbl As DataTable In ds.Tables : totalRows += tbl.Rows.Count : Next
            LogPerf("ExecuteStoredProcMulti:" & procedureName, sw.ElapsedMilliseconds, procedureName, totalRows)
        End Try
    End Function

#End Region

#Region "Company Methods"

    ''' <summary>
    ''' Gets all active companies
    ''' </summary>
    Public Shared Function GetAllCompanies() As DataTable
        Dim query As String = "SELECT * FROM Companies WHERE IsActive = 1 ORDER BY CompanyName"
        Return ExecuteDataTable(query)
    End Function

    ''' <summary>
    ''' Gets a company by ID
    ''' </summary>
    Public Shared Function GetCompanyById(companyId As Integer) As DataRow
        Dim query As String = "SELECT * FROM Companies WHERE CompanyID = @CompanyID"
        Dim dt As DataTable = ExecuteDataTable(query, New SqlParameter("@CompanyID", companyId))

        If dt.Rows.Count > 0 Then
            Return dt.Rows(0)
        End If
        Return Nothing
    End Function

    ''' <summary>
    ''' Creates a new company
    ''' </summary>
    Public Shared Function CreateCompany(companyName As String, shortName As String,
                                        contactEmail As String, contactPhone As String,
                                        Optional address As String = Nothing) As Integer
        Dim query As String = "INSERT INTO Companies (CompanyName, ShortName, ContactEmail, ContactPhone, Address) " &
                             "VALUES (@CompanyName, @ShortName, @ContactEmail, @ContactPhone, @Address); " &
                             "SELECT SCOPE_IDENTITY();"

        Dim parameters As SqlParameter() = {
            New SqlParameter("@CompanyName", companyName),
            New SqlParameter("@ShortName", If(String.IsNullOrEmpty(shortName), DBNull.Value, shortName)),
            New SqlParameter("@ContactEmail", If(String.IsNullOrEmpty(contactEmail), DBNull.Value, contactEmail)),
            New SqlParameter("@ContactPhone", If(String.IsNullOrEmpty(contactPhone), DBNull.Value, contactPhone)),
            New SqlParameter("@Address", If(String.IsNullOrEmpty(address), DBNull.Value, address))
        }

        Return Convert.ToInt32(ExecuteScalar(query, parameters))
    End Function

#End Region

#Region "User Methods"

    ''' <summary>
    ''' Gets user by email address (primary login identifier)
    ''' </summary>
    Public Shared Function GetUserByEmail(email As String) As DataRow
        Dim query As String = "SELECT * FROM Users WHERE Email = @Email AND IsActive = 1"
        Dim dt As DataTable = ExecuteDataTable(query, New SqlParameter("@Email", email))
        If dt.Rows.Count > 0 Then
            Return dt.Rows(0)
        End If
        Return Nothing
    End Function

    ''' <summary>
    ''' Gets user by UserID
    ''' </summary>
    Public Shared Function GetUserById(userId As Guid) As DataRow
        Dim query As String = "SELECT * FROM Users WHERE UserID = @UserID AND IsActive = 1"
        Dim dt As DataTable = ExecuteDataTable(query, New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId})
        If dt.Rows.Count > 0 Then
            Return dt.Rows(0)
        End If
        Return Nothing
    End Function

    ''' <summary>
    ''' Updates last login time for user
    ''' </summary>
    Public Shared Sub UpdateLastLogin(userId As Guid)
        Dim query As String = "UPDATE Users SET LastLoginOn = GETDATE() WHERE UserID = @UserID"
        ExecuteNonQuery(query, New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId})
    End Sub

    ''' <summary>
    ''' Gets all users by role for a specific company
    ''' </summary>
    Public Shared Function GetUsersByRole(role As String, companyId As Integer) As DataTable
        EnsureNameColumns()
        Dim query As String = "SELECT UserID, Username, ISNULL(FirstName,'') AS FirstName, ISNULL(LastName,'') AS LastName, " &
                             "LTRIM(RTRIM(ISNULL(FirstName,'') + ' ' + ISNULL(LastName,''))) AS FullName, " &
                             "Email, Role FROM Users WHERE (Role = @Role or Role = 'Admin') AND CompanyID = @CompanyID AND IsActive = 1 ORDER BY Username"
        Return ExecuteDataTable(query,
            New SqlParameter("@Role", role),
            New SqlParameter("@CompanyID", companyId))
    End Function

    ''' <summary>
    ''' Gets all active users for a specific company
    ''' </summary>
    Public Shared Function GetAllActiveUsers(companyId As Integer) As DataTable
        EnsureNameColumns()
        Dim query As String = "SELECT UserID, Username, ISNULL(FirstName,'') AS FirstName, ISNULL(LastName,'') AS LastName, " &
                             "LTRIM(RTRIM(ISNULL(FirstName,'') + ' ' + ISNULL(LastName,''))) AS FullName, " &
                             "Email, Role FROM Users WHERE CompanyID = @CompanyID AND IsActive = 1 ORDER BY Username"
        Return ExecuteDataTable(query, New SqlParameter("@CompanyID", companyId))
    End Function

    ''' <summary>
    ''' Gets all users for a specific company
    ''' </summary>
    Public Shared Function GetUsersByCompany(companyId As Integer) As DataTable
        EnsureNameColumns()
        Dim query As String = "SELECT UserID, Username, ISNULL(FirstName,'') AS FirstName, ISNULL(LastName,'') AS LastName, " &
                             "LTRIM(RTRIM(ISNULL(FirstName,'') + ' ' + ISNULL(LastName,''))) AS FullName, " &
                             "Email, Role FROM Users WHERE CompanyID = @CompanyID AND IsActive = 1 ORDER BY Username"
        Return ExecuteDataTable(query, New SqlParameter("@CompanyID", companyId))
    End Function

    ''' <summary>
    ''' Gets workers (Admin/Worker roles) for a specific company
    ''' </summary>
    Public Shared Function GetWorkersByCompany(companyId As Integer) As DataTable
        EnsureNameColumns()
        Dim query As String = "SELECT UserID, Username, ISNULL(FirstName,'') AS FirstName, ISNULL(LastName,'') AS LastName, " &
                             "LTRIM(RTRIM(ISNULL(FirstName,'') + ' ' + ISNULL(LastName,''))) AS FullName, " &
                             "Email FROM Users " &
                             "WHERE CompanyID = @CompanyID AND Role IN ('Admin', 'Worker') " &
                             "AND IsActive = 1 ORDER BY Username"
        Return ExecuteDataTable(query, New SqlParameter("@CompanyID", companyId))
    End Function

    ' ── Schema-check caches ───────────────────────────────────────────────
    ' These flags short-circuit the Ensure* helpers after their first successful
    ' run in the lifetime of an app-pool. Each Ensure* call previously cost a
    ' round trip to the DB (sometimes several) on every invocation, despite the
    ' work being a one-time schema migration that never undoes itself.
    Private Shared _nameColumnsEnsured As Boolean = False
    Private Shared _payrollColumnsEnsured As Boolean = False
    Private Shared _clientColumnsEnsured As Boolean = False
    Private Shared _ticketReadStatusEnsured As Boolean = False
    Private Shared _commentActivityBackfilled As Boolean = False
    Private Shared ReadOnly _defaultProjectChecked As New System.Collections.Concurrent.ConcurrentDictionary(Of Guid, Boolean)

    ''' <summary>
    ''' Adds FirstName / LastName / MustResetPassword columns to Users if they don't exist yet.
    ''' Safe to call repeatedly — runs the SQL only once per app-pool lifetime.
    ''' </summary>
    Public Shared Sub EnsureNameColumns()
        If _nameColumnsEnsured Then Return
        ExecuteNonQuery(
            "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Users') AND name = 'FirstName') " &
            "BEGIN " &
            "  ALTER TABLE dbo.Users ADD FirstName NVARCHAR(100) NULL; " &
            "  ALTER TABLE dbo.Users ADD LastName  NVARCHAR(100) NULL; " &
            "END")
        ExecuteNonQuery(
            "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Users') AND name = 'MustResetPassword') " &
            "ALTER TABLE dbo.Users ADD MustResetPassword BIT NOT NULL DEFAULT 0")
        _nameColumnsEnsured = True
    End Sub

    ''' <summary>
    ''' Adds PayType / HourlyRate / AnnualSalary columns to Users if they don't exist yet.
    ''' Cached after first successful run — see _payrollColumnsEnsured.
    ''' </summary>
    Public Shared Sub EnsurePayrollColumns()
        If _payrollColumnsEnsured Then Return
        ExecuteNonQuery(
            "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Users') AND name = 'PayType') " &
            "BEGIN " &
            "  ALTER TABLE dbo.Users ADD PayType     VARCHAR(10)    NULL; " &
            "  ALTER TABLE dbo.Users ADD HourlyRate  DECIMAL(10,2)  NULL; " &
            "  ALTER TABLE dbo.Users ADD AnnualSalary DECIMAL(12,2) NULL; " &
            "END")
        ExecuteNonQuery(
            "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Users') AND name = 'TimeRoundingMinutes') " &
            "ALTER TABLE dbo.Users ADD TimeRoundingMinutes INT NOT NULL DEFAULT 0")
        _payrollColumnsEnsured = True
    End Sub

    ''' <summary>
    ''' Ensures Clients table has extended address/contact columns.
    ''' Cached after first successful run; the 8 individual ALTER checks are also
    ''' folded into a single batched round trip when they do need to execute.
    ''' </summary>
    Public Shared Sub EnsureClientColumns()
        If _clientColumnsEnsured Then Return
        Dim batch As String =
            "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Clients') AND name='Address')  ALTER TABLE dbo.Clients ADD Address  NVARCHAR(200) NULL;" &
            "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Clients') AND name='City')     ALTER TABLE dbo.Clients ADD City     NVARCHAR(100) NULL;" &
            "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Clients') AND name='State')    ALTER TABLE dbo.Clients ADD State    NVARCHAR(100) NULL;" &
            "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Clients') AND name='Zip')      ALTER TABLE dbo.Clients ADD Zip      NVARCHAR(20)  NULL;" &
            "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Clients') AND name='Country')  ALTER TABLE dbo.Clients ADD Country  NVARCHAR(100) NULL;" &
            "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Clients') AND name='Website')  ALTER TABLE dbo.Clients ADD Website  NVARCHAR(255) NULL;" &
            "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Clients') AND name='Notes')    ALTER TABLE dbo.Clients ADD Notes    NVARCHAR(MAX) NULL;" &
            "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Clients') AND name='TaxID')    ALTER TABLE dbo.Clients ADD TaxID    NVARCHAR(100) NULL;"
        ExecuteNonQuery(batch)
        _clientColumnsEnsured = True
    End Sub

    ''' <summary>
    ''' Ensures the TicketReadStatus(UserID, TicketID, LastViewedOn) table exists.
    ''' Used to drive the "unread comment" badge on the ticket hopper — when a user
    ''' opens a ticket, their LastViewedOn for it is upserted; the hopper query then
    ''' counts comments newer than that timestamp authored by other users.
    ''' Cached after first successful run via _ticketReadStatusEnsured.
    ''' </summary>
    Public Shared Sub EnsureTicketReadStatusTable()
        If _ticketReadStatusEnsured Then Return
        ExecuteNonQuery(
            "IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TicketReadStatus') " &
            "CREATE TABLE dbo.TicketReadStatus (" &
            "  UserID UNIQUEIDENTIFIER NOT NULL, " &
            "  TicketID UNIQUEIDENTIFIER NOT NULL, " &
            "  LastViewedOn DATETIME NOT NULL, " &
            "  CONSTRAINT PK_TicketReadStatus PRIMARY KEY (UserID, TicketID))")
        _ticketReadStatusEnsured = True
    End Sub

    ''' <summary>
    ''' Backfills TicketActivity rows for any TicketComments that don't have a
    ''' matching activity row (legacy / migrated data). Comments created through
    ''' the app's normal flow always insert into both tables together; older
    ''' migration scripts only populated TicketComments. Without this, migrated
    ''' comments are invisible in the ticket-details thread (which renders from
    ''' TicketActivity). Cached after first successful run via
    ''' _commentActivityBackfilled.
    ''' </summary>
    Public Shared Sub EnsureCommentActivityBackfill()
        If _commentActivityBackfilled Then Return
        Try
            ExecuteNonQuery(
                "INSERT INTO TicketActivity (TicketID, UserID, ActivityType, ActivityText, CommentID, CreatedOn, IsPrivate) " &
                "SELECT tc.TicketID, tc.CreatedBy, 'Comment', tc.CommentText, tc.CommentID, tc.Timestamp, ISNULL(tc.Private, 0) " &
                "FROM TicketComments tc " &
                "WHERE NOT EXISTS (SELECT 1 FROM TicketActivity ta WHERE ta.CommentID = tc.CommentID)")
            _commentActivityBackfilled = True
        Catch
            ' Table mismatch / permission issue — leave the flag false so we
            ' retry on the next request rather than silently never running.
        End Try
    End Sub

    ''' <summary>
    ''' Returns every unread comment authored by someone other than the given user
    ''' on a ticket the user can see in the given company. "Unread" = comment's
    ''' Timestamp is newer than the user's TicketReadStatus.LastViewedOn for that
    ''' ticket, OR the user has no read-status row yet. Newest comment first.
    ''' Each row carries the ticket subject + number so the New Comments list can
    ''' render a self-contained card.
    ''' </summary>
    Public Shared Function GetUnreadCommentsForUser(userId As Guid, companyId As Integer) As DataTable
        EnsureTicketReadStatusTable()
        ' Sourced from TicketActivity (ActivityType = 'Comment') rather than from
        ' TicketComments directly — that's the table the ticket-details panel
        ' renders from, so this guarantees every card in the New Comments list
        ' has a matching row in the ticket's actual thread. (Migration data has
        ' some TicketComments rows without TicketActivity counterparts; querying
        ' from TicketComments would surface those orphans, which is what
        ' originally led to "the comment I clicked isn't in the thread".)
        ' Hopper-scoped to assignee/collaborator so admins don't get noise from
        ' tickets they have nothing to do with.
        Dim query As String =
            "SELECT ta.ActivityID AS CommentID, ta.TicketID, " &
            "       ta.ActivityText AS CommentText, ta.CreatedOn AS Timestamp, " &
            "       t.TicketNumber, t.Subject, " &
            "       CASE WHEN LTRIM(RTRIM(ISNULL(u.FirstName,'') + ' ' + ISNULL(u.LastName,''))) <> '' " &
            "            THEN LTRIM(RTRIM(ISNULL(u.FirstName,'') + ' ' + ISNULL(u.LastName,''))) " &
            "            ELSE u.Username END AS AuthorName " &
            "FROM TicketActivity ta " &
            "INNER JOIN Tickets t ON ta.TicketID = t.TicketID " &
            "INNER JOIN Projects p ON t.ProjectID = p.ProjectID " &
            "INNER JOIN Clients c ON p.ClientID = c.ClientID " &
            "INNER JOIN Users u ON ta.UserID = u.UserID " &
            "LEFT JOIN TicketReadStatus trs ON trs.TicketID = ta.TicketID AND trs.UserID = @UserID " &
            "WHERE c.CompanyID = @CompanyID " &
            "  AND ta.ActivityType = 'Comment' " &
            "  AND t.IsActive = 1 AND t.IsArchived = 0 " &
            "  AND ta.UserID <> @UserID " &
            "  AND (trs.LastViewedOn IS NULL OR ta.CreatedOn > trs.LastViewedOn) " &
            "  AND (t.AssignedTo = @UserID " &
            "       OR EXISTS (SELECT 1 FROM Collaborators col " &
            "                  WHERE col.TicketID = t.TicketID AND col.UserID = @UserID)) " &
            "ORDER BY ta.CreatedOn DESC"
        Return ExecuteDataTable(query,
            New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId},
            New SqlParameter("@CompanyID", companyId))
    End Function

    ''' <summary>
    ''' Records that the given user has viewed the given ticket (now). Upserts the
    ''' TicketReadStatus row so subsequent comments by other users count as unread
    ''' until the user opens the ticket again.
    ''' </summary>
    ''' <summary>
    ''' Bulk mark-read for a set of comment ActivityIDs. For each ticket
    ''' represented in the selection, the user's TicketReadStatus.LastViewedOn
    ''' is bumped to the MAX timestamp of selected comments on that ticket
    ''' (so older selected comments + any earlier unread ones become read,
    ''' but later unread comments on the same ticket remain unread). MERGE
    ''' is used so a missing read-status row is inserted as well.
    ''' </summary>
    Public Shared Sub MarkCommentActivitiesRead(userId As Guid, activityIds As IList(Of Guid))
        If activityIds Is Nothing OrElse activityIds.Count = 0 Then Return
        EnsureTicketReadStatusTable()

        Dim paramNames As New List(Of String)
        Dim params As New List(Of SqlParameter)
        params.Add(New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId})
        For i As Integer = 0 To activityIds.Count - 1
            Dim n As String = "@A" & i
            paramNames.Add(n)
            params.Add(New SqlParameter(n, SqlDbType.UniqueIdentifier) With {.Value = activityIds(i)})
        Next

        Dim merge As String =
            "MERGE dbo.TicketReadStatus AS target " &
            "USING (SELECT @UserID AS UserID, ta.TicketID, MAX(ta.CreatedOn) AS LastSeen " &
            "       FROM TicketActivity ta " &
            "       WHERE ta.ActivityID IN (" & String.Join(", ", paramNames) & ") " &
            "         AND ta.ActivityType = 'Comment' " &
            "       GROUP BY ta.TicketID) AS src " &
            "ON target.UserID = src.UserID AND target.TicketID = src.TicketID " &
            "WHEN MATCHED AND target.LastViewedOn < src.LastSeen THEN " &
            "    UPDATE SET LastViewedOn = src.LastSeen " &
            "WHEN NOT MATCHED THEN " &
            "    INSERT (UserID, TicketID, LastViewedOn) VALUES (src.UserID, src.TicketID, src.LastSeen);"
        ExecuteNonQuery(merge, params.ToArray())
    End Sub

    Public Shared Sub MarkTicketRead(userId As Guid, ticketId As Guid)
        EnsureTicketReadStatusTable()
        ExecuteNonQuery(
            "MERGE dbo.TicketReadStatus AS target " &
            "USING (SELECT @UserID AS UserID, @TicketID AS TicketID) AS src " &
            "  ON target.UserID = src.UserID AND target.TicketID = src.TicketID " &
            "WHEN MATCHED THEN UPDATE SET LastViewedOn = GETDATE() " &
            "WHEN NOT MATCHED THEN INSERT (UserID, TicketID, LastViewedOn) VALUES (@UserID, @TicketID, GETDATE());",
            New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId},
            New SqlParameter("@TicketID", SqlDbType.UniqueIdentifier) With {.Value = ticketId})
    End Sub

    ''' <summary>Returns all clients for admin management (active and inactive), including extended fields.</summary>
    Public Shared Function GetAllClientsAdmin(companyId As Integer) As DataTable
        EnsureClientColumns()
        Dim query As String =
            "SELECT ClientID, CompanyID, ClientName, ISNULL(ShortName,'') AS ShortName, " &
            "ISNULL(Billable,1) AS Billable, ISNULL(ContactEmail,'') AS ContactEmail, " &
            "ISNULL(ContactPhone,'') AS ContactPhone, ISNULL(Address,'') AS Address, " &
            "ISNULL(City,'') AS City, ISNULL(State,'') AS State, ISNULL(Zip,'') AS Zip, " &
            "ISNULL(Country,'') AS Country, ISNULL(Website,'') AS Website, " &
            "ISNULL(Notes,'') AS Notes, ISNULL(TaxID,'') AS TaxID, " &
            "IsActive, CreatedOn " &
            "FROM Clients WHERE CompanyID = @CompanyID " &
            "ORDER BY IsActive DESC, ClientName"
        Return ExecuteDataTable(query, New SqlParameter("@CompanyID", companyId))
    End Function

    ''' <summary>Returns all projects for a client (active and inactive) for admin management.</summary>
    Public Shared Function GetProjectsByClientAdmin(clientId As Guid) As DataTable
        Dim query As String =
            "SELECT ProjectID, ClientID, Name, ISNULL(Status,'Active') AS Status, " &
            "ISNULL(Description,'') AS Description, IsActive, CreatedOn " &
            "FROM Projects WHERE ClientID = @ClientID ORDER BY Name"
        Return ExecuteDataTable(query, New SqlParameter("@ClientID", SqlDbType.UniqueIdentifier) With {.Value = clientId})
    End Function

    ''' <summary>
    ''' Batch variant: returns ALL projects across every client of the given company in one query.
    ''' Used by Admin to avoid N+1 lookups when binding the nested project repeater.
    ''' </summary>
    Public Shared Function GetAllProjectsForCompanyAdmin(companyId As Integer) As DataTable
        Dim query As String =
            "SELECT p.ProjectID, p.ClientID, p.Name, ISNULL(p.Status,'Active') AS Status, " &
            "ISNULL(p.Description,'') AS Description, p.IsActive, p.CreatedOn " &
            "FROM Projects p " &
            "INNER JOIN Clients c ON p.ClientID = c.ClientID " &
            "WHERE c.CompanyID = @CompanyID " &
            "ORDER BY p.ClientID, p.Name"
        Return ExecuteDataTable(query, New SqlParameter("@CompanyID", companyId))
    End Function

    ''' <summary>Creates a new client record.</summary>
    Public Shared Function CreateClient(companyId As Integer, clientName As String,
                                        shortName As String, billable As Boolean,
                                        contactEmail As String, contactPhone As String,
                                        address As String, city As String, state As String,
                                        zip As String, country As String, website As String,
                                        notes As String, taxId As String) As Guid
        EnsureClientColumns()

        Dim insertClient As String =
            "INSERT INTO Clients (CompanyID, ClientName, ShortName, Billable, ContactEmail, ContactPhone, " &
            "Address, City, State, Zip, Country, Website, Notes, TaxID, IsActive) " &
            "OUTPUT INSERTED.ClientID " &
            "VALUES (@CompanyID, @ClientName, @ShortName, @Billable, @ContactEmail, @ContactPhone, " &
            "@Address, @City, @State, @Zip, @Country, @Website, @Notes, @TaxID, 1)"

        Dim insertProject As String =
            "INSERT INTO Projects (ClientID, Name, Description, Status, IsActive) " &
            "VALUES (@ClientID, @Name, @Description, @Status, 1)"

        Using conn As New SqlConnection(ConnectionString)
            conn.Open()
            Using tran As SqlTransaction = conn.BeginTransaction()
                Try
                    Dim newClientId As Guid
                    Using cmd As New SqlCommand(insertClient, conn, tran)
                        cmd.Parameters.AddWithValue("@CompanyID",    companyId)
                        cmd.Parameters.AddWithValue("@ClientName",   clientName)
                        cmd.Parameters.AddWithValue("@ShortName",    If(String.IsNullOrEmpty(shortName),    CObj(DBNull.Value), shortName))
                        cmd.Parameters.AddWithValue("@Billable",     billable)
                        cmd.Parameters.AddWithValue("@ContactEmail", If(String.IsNullOrEmpty(contactEmail), CObj(DBNull.Value), contactEmail))
                        cmd.Parameters.AddWithValue("@ContactPhone", If(String.IsNullOrEmpty(contactPhone), CObj(DBNull.Value), contactPhone))
                        cmd.Parameters.AddWithValue("@Address",      If(String.IsNullOrEmpty(address),      CObj(DBNull.Value), address))
                        cmd.Parameters.AddWithValue("@City",         If(String.IsNullOrEmpty(city),         CObj(DBNull.Value), city))
                        cmd.Parameters.AddWithValue("@State",        If(String.IsNullOrEmpty(state),        CObj(DBNull.Value), state))
                        cmd.Parameters.AddWithValue("@Zip",          If(String.IsNullOrEmpty(zip),          CObj(DBNull.Value), zip))
                        cmd.Parameters.AddWithValue("@Country",      If(String.IsNullOrEmpty(country),      CObj(DBNull.Value), country))
                        cmd.Parameters.AddWithValue("@Website",      If(String.IsNullOrEmpty(website),      CObj(DBNull.Value), website))
                        cmd.Parameters.AddWithValue("@Notes",        If(String.IsNullOrEmpty(notes),        CObj(DBNull.Value), notes))
                        cmd.Parameters.AddWithValue("@TaxID",        If(String.IsNullOrEmpty(taxId),        CObj(DBNull.Value), taxId))
                        newClientId = Guid.Parse(cmd.ExecuteScalar().ToString())
                    End Using

                    ' Every client gets a default "General Support" project automatically
                    Using cmd As New SqlCommand(insertProject, conn, tran)
                        cmd.Parameters.Add(New SqlParameter("@ClientID", SqlDbType.UniqueIdentifier) With {.Value = newClientId})
                        cmd.Parameters.AddWithValue("@Name",        "General Support")
                        cmd.Parameters.AddWithValue("@Description", "Default project for tickets not tied to a specific engagement.")
                        cmd.Parameters.AddWithValue("@Status",      "Active")
                        cmd.ExecuteNonQuery()
                    End Using

                    tran.Commit()
                    Return newClientId
                Catch
                    tran.Rollback()
                    Throw
                End Try
            End Using
        End Using
    End Function

    ''' <summary>Updates an existing client record.</summary>
    Public Shared Function UpdateClient(clientId As Guid, clientName As String,
                                        shortName As String, billable As Boolean,
                                        contactEmail As String, contactPhone As String,
                                        address As String, city As String, state As String,
                                        zip As String, country As String, website As String,
                                        notes As String, taxId As String,
                                        isActive As Boolean) As Integer
        EnsureClientColumns()
        Dim query As String =
            "UPDATE Clients SET ClientName=@ClientName, ShortName=@ShortName, Billable=@Billable, " &
            "ContactEmail=@ContactEmail, ContactPhone=@ContactPhone, Address=@Address, City=@City, " &
            "State=@State, Zip=@Zip, Country=@Country, Website=@Website, Notes=@Notes, TaxID=@TaxID, " &
            "IsActive=@IsActive WHERE ClientID=@ClientID"
        Return ExecuteNonQuery(query,
            New SqlParameter("@ClientID",     SqlDbType.UniqueIdentifier) With {.Value = clientId},
            New SqlParameter("@ClientName",   clientName),
            New SqlParameter("@ShortName",    If(String.IsNullOrEmpty(shortName),    CObj(DBNull.Value), shortName)),
            New SqlParameter("@Billable",     billable),
            New SqlParameter("@ContactEmail", If(String.IsNullOrEmpty(contactEmail), CObj(DBNull.Value), contactEmail)),
            New SqlParameter("@ContactPhone", If(String.IsNullOrEmpty(contactPhone), CObj(DBNull.Value), contactPhone)),
            New SqlParameter("@Address",      If(String.IsNullOrEmpty(address),      CObj(DBNull.Value), address)),
            New SqlParameter("@City",         If(String.IsNullOrEmpty(city),         CObj(DBNull.Value), city)),
            New SqlParameter("@State",        If(String.IsNullOrEmpty(state),        CObj(DBNull.Value), state)),
            New SqlParameter("@Zip",          If(String.IsNullOrEmpty(zip),          CObj(DBNull.Value), zip)),
            New SqlParameter("@Country",      If(String.IsNullOrEmpty(country),      CObj(DBNull.Value), country)),
            New SqlParameter("@Website",      If(String.IsNullOrEmpty(website),      CObj(DBNull.Value), website)),
            New SqlParameter("@Notes",        If(String.IsNullOrEmpty(notes),        CObj(DBNull.Value), notes)),
            New SqlParameter("@TaxID",        If(String.IsNullOrEmpty(taxId),        CObj(DBNull.Value), taxId)),
            New SqlParameter("@IsActive",     isActive))
    End Function

    ''' <summary>Sets a client active or inactive.</summary>
    Public Shared Function SetClientActiveStatus(clientId As Guid, isActive As Boolean) As Integer
        Dim query As String = "UPDATE Clients SET IsActive=@IsActive WHERE ClientID=@ClientID"
        Return ExecuteNonQuery(query,
            New SqlParameter("@ClientID", SqlDbType.UniqueIdentifier) With {.Value = clientId},
            New SqlParameter("@IsActive", isActive))
    End Function

    ''' <summary>
    ''' Ensures every client in the database has a "General Support" project.
    ''' Creates one for any client that is missing it. Safe to call repeatedly.
    ''' </summary>
    Public Shared Sub EnsureDefaultProjects()
        ' For every client that has no project named "General Support", create one in a single statement.
        ' Use explicit NEWID() so this works regardless of whether ProjectID has a column default.
        Dim query As String =
            "INSERT INTO Projects (ProjectID, ClientID, Name, Description, Status, IsActive) " &
            "SELECT NEWID(), c.ClientID, 'General Support', " &
            "'Default project for tickets not tied to a specific engagement.', 'Active', 1 " &
            "FROM Clients c " &
            "WHERE NOT EXISTS (" &
            "    SELECT 1 FROM Projects p " &
            "    WHERE p.ClientID = c.ClientID AND p.Name = 'General Support'" &
            ")"
        ExecuteNonQuery(query)
    End Sub

    ''' <summary>
    ''' Ensures a single client has a "General Support" project. Called on-the-fly
    ''' when populating project dropdowns so no app restart is needed.
    ''' </summary>
    Private Shared Sub EnsureDefaultProjectForClient(clientId As Guid)
        Dim query As String =
            "IF NOT EXISTS (SELECT 1 FROM Projects WHERE ClientID = @ClientID AND Name = 'General Support') " &
            "INSERT INTO Projects (ProjectID, ClientID, Name, Description, Status, IsActive) " &
            "VALUES (NEWID(), @ClientID, 'General Support', " &
            "'Default project for tickets not tied to a specific engagement.', 'Active', 1)"
        ExecuteNonQuery(query, New SqlParameter("@ClientID", SqlDbType.UniqueIdentifier) With {.Value = clientId})
    End Sub

    ''' <summary>
    ''' Returns the ProjectID for the given client/name (case-insensitive).
    ''' If no matching active project exists, creates one and returns its new ID.
    ''' </summary>
    Public Shared Function ResolveOrCreateProject(clientId As Guid, name As String) As Guid
        Dim trimmed As String = If(name, "").Trim()
        If String.IsNullOrEmpty(trimmed) Then
            Throw New ArgumentException("Project name is required.", "name")
        End If

        Dim lookup As String =
            "SELECT TOP 1 ProjectID FROM Projects " &
            "WHERE ClientID = @ClientID AND IsActive = 1 AND LOWER(Name) = LOWER(@Name)"
        Dim existing As Object = ExecuteScalar(lookup,
            New SqlParameter("@ClientID", SqlDbType.UniqueIdentifier) With {.Value = clientId},
            New SqlParameter("@Name",     trimmed))

        If existing IsNot Nothing AndAlso Not Convert.IsDBNull(existing) Then
            Return Guid.Parse(existing.ToString())
        End If

        Return CreateProject(clientId, trimmed, "", "Active")
    End Function

    ''' <summary>Creates a new project for a client.</summary>
    Public Shared Function CreateProject(clientId As Guid, name As String,
                                         description As String, status As String) As Guid
        Dim query As String =
            "INSERT INTO Projects (ClientID, Name, Description, Status, IsActive) " &
            "OUTPUT INSERTED.ProjectID " &
            "VALUES (@ClientID, @Name, @Description, @Status, 1)"
        Return Guid.Parse(ExecuteScalar(query,
            New SqlParameter("@ClientID",    SqlDbType.UniqueIdentifier) With {.Value = clientId},
            New SqlParameter("@Name",        name),
            New SqlParameter("@Description", If(String.IsNullOrEmpty(description), CObj(DBNull.Value), description)),
            New SqlParameter("@Status",      If(String.IsNullOrEmpty(status), "Active", status))
        ).ToString())
    End Function

    ''' <summary>Updates an existing project.</summary>
    Public Shared Function UpdateProject(projectId As Guid, name As String,
                                         description As String, status As String,
                                         isActive As Boolean) As Integer
        Dim query As String =
            "UPDATE Projects SET Name=@Name, Description=@Description, Status=@Status, IsActive=@IsActive " &
            "WHERE ProjectID=@ProjectID"
        Return ExecuteNonQuery(query,
            New SqlParameter("@ProjectID",   SqlDbType.UniqueIdentifier) With {.Value = projectId},
            New SqlParameter("@Name",        name),
            New SqlParameter("@Description", If(String.IsNullOrEmpty(description), CObj(DBNull.Value), description)),
            New SqlParameter("@Status",      If(String.IsNullOrEmpty(status), "Active", status)),
            New SqlParameter("@IsActive",    isActive))
    End Function

    ''' <summary>Returns the Name of a project by its ID, or an empty string if not found.</summary>
    Public Shared Function GetProjectName(projectId As Guid) As String
        Dim result As Object = ExecuteScalar(
            "SELECT Name FROM Projects WHERE ProjectID = @ID",
            New SqlParameter("@ID", SqlDbType.UniqueIdentifier) With {.Value = projectId})
        If result Is Nothing OrElse IsDBNull(result) Then Return ""
        Return result.ToString()
    End Function

    ''' <summary>
    ''' Returns True if a project with the given name already exists for the client.
    ''' Pass excludeProjectId to skip the current project when checking during an edit.
    ''' </summary>
    Public Shared Function ProjectNameExistsForClient(clientId As Guid, name As String,
                                                       Optional excludeProjectId As Guid? = Nothing) As Boolean
        Dim query As String =
            "SELECT COUNT(1) FROM Projects WHERE ClientID = @ClientID AND LOWER(Name) = LOWER(@Name)"
        Dim params As New List(Of SqlParameter) From {
            New SqlParameter("@ClientID", SqlDbType.UniqueIdentifier) With {.Value = clientId},
            New SqlParameter("@Name", name)
        }
        If excludeProjectId.HasValue Then
            query &= " AND ProjectID <> @ExcludeID"
            params.Add(New SqlParameter("@ExcludeID", SqlDbType.UniqueIdentifier) With {.Value = excludeProjectId.Value})
        End If
        Return Convert.ToInt32(ExecuteScalar(query, params.ToArray())) > 0
    End Function

    ''' <summary>Sets a project active or inactive.</summary>
    Public Shared Function SetProjectActiveStatus(projectId As Guid, isActive As Boolean) As Integer
        Dim query As String = "UPDATE Projects SET IsActive=@IsActive WHERE ProjectID=@ProjectID"
        Return ExecuteNonQuery(query,
            New SqlParameter("@ProjectID", SqlDbType.UniqueIdentifier) With {.Value = projectId},
            New SqlParameter("@IsActive",  isActive))
    End Function

    ''' <summary>
    ''' Adds ParentCommentID to TicketComments if it doesn't exist yet (threaded replies support).
    ''' Safe to call repeatedly.
    ''' </summary>
    Private Shared Sub EnsureParentCommentColumn()
        ExecuteNonQuery(
            "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketComments') AND name = 'ParentCommentID') " &
            "BEGIN " &
            "  ALTER TABLE dbo.TicketComments ADD ParentCommentID UNIQUEIDENTIFIER NULL; " &
            "  ALTER TABLE dbo.TicketComments ADD CONSTRAINT FK_TicketComments_Parent " &
            "    FOREIGN KEY (ParentCommentID) REFERENCES dbo.TicketComments(CommentID); " &
            "END")
    End Sub

    ''' <summary>
    ''' Gets all users for admin management (active and inactive), including payroll fields.
    ''' Auto-creates payroll columns if the patch has not yet been applied.
    ''' </summary>
    Public Shared Function GetAllUsersAdmin(companyId As Integer) As DataTable
        EnsureNameColumns()
        EnsurePayrollColumns()
        Dim query As String =
            "SELECT UserID, Username, ISNULL(FirstName,'') AS FirstName, ISNULL(LastName,'') AS LastName, " &
            "LTRIM(RTRIM(ISNULL(FirstName,'') + ' ' + ISNULL(LastName,''))) AS FullName, " &
            "Email, Role, IsActive, LastLoginOn, CreatedOn, " &
            "ISNULL(PayType,'Hourly') AS PayType, " &
            "ISNULL(HourlyRate,0) AS HourlyRate, " &
            "ISNULL(AnnualSalary,0) AS AnnualSalary, " &
            "ISNULL(TimeRoundingMinutes,0) AS TimeRoundingMinutes " &
            "FROM Users WHERE CompanyID = @CompanyID " &
            "ORDER BY IsActive DESC, Username"
        Return ExecuteDataTable(query, New SqlParameter("@CompanyID", companyId))
    End Function

    ''' <summary>
    ''' Creates a new user account
    ''' </summary>
    Public Shared Function CreateUser(username As String, email As String, passwordHash As String,
                                     role As String, companyId As Integer,
                                     Optional firstName As String = "",
                                     Optional lastName As String = "",
                                     Optional payType As String = "Hourly",
                                     Optional hourlyRate As Decimal? = Nothing,
                                     Optional annualSalary As Decimal? = Nothing,
                                     Optional timeRoundingMinutes As Integer = 0) As Guid
        EnsureNameColumns()
        EnsurePayrollColumns()

        ' Check for duplicate email
        If Not String.IsNullOrEmpty(email) Then
            Dim exists As Object = ExecuteScalar("SELECT COUNT(1) FROM Users WHERE Email = @Email",
                New SqlParameter("@Email", email))
            If Convert.ToInt32(exists) > 0 Then
                Throw New ApplicationException("A user with email '" & email & "' already exists.")
            End If
        End If

        Dim query As String =
            "INSERT INTO Users (Username, Email, PasswordHash, FirstName, LastName, Role, CompanyID, IsActive, PayType, HourlyRate, AnnualSalary, TimeRoundingMinutes) " &
            "OUTPUT INSERTED.UserID " &
            "VALUES (@Username, @Email, @PasswordHash, @FirstName, @LastName, @Role, @CompanyID, 1, @PayType, @HourlyRate, @AnnualSalary, @TimeRoundingMinutes);"
        Return Guid.Parse(ExecuteScalar(query,
            New SqlParameter("@Username", username),
            New SqlParameter("@Email", email),
            New SqlParameter("@PasswordHash", passwordHash),
            New SqlParameter("@FirstName", If(String.IsNullOrEmpty(firstName), CObj(DBNull.Value), firstName)),
            New SqlParameter("@LastName", If(String.IsNullOrEmpty(lastName), CObj(DBNull.Value), lastName)),
            New SqlParameter("@Role", role),
            New SqlParameter("@CompanyID", companyId),
            New SqlParameter("@PayType", If(payType, "Hourly")),
            New SqlParameter("@HourlyRate", SqlDbType.Decimal) With {.Value = If(hourlyRate.HasValue, CObj(hourlyRate.Value), DBNull.Value)},
            New SqlParameter("@AnnualSalary", SqlDbType.Decimal) With {.Value = If(annualSalary.HasValue, CObj(annualSalary.Value), DBNull.Value)},
            New SqlParameter("@TimeRoundingMinutes", timeRoundingMinutes)
        ).ToString())
    End Function

    ''' <summary>
    ''' Updates user details including payroll fields
    ''' </summary>
    Public Shared Function UpdateUser(userId As Guid, username As String, email As String, role As String,
                                     Optional firstName As String = "",
                                     Optional lastName As String = "",
                                     Optional payType As String = "Hourly",
                                     Optional hourlyRate As Decimal? = Nothing,
                                     Optional annualSalary As Decimal? = Nothing,
                                     Optional timeRoundingMinutes As Integer = 0) As Integer
        EnsureNameColumns()
        EnsurePayrollColumns()

        ' Check for duplicate email (exclude current user)
        If Not String.IsNullOrEmpty(email) Then
            Dim exists As Object = ExecuteScalar("SELECT COUNT(1) FROM Users WHERE Email = @Email AND UserID <> @UserID",
                New SqlParameter("@Email", email),
                New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId})
            If Convert.ToInt32(exists) > 0 Then
                Throw New ApplicationException("A user with email '" & email & "' already exists.")
            End If
        End If

        Dim query As String =
            "UPDATE Users SET Username = @Username, Email = @Email, FirstName = @FirstName, LastName = @LastName, Role = @Role, " &
            "PayType = @PayType, HourlyRate = @HourlyRate, AnnualSalary = @AnnualSalary, " &
            "TimeRoundingMinutes = @TimeRoundingMinutes " &
            "WHERE UserID = @UserID"
        Return ExecuteNonQuery(query,
            New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId},
            New SqlParameter("@Username", username),
            New SqlParameter("@Email", email),
            New SqlParameter("@FirstName", If(String.IsNullOrEmpty(firstName), CObj(DBNull.Value), firstName)),
            New SqlParameter("@LastName", If(String.IsNullOrEmpty(lastName), CObj(DBNull.Value), lastName)),
            New SqlParameter("@Role", role),
            New SqlParameter("@PayType", If(payType, "Hourly")),
            New SqlParameter("@HourlyRate", SqlDbType.Decimal) With {.Value = If(hourlyRate.HasValue, CObj(hourlyRate.Value), DBNull.Value)},
            New SqlParameter("@AnnualSalary", SqlDbType.Decimal) With {.Value = If(annualSalary.HasValue, CObj(annualSalary.Value), DBNull.Value)},
            New SqlParameter("@TimeRoundingMinutes", timeRoundingMinutes))
    End Function

    ''' <summary>
    ''' Sets user active/inactive status
    ''' </summary>
    Public Shared Function SetUserActiveStatus(userId As Guid, isActive As Boolean) As Integer
        Dim query As String = "UPDATE Users SET IsActive = @IsActive WHERE UserID = @UserID"
        Return ExecuteNonQuery(query,
            New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId},
            New SqlParameter("@IsActive", isActive))
    End Function

    ''' <summary>
    ''' Updates user password hash
    ''' </summary>
    Public Shared Function SetUserPassword(userId As Guid, passwordHash As String) As Integer
        Dim query As String = "UPDATE Users SET PasswordHash = @PasswordHash WHERE UserID = @UserID"
        Return ExecuteNonQuery(query,
            New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId},
            New SqlParameter("@PasswordHash", passwordHash))
    End Function

    ''' <summary>
    ''' Checks if an email already exists (optionally excluding a specific user)
    ''' </summary>
    Public Shared Function EmailExists(email As String, Optional excludeUserId As Guid? = Nothing) As Boolean
        Dim query As String
        If excludeUserId.HasValue Then
            query = "SELECT COUNT(1) FROM Users WHERE Email = @Email AND UserID <> @ExcludeUserID"
            Return Convert.ToInt32(ExecuteScalar(query,
                New SqlParameter("@Email", email),
                New SqlParameter("@ExcludeUserID", SqlDbType.UniqueIdentifier) With {.Value = excludeUserId.Value})) > 0
        Else
            query = "SELECT COUNT(1) FROM Users WHERE Email = @Email"
            Return Convert.ToInt32(ExecuteScalar(query, New SqlParameter("@Email", email))) > 0
        End If
    End Function

#End Region

#Region "Client Methods"

    ''' <summary>
    ''' Gets all active clients for a specific company
    ''' </summary>
    Public Shared Function GetAllClients(companyId As Integer) As DataTable
        Dim query As String = "SELECT * FROM Clients WHERE CompanyID = @CompanyID AND IsActive = 1 ORDER BY ClientName"
        Return ExecuteDataTable(query, New SqlParameter("@CompanyID", companyId))
    End Function

    ''' <summary>
    ''' Gets all clients for a specific company (alias for GetAllClients)
    ''' </summary>
    Public Shared Function GetClientsByCompany(companyId As Integer) As DataTable
        Return GetAllClients(companyId)
    End Function

    ''' <summary>
    ''' Gets client by ID
    ''' </summary>
    Public Shared Function GetClientById(clientId As Guid) As DataRow
        Dim query As String = "SELECT * FROM Clients WHERE ClientID = @ClientID AND IsActive = 1"
        Dim dt As DataTable = ExecuteDataTable(query, New SqlParameter("@ClientID", SqlDbType.UniqueIdentifier) With {.Value = clientId})
        If dt.Rows.Count > 0 Then
            Return dt.Rows(0)
        End If
        Return Nothing
    End Function

#End Region

#Region "Project Methods"

    ''' <summary>
    ''' Gets all projects for a client
    ''' </summary>
    Public Shared Function GetProjectsByClient(clientId As Guid) As DataTable
        ' Guarantee "General Support" exists AND fetch the active project list in a single round trip.
        ' (Two separate SQL calls to the remote production DB added ~200-400ms of latency per fetch.)
        Dim query As String =
            "IF NOT EXISTS (SELECT 1 FROM Projects WHERE ClientID = @ClientID AND Name = 'General Support') " &
            "  INSERT INTO Projects (ProjectID, ClientID, Name, Description, Status, IsActive) " &
            "  VALUES (NEWID(), @ClientID, 'General Support', " &
            "          'Default project for tickets not tied to a specific engagement.', 'Active', 1); " &
            "SELECT ProjectID, Name FROM Projects WHERE ClientID = @ClientID AND IsActive = 1 ORDER BY Name;"
        Return ExecuteDataTable(query, New SqlParameter("@ClientID", SqlDbType.UniqueIdentifier) With {.Value = clientId})
    End Function

    ''' <summary>
    ''' Gets all active projects for a company (not filtered by client)
    ''' </summary>
    Public Shared Function GetProjectsByCompany(companyId As Integer) As DataTable
        Dim query As String =
            "SELECT p.ProjectID, p.Name FROM Projects p " &
            "INNER JOIN Clients c ON p.ClientID = c.ClientID " &
            "WHERE c.CompanyID = @CompanyID AND p.IsActive = 1 ORDER BY p.Name"
        Return ExecuteDataTable(query, New SqlParameter("@CompanyID", companyId))
    End Function

    ''' <summary>
    ''' Gets project by ID
    ''' </summary>
    Public Shared Function GetProjectById(projectId As Guid) As DataRow
        Dim query As String = "SELECT * FROM Projects WHERE ProjectID = @ProjectID AND IsActive = 1"
        Dim dt As DataTable = ExecuteDataTable(query, New SqlParameter("@ProjectID", SqlDbType.UniqueIdentifier) With {.Value = projectId})
        If dt.Rows.Count > 0 Then
            Return dt.Rows(0)
        End If
        Return Nothing
    End Function

#End Region

#Region "Status and Priority Methods"

    ''' <summary>
    ''' Gets all active statuses
    ''' </summary>
    Public Shared Function GetAllStatuses() As DataTable
        Dim query As String = "SELECT * FROM Status WHERE IsActive = 1 ORDER BY DisplayOrder"
        Return ExecuteDataTable(query)
    End Function

    ''' <summary>
    ''' Gets all active priorities
    ''' </summary>
    Public Shared Function GetAllPriorities() As DataTable
        Dim query As String = "SELECT * FROM Priority WHERE IsActive = 1 ORDER BY DisplayOrder"
        Return ExecuteDataTable(query)
    End Function

#End Region

#Region "Ticket Methods"

    ''' <summary>
    ''' Gets tickets visible to a user, with optional ClientID + status-group filters
    ''' pushed into the SQL WHERE clause.
    '''
    ''' Optional filters:
    '''   clientId    -- restricts to a single client. When set, the TOP cap is removed
    '''                  because a per-client result is bounded.
    '''   statusGroup -- one of "active" / "completed" / "closed". Pushed to SQL via
    '''                  StatusName comparisons. Same TOP-cap removal rationale.
    '''
    ''' Without filters, returns the user's role-scoped ticket set capped at TOP 5000.
    ''' (Was TOP 500 historically — raised after the Asana migration brought ~3,400
    ''' tickets in a single company, which made the old cap silently hide tickets for
    ''' clients whose newest item fell outside the most-recent 500.)
    ''' </summary>
    Public Shared Function GetTicketsByUser(userId As Guid, userRole As String,
                                             Optional clientId As Guid? = Nothing,
                                             Optional statusGroup As String = Nothing,
                                             Optional searchTerm As String = Nothing,
                                             Optional clientIds As IList(Of Guid) = Nothing) As DataTable
        EnsureNameColumns()

        Dim companyQuery As String = "SELECT CompanyID FROM Users WHERE UserID = @UserID"
        Dim companyId As Object = ExecuteScalar(companyQuery, New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId})
        If companyId Is Nothing Then Return New DataTable()

        Dim fullNameExpr As String = "CASE WHEN LTRIM(RTRIM(ISNULL({0}.FirstName,'') + ' ' + ISNULL({0}.LastName,''))) <> '' " &
                                     "THEN LTRIM(RTRIM(ISNULL({0}.FirstName,'') + ' ' + ISNULL({0}.LastName,''))) ELSE {0}.Username END"

        ' Make sure the read-status table exists before we LEFT JOIN it below.
        EnsureTicketReadStatusTable()

        ' NOTE: t.Description is intentionally omitted — the list view never displays it
        ' and it's the largest column per row (full comment-edited HTML). On a 3,385-row
        ' "All Tickets" admin pull this dropped the wire payload from ~8s to ~1s.
        ' The Details panel fetches Description separately via sp_GetTicketDetails RS1.
        '
        ' UnreadCount used to be a correlated subquery on every row (3,385 subquery
        ' executions per admin "All" load). Replaced with a single GROUP BY aggregate
        ' fired after this main query and merged in-memory below. TicketReadStatus
        ' is no longer joined here either since UnreadCount is the only thing that
        ' needed it.
        Dim selectCols As String =
            "t.TicketID, t.TicketNumber, t.Subject, " &
            "t.ProjectID, t.StatusID, t.PriorityID, t.StartDate, t.DueDate, " &
            "t.CreatedBy, t.AssignedTo, t.ProjectedHours, " &
            "t.CreatedOn, t.UpdatedOn, t.IsActive, " &
            "c.ClientID, c.ClientName, " &
            "p.Name AS ProjectName, " &
            "s.Name AS StatusName, " &
            "pr.Name AS PriorityName, " &
            "u1.Username AS CreatedByUsername, " &
            String.Format(fullNameExpr, "u1") & " AS CreatedByFullName, " &
            "u2.Username AS AssignedToUsername, " &
            String.Format(fullNameExpr, "u2") & " AS AssignedToFullName "

        Dim fromClause As String =
            "FROM Tickets t " &
            "INNER JOIN Projects p ON t.ProjectID = p.ProjectID " &
            "INNER JOIN Clients c ON p.ClientID = c.ClientID " &
            "INNER JOIN Status s ON t.StatusID = s.StatusID " &
            "INNER JOIN Priority pr ON t.PriorityID = pr.PriorityID " &
            "INNER JOIN Users u1 ON t.CreatedBy = u1.UserID " &
            "LEFT JOIN Users u2 ON t.AssignedTo = u2.UserID "

        ' Build the WHERE clause additively so role + optional filters compose cleanly.
        Dim wheres As New System.Collections.Generic.List(Of String)
        wheres.Add("t.IsActive = 1")
        wheres.Add("t.IsArchived = 0")
        wheres.Add("c.CompanyID = @CompanyID")

        Dim distinctClause As String = ""
        Dim collabJoin As String = ""

        Select Case userRole
            Case "Admin"
                ' no extra role filter
            Case "Worker"
                distinctClause = "DISTINCT "
                collabJoin = "LEFT JOIN Collaborators col ON t.TicketID = col.TicketID AND col.UserID = @UserID "
                wheres.Add("(t.CreatedBy = @UserID OR t.AssignedTo = @UserID OR col.UserID IS NOT NULL)")
            Case "Client"
                wheres.Add("t.CreatedBy = @UserID")
            Case Else
                distinctClause = "DISTINCT "
                collabJoin = "INNER JOIN Collaborators col ON t.TicketID = col.TicketID AND col.UserID = @UserID "
        End Select

        ' Optional ClientID filter — narrows to a single client. Pushed to SQL so we
        ' don't need the TOP cap to protect against runaway result sets.
        Dim params As New System.Collections.Generic.List(Of SqlParameter)
        params.Add(New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId})
        params.Add(New SqlParameter("@CompanyID", Convert.ToInt32(companyId)))

        ' Multi-client filter takes precedence over the legacy single clientId
        ' so callers using the new API don't double-filter. Empty list / Nothing
        ' falls through to the legacy single-client path below.
        If clientIds IsNot Nothing AndAlso clientIds.Count > 0 Then
            Dim clientParamNames As New System.Collections.Generic.List(Of String)
            For i As Integer = 0 To clientIds.Count - 1
                Dim pname As String = "@ClientID_" & i.ToString()
                clientParamNames.Add(pname)
                params.Add(New SqlParameter(pname, SqlDbType.UniqueIdentifier) With {.Value = clientIds(i)})
            Next
            wheres.Add("c.ClientID IN (" & String.Join(", ", clientParamNames) & ")")
        ElseIf clientId.HasValue Then
            wheres.Add("c.ClientID = @ClientID")
            params.Add(New SqlParameter("@ClientID", SqlDbType.UniqueIdentifier) With {.Value = clientId.Value})
        End If

        ' Optional status-group filter -- map to StatusName comparisons.
        If Not String.IsNullOrEmpty(statusGroup) Then
            Select Case statusGroup.ToLower()
                Case "active"
                    wheres.Add("s.Name NOT IN ('Completed', 'Closed')")
                Case "completed"
                    wheres.Add("s.Name = 'Completed'")
                Case "closed"
                    wheres.Add("s.Name = 'Closed'")
            End Select
        End If

        ' Optional search-term filter. LIKE %term% across the same columns the
        ' top-bar Global Search uses, so the in-grid search box becomes a real
        ' DB-backed search instead of a DOM-only filter.
        If Not String.IsNullOrWhiteSpace(searchTerm) Then
            wheres.Add("(t.TicketNumber LIKE @SearchTerm " &
                       " OR t.Subject LIKE @SearchTerm " &
                       " OR t.Description LIKE @SearchTerm " &
                       " OR c.ClientName LIKE @SearchTerm " &
                       " OR p.Name LIKE @SearchTerm " &
                       " OR ISNULL(u1.Username, '') LIKE @SearchTerm " &
                       " OR ISNULL(u2.Username, '') LIKE @SearchTerm)")
            params.Add(New SqlParameter("@SearchTerm", "%" & searchTerm.Trim() & "%"))
        End If

        ' When filters are present the result set is bounded by the filter; no TOP needed.
        ' Otherwise apply a 5000-row safety net (raised from the original 500 after the
        ' Asana migration brought ~3,400 tickets into a single company).
        Dim hasClientFilter As Boolean = clientId.HasValue OrElse (clientIds IsNot Nothing AndAlso clientIds.Count > 0)
        Dim isFiltered As Boolean = hasClientFilter OrElse Not String.IsNullOrEmpty(statusGroup) OrElse Not String.IsNullOrWhiteSpace(searchTerm)
        Dim topClause As String = If(isFiltered, "", "TOP 5000 ")

        Dim query As String = "SELECT " & distinctClause & topClause & selectCols & fromClause & collabJoin &
                              "WHERE " & String.Join(" AND ", wheres) & " ORDER BY t.CreatedOn DESC"

        Dim dt As DataTable = ExecuteDataTable(query, params.ToArray())

        ' Merge per-ticket unread comment counts. One bulk GROUP BY query that returns
        ' only tickets with an unread comment for this user (most tickets have zero
        ' unread, so the row count is usually small). Tickets not in the dict get
        ' UnreadCount=0 by default.
        AttachUnreadCounts(dt, userId, Convert.ToInt32(companyId))

        Return dt
    End Function

    ''' <summary>
    ''' Adds an UnreadCount column to <paramref name="dt"/> and populates it from a
    ''' single GROUP BY query against TicketActivity. Replaces the correlated subquery
    ''' that used to run once per ticket row (3,385× on admin "All" loads on this DB).
    ''' Errors are swallowed — UnreadCount defaults to 0, which is the safe display.
    ''' </summary>
    Private Shared Sub AttachUnreadCounts(dt As DataTable, userId As Guid, companyId As Integer)
        If dt Is Nothing Then Return
        If Not dt.Columns.Contains("UnreadCount") Then
            dt.Columns.Add("UnreadCount", GetType(Integer))
        End If
        For Each row As DataRow In dt.Rows
            row("UnreadCount") = 0
        Next
        If dt.Rows.Count = 0 Then Return

        Try
            Dim aggregateQuery As String =
                "SELECT ta.TicketID, COUNT(*) AS UnreadCount " &
                "FROM TicketActivity ta " &
                "INNER JOIN Tickets t ON ta.TicketID = t.TicketID " &
                "INNER JOIN Projects p ON t.ProjectID = p.ProjectID " &
                "INNER JOIN Clients c ON p.ClientID = c.ClientID " &
                "LEFT JOIN TicketReadStatus trs ON trs.TicketID = ta.TicketID AND trs.UserID = @UserID " &
                "WHERE c.CompanyID = @CompanyID " &
                "  AND ta.ActivityType = 'Comment' " &
                "  AND ta.UserID <> @UserID " &
                "  AND (trs.LastViewedOn IS NULL OR ta.CreatedOn > trs.LastViewedOn) " &
                "  AND t.IsActive = 1 AND t.IsArchived = 0 " &
                "GROUP BY ta.TicketID"

            Dim aggDt As DataTable = ExecuteDataTable(aggregateQuery,
                New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId},
                New SqlParameter("@CompanyID", companyId))

            Dim counts As New Dictionary(Of Guid, Integer)
            For Each aRow As DataRow In aggDt.Rows
                Dim tid As Guid
                If Guid.TryParse(aRow("TicketID").ToString(), tid) Then
                    counts(tid) = Convert.ToInt32(aRow("UnreadCount"))
                End If
            Next
            If counts.Count = 0 Then Return

            For Each row As DataRow In dt.Rows
                Dim tid As Guid
                If Guid.TryParse(row("TicketID").ToString(), tid) Then
                    Dim c As Integer
                    If counts.TryGetValue(tid, c) Then row("UnreadCount") = c
                End If
            Next
        Catch ex As Exception
            LogError("AttachUnreadCounts", ex)
            ' UnreadCount stays at 0 — safe display.
        End Try
    End Sub

    ''' <summary>
    ''' Gets ticket details with all related data (uses stored procedure)
    ''' </summary>
    Public Shared Function GetTicketDetails(ticketId As Guid) As DataSet
        Return ExecuteStoredProcedureMultipleResults("sp_GetTicketDetails",
            New SqlParameter("@TicketID", SqlDbType.UniqueIdentifier) With {.Value = ticketId})
    End Function

    ''' <summary>
    ''' Creates a new ticket with company-scoped numbering
    ''' </summary>
    Public Shared Function CreateTicket(subject As String, description As String, projectId As Guid,
                                       statusId As Guid, priorityId As Guid, startDate As DateTime?,
                                       dueDate As DateTime?, createdBy As Guid, assignedTo As Guid?,
                                       Optional projectedHours As Decimal = 0) As Guid
        Try
            ' Get ClientID from ProjectID
            Dim clientIdQuery As String = "SELECT ClientID FROM Projects WHERE ProjectID = @ProjectID"
            Dim clientIdScalar As Object = ExecuteScalar(clientIdQuery, New SqlParameter("@ProjectID", SqlDbType.UniqueIdentifier) With {.Value = projectId})
            If clientIdScalar Is Nothing OrElse IsDBNull(clientIdScalar) Then
                Throw New Exception("Project not found (ID: " & projectId.ToString() & "). Please select a valid project.")
            End If
            Dim clientId As Guid = Guid.Parse(clientIdScalar.ToString())

            ' Generate company-scoped ticket number
            Dim ticketNumber As String = GetNextTicketNumber(clientId)

            ' Insert ticket with ticket number and projected hours - use OUTPUT clause to return the GUID
            Dim query As String = "INSERT INTO Tickets (TicketNumber, Subject, Description, ProjectID, StatusID, PriorityID, StartDate, DueDate, CreatedBy, AssignedTo, ProjectedHours) " &
                                 "OUTPUT INSERTED.TicketID " &
                                 "VALUES (@TicketNumber, @Subject, @Description, @ProjectID, @StatusID, @PriorityID, @StartDate, @DueDate, @CreatedBy, @AssignedTo, @ProjectedHours);"

            Dim parameters As New List(Of SqlParameter) From {
                New SqlParameter("@TicketNumber", ticketNumber),
                New SqlParameter("@Subject", subject),
                New SqlParameter("@Description", If(String.IsNullOrEmpty(description), DBNull.Value, description)),
                New SqlParameter("@ProjectID", SqlDbType.UniqueIdentifier) With {.Value = projectId},
                New SqlParameter("@StatusID", SqlDbType.UniqueIdentifier) With {.Value = statusId},
                New SqlParameter("@PriorityID", SqlDbType.UniqueIdentifier) With {.Value = priorityId},
                New SqlParameter("@StartDate", If(startDate.HasValue, CObj(startDate.Value), DBNull.Value)),
                New SqlParameter("@DueDate", If(dueDate.HasValue, CObj(dueDate.Value), DBNull.Value)),
                New SqlParameter("@CreatedBy", SqlDbType.UniqueIdentifier) With {.Value = createdBy},
                New SqlParameter("@AssignedTo", SqlDbType.UniqueIdentifier) With {.Value = If(assignedTo.HasValue, CObj(assignedTo.Value), DBNull.Value)},
                New SqlParameter("@ProjectedHours", projectedHours)
            }

            Return Guid.Parse(ExecuteScalar(query, parameters.ToArray()).ToString())
        Catch ex As Exception
            Throw New Exception("Error creating ticket: " & ex.Message, ex)
        End Try
    End Function

    ''' <summary>
    ''' Gets all tickets for a company for admin management, with client/project/status/priority/worker info.
    ''' </summary>
    Public Shared Function GetTicketsAdmin(companyId As Integer) As DataTable
        EnsureNameColumns()
        Dim query As String =
            "SELECT t.TicketID, t.TicketNumber, t.Subject, ISNULL(t.Description,'') AS Description, " &
            "    t.IsActive, t.CreatedOn, t.DueDate, " &
            "    s.StatusID, s.Name AS StatusName, " &
            "    pr.PriorityID, pr.Name AS PriorityName, " &
            "    p.ProjectID, p.Name AS ProjectName, " &
            "    cl.ClientID, CAST(cl.ClientID AS VARCHAR(36)) AS ClientIDStr, cl.ClientName, " &
            "    ua.UserID AS AssignedToID, " &
            "    CASE WHEN LTRIM(RTRIM(ISNULL(ua.FirstName,'') + ' ' + ISNULL(ua.LastName,''))) <> '' THEN LTRIM(RTRIM(ISNULL(ua.FirstName,'') + ' ' + ISNULL(ua.LastName,''))) ELSE ISNULL(ua.Username,'') END AS AssignedToName, " &
            "    CASE WHEN LTRIM(RTRIM(ISNULL(uc.FirstName,'') + ' ' + ISNULL(uc.LastName,''))) <> '' THEN LTRIM(RTRIM(ISNULL(uc.FirstName,'') + ' ' + ISNULL(uc.LastName,''))) ELSE uc.Username END AS CreatedByName, " &
            "    (SELECT COUNT(*) FROM TimePunch tp2 WHERE tp2.TicketID = t.TicketID AND tp2.ClockOut IS NOT NULL) AS PunchCount, " &
            "    (SELECT ISNULL(SUM(DATEDIFF(MINUTE, tp3.ClockIn, tp3.ClockOut)),0) " &
            "     FROM TimePunch tp3 WHERE tp3.TicketID = t.TicketID AND tp3.ClockOut IS NOT NULL) AS TotalMinutes " &
            "FROM Tickets t " &
            "INNER JOIN Status   s   ON t.StatusID   = s.StatusID " &
            "INNER JOIN Priority pr  ON t.PriorityID = pr.PriorityID " &
            "INNER JOIN Projects p   ON t.ProjectID  = p.ProjectID " &
            "INNER JOIN Clients  cl  ON p.ClientID   = cl.ClientID " &
            "LEFT  JOIN Users    ua  ON t.AssignedTo = ua.UserID " &
            "LEFT  JOIN Users    uc  ON t.CreatedBy  = uc.UserID " &
            "WHERE cl.CompanyID = @CompanyID AND t.IsActive = 1 AND t.IsArchived = 0 " &
            "ORDER BY t.CreatedOn DESC"
        Return ExecuteDataTable(query, New SqlParameter("@CompanyID", companyId))
    End Function

    ''' <summary>
    ''' Gets all time punch records for a specific ticket (for admin editing).
    ''' </summary>
    Public Shared Function GetTimePunchesForTicket(ticketId As Guid) As DataTable
        Dim query As String =
            "SELECT tp.PunchID AS TimePunchID, tp.TicketID, tp.UserID, u.Username, " &
            "    tp.ClockIn, tp.ClockOut, " &
            "    DATEDIFF(MINUTE, tp.ClockIn, tp.ClockOut) AS DurationMinutes " &
            "FROM TimePunch tp " &
            "INNER JOIN Users u ON tp.UserID = u.UserID " &
            "WHERE tp.TicketID = @TicketID " &
            "ORDER BY tp.ClockIn"
        Return ExecuteDataTable(query, New SqlParameter("@TicketID", SqlDbType.UniqueIdentifier) With {.Value = ticketId})
    End Function

    ''' <summary>
    ''' Batch variant: returns ALL time punches for active, non-archived tickets in the given
    ''' company in a single query. Used by Admin to avoid N+1 lookups when binding the nested
    ''' time-punch repeater on each ticket row.
    ''' </summary>
    Public Shared Function GetAllTimePunchesForCompanyAdmin(companyId As Integer) As DataTable
        Dim query As String =
            "SELECT tp.PunchID AS TimePunchID, tp.TicketID, tp.UserID, u.Username, " &
            "    tp.ClockIn, tp.ClockOut, " &
            "    DATEDIFF(MINUTE, tp.ClockIn, tp.ClockOut) AS DurationMinutes " &
            "FROM TimePunch tp " &
            "INNER JOIN Users    u  ON tp.UserID    = u.UserID " &
            "INNER JOIN Tickets  t  ON tp.TicketID  = t.TicketID " &
            "INNER JOIN Projects p  ON t.ProjectID  = p.ProjectID " &
            "INNER JOIN Clients  c  ON p.ClientID   = c.ClientID " &
            "WHERE c.CompanyID = @CompanyID AND t.IsActive = 1 AND t.IsArchived = 0 " &
            "ORDER BY tp.TicketID, tp.ClockIn"
        Return ExecuteDataTable(query, New SqlParameter("@CompanyID", companyId))
    End Function

    ''' <summary>
    ''' Gets all time punch records for a specific ticket AND user (for self-edit feature).
    ''' </summary>
    Public Shared Function GetUserTimePunchesForTicket(ticketId As Guid, userId As Guid) As DataTable
        Dim query As String =
            "SELECT tp.PunchID AS TimePunchID, tp.TicketID, tp.UserID, " &
            "    tp.ClockIn, tp.ClockOut, " &
            "    CASE WHEN tp.ClockOut IS NULL THEN NULL " &
            "         ELSE DATEDIFF(MINUTE, tp.ClockIn, tp.ClockOut) END AS DurationMinutes " &
            "FROM TimePunch tp " &
            "WHERE tp.TicketID = @TicketID AND tp.UserID = @UserID " &
            "ORDER BY tp.ClockIn DESC"
        Return ExecuteDataTable(query,
            New SqlParameter("@TicketID", SqlDbType.UniqueIdentifier) With {.Value = ticketId},
            New SqlParameter("@UserID",   SqlDbType.UniqueIdentifier) With {.Value = userId})
    End Function

    ''' <summary>
    ''' Updates a time punch only if it belongs to the specified user (prevents editing others' entries).
    ''' </summary>
    Public Shared Function UserUpdateTimePunch(punchId As Guid, userId As Guid,
                                               clockIn As DateTime, clockOut As DateTime?) As Integer
        Dim query As String =
            "UPDATE TimePunch SET ClockIn=@ClockIn, ClockOut=@ClockOut " &
            "WHERE PunchID=@PunchID AND UserID=@UserID"
        Return ExecuteNonQuery(query,
            New SqlParameter("@PunchID",  SqlDbType.UniqueIdentifier) With {.Value = punchId},
            New SqlParameter("@UserID",   SqlDbType.UniqueIdentifier) With {.Value = userId},
            New SqlParameter("@ClockIn",  clockIn),
            New SqlParameter("@ClockOut", If(clockOut.HasValue, CObj(clockOut.Value), DBNull.Value)))
    End Function

    ''' <summary>
    ''' Creates a new time punch for a ticket (admin add-time feature).
    ''' </summary>
    Public Shared Function CreateTimePunch(ticketId As Guid, userId As Guid,
                                           clockIn As DateTime, clockOut As DateTime?) As Guid
        Dim punchId As Guid = Guid.NewGuid()
        Dim query As String =
            "INSERT INTO TimePunch (PunchID, TicketID, UserID, ClockIn, ClockOut) " &
            "VALUES (@PunchID, @TicketID, @UserID, @ClockIn, @ClockOut)"
        ExecuteNonQuery(query,
            New SqlParameter("@PunchID",  SqlDbType.UniqueIdentifier) With {.Value = punchId},
            New SqlParameter("@TicketID", SqlDbType.UniqueIdentifier) With {.Value = ticketId},
            New SqlParameter("@UserID",   SqlDbType.UniqueIdentifier) With {.Value = userId},
            New SqlParameter("@ClockIn",  clockIn),
            New SqlParameter("@ClockOut", SqlDbType.DateTime) With {
                .Value = If(clockOut.HasValue, CObj(clockOut.Value), DBNull.Value)})
        Return punchId
    End Function

    ''' <summary>
    ''' Admin update of a ticket's subject, description, status, priority, and assigned user.
    ''' </summary>
    Public Shared Function AdminUpdateTicket(ticketId As Guid, subject As String, description As String,
                                             statusId As Guid, priorityId As Guid,
                                             assignedTo As Guid?) As Integer
        Dim query As String =
            "UPDATE Tickets SET Subject=@Subject, Description=@Description, " &
            "StatusID=@StatusID, PriorityID=@PriorityID, AssignedTo=@AssignedTo, " &
            "UpdatedOn=GETDATE() WHERE TicketID=@TicketID"
        Return ExecuteNonQuery(query,
            New SqlParameter("@TicketID",    SqlDbType.UniqueIdentifier) With {.Value = ticketId},
            New SqlParameter("@Subject",     subject),
            New SqlParameter("@Description", If(String.IsNullOrEmpty(description), CObj(DBNull.Value), description)),
            New SqlParameter("@StatusID",    SqlDbType.UniqueIdentifier) With {.Value = statusId},
            New SqlParameter("@PriorityID",  SqlDbType.UniqueIdentifier) With {.Value = priorityId},
            New SqlParameter("@AssignedTo",  SqlDbType.UniqueIdentifier) With {.Value = If(assignedTo.HasValue, CObj(assignedTo.Value), DBNull.Value)})
    End Function

    ''' <summary>
    ''' Admin update of a time punch's ClockIn and ClockOut timestamps.
    ''' </summary>
    Public Shared Function AdminUpdateTimePunch(punchId As Guid, clockIn As DateTime, clockOut As DateTime?) As Integer
        Dim query As String =
            "UPDATE TimePunch SET ClockIn=@ClockIn, ClockOut=@ClockOut WHERE PunchID=@PunchID"
        Return ExecuteNonQuery(query,
            New SqlParameter("@PunchID",  SqlDbType.UniqueIdentifier) With {.Value = punchId},
            New SqlParameter("@ClockIn",  clockIn),
            New SqlParameter("@ClockOut", If(clockOut.HasValue, CObj(clockOut.Value), DBNull.Value)))
    End Function

    ''' <summary>
    ''' Deletes a time punch record (admin action).
    ''' </summary>
    Public Shared Function AdminDeleteTimePunch(punchId As Guid) As Integer
        Dim query As String = "DELETE FROM TimePunch WHERE PunchID=@PunchID"
        Return ExecuteNonQuery(query,
            New SqlParameter("@PunchID", SqlDbType.UniqueIdentifier) With {.Value = punchId})
    End Function

    ''' <summary>
    ''' Updates a ticket
    ''' </summary>
    Public Shared Function UpdateTicket(ticketId As Guid, subject As String, description As String,
                                       statusId As Guid, priorityId As Guid, startDate As DateTime?,
                                       dueDate As DateTime?, assignedTo As Guid?) As Integer
        Dim query As String = "UPDATE Tickets SET Subject = @Subject, Description = @Description, " &
                             "StatusID = @StatusID, PriorityID = @PriorityID, StartDate = @StartDate, DueDate = @DueDate, " &
                             "AssignedTo = @AssignedTo, UpdatedOn = GETDATE() WHERE TicketID = @TicketID"

        Return ExecuteNonQuery(query,
            New SqlParameter("@TicketID", SqlDbType.UniqueIdentifier) With {.Value = ticketId},
            New SqlParameter("@Subject", subject),
            New SqlParameter("@Description", If(description, DBNull.Value)),
            New SqlParameter("@StatusID", SqlDbType.UniqueIdentifier) With {.Value = statusId},
            New SqlParameter("@PriorityID", SqlDbType.UniqueIdentifier) With {.Value = priorityId},
            New SqlParameter("@StartDate", If(startDate.HasValue, CObj(startDate.Value), DBNull.Value)),
            New SqlParameter("@DueDate", If(dueDate.HasValue, CObj(dueDate.Value), DBNull.Value)),
            New SqlParameter("@AssignedTo", SqlDbType.UniqueIdentifier) With {.Value = If(assignedTo.HasValue, CObj(assignedTo.Value), DBNull.Value)})
    End Function

#End Region

#Region "Comment Methods"

    ''' <summary>
    ''' Adds a comment to a ticket
    ''' </summary>
    Public Shared Function AddComment(ticketId As Guid, commentText As String, createdBy As Guid,
                                     recipientUserId As Guid?, isPrivate As Boolean,
                                     Optional parentCommentId As Guid? = Nothing) As Guid
        Try
            EnsureParentCommentColumn()
            Dim query As String =
                "INSERT INTO TicketComments " &
                "(TicketID, CommentText, CreatedBy, RecipientUserID, Private, ParentCommentID) " &
                "OUTPUT INSERTED.CommentID " &
                "VALUES (@TicketID, @CommentText, @CreatedBy, @RecipientUserID, @Private, @ParentCommentID);"
            Dim commentId As Guid = Guid.Parse(ExecuteScalar(query,
                New SqlParameter("@TicketID", SqlDbType.UniqueIdentifier) With {.Value = ticketId},
                New SqlParameter("@CommentText", commentText),
                New SqlParameter("@CreatedBy", SqlDbType.UniqueIdentifier) With {.Value = createdBy},
                New SqlParameter("@RecipientUserID", SqlDbType.UniqueIdentifier) With {.Value = If(recipientUserId.HasValue, CObj(recipientUserId.Value), DBNull.Value)},
                New SqlParameter("@Private", isPrivate),
                New SqlParameter("@ParentCommentID", SqlDbType.UniqueIdentifier) With {
                    .Value = If(parentCommentId.HasValue, CObj(parentCommentId.Value), DBNull.Value)
                }).ToString())

            ' Also save to activity stream
            SaveActivityRecord(ticketId, createdBy, "Comment", commentText, commentId, Nothing, isPrivate)

            Return commentId
        Catch ex As Exception
            Throw New Exception("Error adding comment: " & ex.Message, ex)
        End Try
    End Function

#End Region

#Region "Time Tracking Methods"

    ''' <summary>
    ''' Gets active time punch for user on ticket
    ''' </summary>
    Public Shared Function GetActiveTimePunch(ticketId As Guid, userId As Guid) As DataRow
        Dim dt As DataTable = ExecuteStoredProcedure("sp_GetActiveTimePunch",
            New SqlParameter("@TicketID", SqlDbType.UniqueIdentifier) With {.Value = ticketId},
            New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId})
        If dt.Rows.Count > 0 Then
            Return dt.Rows(0)
        End If
        Return Nothing
    End Function

    ''' <summary>
    ''' Clocks in user for ticket
    ''' </summary>
    Public Shared Function ClockIn(ticketId As Guid, userId As Guid) As Guid
        Dim dt As DataTable = ExecuteStoredProcedure("sp_ClockIn",
            New SqlParameter("@TicketID", SqlDbType.UniqueIdentifier) With {.Value = ticketId},
            New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId})
        If dt.Rows.Count > 0 Then
            Return Guid.Parse(dt.Rows(0)("TimePunchID").ToString())
        End If
        Return Guid.Empty
    End Function

    ''' <summary>
    ''' Clocks out user
    ''' </summary>
    Public Shared Function ClockOut(timePunchId As Guid) As Boolean
        Dim dt As DataTable = ExecuteStoredProcedure("sp_ClockOut",
            New SqlParameter("@TimePunchID", SqlDbType.UniqueIdentifier) With {.Value = timePunchId})
        If dt.Rows.Count > 0 Then
            Return Convert.ToInt32(dt.Rows(0)("RowsAffected")) > 0
        End If
        Return False
    End Function

#End Region

#Region "Reports Methods"

    ''' <summary>
    ''' Returns per-worker payroll summary for a date range using inline SQL.
    ''' Works whether or not the PayType/HourlyRate/AnnualSalary columns have been added.
    ''' </summary>
    ' Build a SQL IN condition from a GUID list. GUIDs are safe to embed (no injection risk).
    ' Returns "1=1" when the list is empty (no filter), or "col IN ('g1','g2',...)" when populated.
    Private Shared Function BuildInClause(column As String, guids As List(Of Guid)) As String
        If guids Is Nothing OrElse guids.Count = 0 Then Return "1=1"
        Return column & " IN (" & String.Join(",", guids.Select(Function(g) "'" & g.ToString() & "'")) & ")"
    End Function

    ' Build a SQL IN condition from a string list. Escapes embedded single quotes.
    ' Returns "1=1" when the list is empty (no filter).
    Private Shared Function BuildNameInClause(column As String, names As List(Of String)) As String
        If names Is Nothing OrElse names.Count = 0 Then Return "1=1"
        Return column & " IN (" & String.Join(",", names.Select(Function(n) "'" & n.Replace("'", "''") & "'")) & ")"
    End Function

    ''' <summary>Returns distinct project names across a whole company (for filter dropdown).</summary>
    Public Shared Function GetDistinctProjectNamesByCompany(companyId As Integer) As DataTable
        Dim query As String =
            "SELECT p.Name, c.ClientName FROM Projects p " &
            "INNER JOIN Clients c ON p.ClientID = c.ClientID " &
            "WHERE c.CompanyID = @CompanyID AND p.IsActive = 1 ORDER BY p.Name, c.ClientName"
        Return ExecuteDataTable(query, New SqlParameter("@CompanyID", companyId))
    End Function

    ''' <summary>Returns project names (with client name) for the given client GUIDs (for cascaded filter dropdown).</summary>
    Public Shared Function GetDistinctProjectNamesByClients(clientIds As List(Of Guid)) As DataTable
        If clientIds Is Nothing OrElse clientIds.Count = 0 Then Return New DataTable()
        Dim clientCond As String = BuildInClause("p.ClientID", clientIds)
        Dim query As String =
            "SELECT p.Name, c.ClientName FROM Projects p " &
            "INNER JOIN Clients c ON p.ClientID = c.ClientID " &
            "WHERE p.IsActive = 1 AND " & clientCond & " ORDER BY p.Name, c.ClientName"
        Return ExecuteDataTable(query)
    End Function

    Public Shared Function GetPayrollReport(companyId As Integer, startDate As DateTime, endDate As DateTime,
                                            Optional filterClientIds As List(Of Guid) = Nothing,
                                            Optional filterProjectNames As List(Of String) = Nothing,
                                            Optional filterUserIds As List(Of Guid) = Nothing) As DataTable
        EnsureNameColumns()
        ' Detect whether payroll columns exist yet
        Dim colCheck As String =
            "SELECT COUNT(1) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Users') AND name = 'PayType'"
        Dim hasPayCols As Boolean = Convert.ToInt32(ExecuteScalar(colCheck)) > 0

        Dim payColsSelect As String
        Dim payColsGroup  As String
        Dim roundExpr     As String
        If hasPayCols Then
            payColsSelect = "ISNULL(u.PayType,'Hourly') AS PayType, ISNULL(u.HourlyRate,0) AS HourlyRate, ISNULL(u.AnnualSalary,0) AS AnnualSalary, "
            payColsGroup  = ", u.PayType, u.HourlyRate, u.AnnualSalary"
            roundExpr     = "CASE WHEN ISNULL(u.TimeRoundingMinutes,0)=0 " &
                            "THEN DATEDIFF(MINUTE,tp.ClockIn,tp.ClockOut) " &
                            "ELSE CAST(ROUND(CAST(DATEDIFF(MINUTE,tp.ClockIn,tp.ClockOut) AS FLOAT)/NULLIF(u.TimeRoundingMinutes,0),0)*u.TimeRoundingMinutes AS INT) " &
                            "END"
        Else
            payColsSelect = "'Hourly' AS PayType, 0 AS HourlyRate, 0 AS AnnualSalary, "
            payColsGroup  = ""
            roundExpr     = "DATEDIFF(MINUTE,tp.ClockIn,tp.ClockOut)"
        End If

        ' Build per-dimension IN conditions (1=1 when no filter = include all)
        Dim clientCond  As String = BuildInClause("cl.ClientID",  filterClientIds)
        Dim projectCond As String = BuildNameInClause("pr.Name",  filterProjectNames)
        Dim userCond    As String = BuildInClause("u.UserID",     filterUserIds)

        Dim query As String =
            "SELECT u.UserID, u.Username, " &
            "CASE WHEN LTRIM(RTRIM(ISNULL(u.FirstName,'') + ' ' + ISNULL(u.LastName,''))) <> '' THEN LTRIM(RTRIM(ISNULL(u.FirstName,'') + ' ' + ISNULL(u.LastName,''))) ELSE u.Username END AS FullName, " &
            "u.Role, " & payColsSelect &
            "    COUNT(DISTINCT CASE WHEN " & clientCond & " AND " & projectCond & " THEN tp.TicketID END) AS TicketsWorked, " &
            "    ISNULL(SUM(CASE WHEN " & clientCond & " AND " & projectCond & " THEN " & roundExpr & " END), 0) AS TotalMinutes, " &
            "    COUNT(DISTINCT CASE WHEN s.Name IN ('Completed','Closed') AND t.IsActive = 1 AND t.IsArchived = 0 " &
            "        AND " & clientCond & " AND " & projectCond & " THEN t.TicketID END) AS TicketsResolved " &
            "FROM Users u " &
            "LEFT JOIN TimePunch tp ON u.UserID = tp.UserID " &
            "    AND tp.ClockIn  >= @StartDate AND tp.ClockIn < @EndDate " &
            "    AND tp.ClockOut IS NOT NULL " &
            "LEFT JOIN Tickets t   ON tp.TicketID  = t.TicketID " &
            "LEFT JOIN Status  s   ON t.StatusID   = s.StatusID " &
            "LEFT JOIN Projects pr ON t.ProjectID  = pr.ProjectID " &
            "LEFT JOIN Clients  cl ON pr.ClientID  = cl.ClientID " &
            "WHERE u.CompanyID = @CompanyID AND u.Role IN ('Admin','Worker') " &
            "AND " & userCond & " " &
            "GROUP BY u.UserID, u.Username, u.FirstName, u.LastName, u.Role" & payColsGroup & " " &
            "ORDER BY u.Username"

        Return ExecuteDataTable(query,
            New SqlParameter("@CompanyID", companyId),
            New SqlParameter("@StartDate", startDate),
            New SqlParameter("@EndDate", endDate))
    End Function

    ''' <summary>
    ''' Returns dashboard ticket-status metrics in a single round trip. Each field
    ''' applies the same client/project filters as the rest of the Reports page so
    ''' the numbers stay coherent with the worker / punch tables on the same view.
    '''
    ''' Columns:
    '''   OpenedInRange     — tickets CreatedOn within the date range
    '''   CompletedInRange  — tickets whose status is Completed/Closed AND UpdatedOn
    '''                       falls within the range (proxy for "moved to done in this window")
    '''   ActiveNow         — tickets currently in a non-Completed/non-Closed status
    '''   ClosedNow         — tickets currently in Closed status
    '''   AvgDaysToClose    — avg DATEDIFF(day, CreatedOn, UpdatedOn) over tickets
    '''                       completed in the range; NULL when no completions
    ''' </summary>
    Public Shared Function GetTicketStatusSummary(companyId As Integer, startDate As DateTime, endDate As DateTime,
                                                   Optional filterClientIds As List(Of Guid) = Nothing,
                                                   Optional filterProjectNames As List(Of String) = Nothing) As DataTable
        Dim clientCond  As String = BuildInClause("cl.ClientID", filterClientIds)
        Dim projectCond As String = BuildNameInClause("pr.Name",  filterProjectNames)

        Dim query As String =
            "SELECT " &
            "  COUNT(DISTINCT CASE WHEN t.CreatedOn >= @StartDate AND t.CreatedOn < @EndDate THEN t.TicketID END) AS OpenedInRange, " &
            "  COUNT(DISTINCT CASE WHEN s.Name IN ('Completed', 'Closed') " &
            "                       AND t.UpdatedOn >= @StartDate AND t.UpdatedOn < @EndDate THEN t.TicketID END) AS CompletedInRange, " &
            "  COUNT(DISTINCT CASE WHEN s.Name NOT IN ('Completed', 'Closed') THEN t.TicketID END) AS ActiveNow, " &
            "  COUNT(DISTINCT CASE WHEN s.Name = 'Closed' THEN t.TicketID END) AS ClosedNow, " &
            "  AVG(CAST(CASE WHEN s.Name IN ('Completed', 'Closed') " &
            "                 AND t.UpdatedOn >= @StartDate AND t.UpdatedOn < @EndDate " &
            "                 AND t.UpdatedOn >= t.CreatedOn " &
            "          THEN DATEDIFF(DAY, t.CreatedOn, t.UpdatedOn) END AS FLOAT)) AS AvgDaysToClose " &
            "FROM Tickets t " &
            "INNER JOIN Projects pr ON t.ProjectID = pr.ProjectID " &
            "INNER JOIN Clients  cl ON pr.ClientID = cl.ClientID " &
            "INNER JOIN Status   s  ON t.StatusID  = s.StatusID " &
            "WHERE cl.CompanyID = @CompanyID " &
            "  AND t.IsActive = 1 AND t.IsArchived = 0 " &
            "  AND " & clientCond & " AND " & projectCond

        Return ExecuteDataTable(query,
            New SqlParameter("@CompanyID", companyId),
            New SqlParameter("@StartDate", startDate),
            New SqlParameter("@EndDate", endDate))
    End Function

    ''' <summary>
    ''' Returns ticket rows for the "By Customer" detail report. A ticket qualifies
    ''' when it was created OR last updated within [startDate, endDate). Each row
    ''' carries the metadata the report renders (client, status, dates, assignee,
    ''' total time logged). Output is ordered by ClientName, then by an explicit
    ''' status priority (New / Open / In Progress first, Completed and Closed last),
    ''' then newest first within each bucket.
    ''' </summary>
    Public Shared Function GetCustomerReportTickets(companyId As Integer, startDate As DateTime, endDate As DateTime,
                                                     Optional filterClientIds As List(Of Guid) = Nothing) As DataTable
        Dim clientCond As String = BuildInClause("c.ClientID", filterClientIds)

        Dim query As String =
            "SELECT t.TicketID, t.TicketNumber, t.Subject, t.Description, " &
            "       t.CreatedOn, t.UpdatedOn, t.StartDate, t.DueDate, " &
            "       c.ClientID, c.ClientName, " &
            "       p.Name AS ProjectName, " &
            "       s.Name AS StatusName, " &
            "       pr.Name AS PriorityName, " &
            "       u.Username AS AssignedToUsername, " &
            "       CASE WHEN LTRIM(RTRIM(ISNULL(u.FirstName,'') + ' ' + ISNULL(u.LastName,''))) <> '' " &
            "            THEN LTRIM(RTRIM(ISNULL(u.FirstName,'') + ' ' + ISNULL(u.LastName,''))) ELSE u.Username END AS AssignedToFullName, " &
            "       ISNULL((SELECT SUM(DATEDIFF(MINUTE, tp.ClockIn, tp.ClockOut)) " &
            "               FROM TimePunch tp WHERE tp.TicketID = t.TicketID AND tp.ClockOut IS NOT NULL), 0) AS TotalMinutes, " &
            "       CASE s.Name " &
            "           WHEN 'New'         THEN 1 " &
            "           WHEN 'Open'        THEN 2 " &
            "           WHEN 'In Progress' THEN 3 " &
            "           WHEN 'Completed'   THEN 8 " &
            "           WHEN 'Closed'      THEN 9 " &
            "           ELSE 5 " &
            "       END AS StatusOrder " &
            "FROM Tickets t " &
            "INNER JOIN Projects p ON t.ProjectID = p.ProjectID " &
            "INNER JOIN Clients  c ON p.ClientID  = c.ClientID " &
            "INNER JOIN Status   s ON t.StatusID  = s.StatusID " &
            "INNER JOIN Priority pr ON t.PriorityID = pr.PriorityID " &
            "LEFT JOIN  Users    u ON t.AssignedTo = u.UserID " &
            "WHERE c.CompanyID = @CompanyID " &
            "  AND t.IsActive = 1 AND t.IsArchived = 0 " &
            "  AND ((t.CreatedOn >= @StartDate AND t.CreatedOn < @EndDate) " &
            "       OR (t.UpdatedOn >= @StartDate AND t.UpdatedOn < @EndDate)) " &
            "  AND " & clientCond & " " &
            "ORDER BY c.ClientName, StatusOrder, t.CreatedOn DESC"

        Return ExecuteDataTable(query,
            New SqlParameter("@CompanyID", companyId),
            New SqlParameter("@StartDate", startDate),
            New SqlParameter("@EndDate", endDate))
    End Function

    ''' <summary>
    ''' Returns ticket rows for the "By Worker" detail report. One row per
    ''' (worker, ticket) combo where the worker has at least one closed punch
    ''' on that ticket within the date range. WorkerMinutes is the SUM of those
    ''' punches' durations — i.e. the time THIS worker spent on THIS ticket in
    ''' the window. Ordered by worker, then status priority, then newest first.
    ''' </summary>
    Public Shared Function GetWorkerReportTickets(companyId As Integer, startDate As DateTime, endDate As DateTime,
                                                   Optional filterWorkerIds As List(Of Guid) = Nothing) As DataTable
        Dim workerCond As String = BuildInClause("u.UserID", filterWorkerIds)

        Dim query As String =
            "SELECT u.UserID, u.Username, " &
            "       CASE WHEN LTRIM(RTRIM(ISNULL(u.FirstName,'') + ' ' + ISNULL(u.LastName,''))) <> '' " &
            "            THEN LTRIM(RTRIM(ISNULL(u.FirstName,'') + ' ' + ISNULL(u.LastName,''))) ELSE u.Username END AS FullName, " &
            "       u.Role, " &
            "       t.TicketID, t.TicketNumber, t.Subject, t.CreatedOn, t.UpdatedOn, " &
            "       c.ClientName, p.Name AS ProjectName, s.Name AS StatusName, " &
            "       SUM(DATEDIFF(MINUTE, tp.ClockIn, tp.ClockOut)) AS WorkerMinutes, " &
            "       CASE s.Name " &
            "           WHEN 'New'         THEN 1 " &
            "           WHEN 'Open'        THEN 2 " &
            "           WHEN 'In Progress' THEN 3 " &
            "           WHEN 'Completed'   THEN 8 " &
            "           WHEN 'Closed'      THEN 9 " &
            "           ELSE 5 " &
            "       END AS StatusOrder " &
            "FROM TimePunch tp " &
            "INNER JOIN Users    u  ON tp.UserID   = u.UserID " &
            "INNER JOIN Tickets  t  ON tp.TicketID  = t.TicketID " &
            "INNER JOIN Projects p  ON t.ProjectID  = p.ProjectID " &
            "INNER JOIN Clients  c  ON p.ClientID   = c.ClientID " &
            "INNER JOIN Status   s  ON t.StatusID   = s.StatusID " &
            "WHERE u.CompanyID = @CompanyID " &
            "  AND tp.ClockIn  >= @StartDate AND tp.ClockIn < @EndDate " &
            "  AND tp.ClockOut IS NOT NULL " &
            "  AND t.IsActive  = 1 AND t.IsArchived = 0 " &
            "  AND " & workerCond & " " &
            "GROUP BY u.UserID, u.Username, u.FirstName, u.LastName, u.Role, " &
            "         t.TicketID, t.TicketNumber, t.Subject, t.CreatedOn, t.UpdatedOn, " &
            "         c.ClientName, p.Name, s.Name " &
            "ORDER BY FullName, StatusOrder, t.CreatedOn DESC"

        Return ExecuteDataTable(query,
            New SqlParameter("@CompanyID", companyId),
            New SqlParameter("@StartDate", startDate),
            New SqlParameter("@EndDate", endDate))
    End Function

    ''' <summary>
    ''' Returns individual time-punch records for a company and date range using inline SQL.
    ''' </summary>
    Public Shared Function GetTimePunchDetail(companyId As Integer, startDate As DateTime, endDate As DateTime,
                                              Optional filterClientIds As List(Of Guid) = Nothing,
                                              Optional filterProjectNames As List(Of String) = Nothing,
                                              Optional filterUserIds As List(Of Guid) = Nothing) As DataTable
        EnsureNameColumns()
        EnsurePayrollColumns()

        Dim clientCond  As String = BuildInClause("cl.ClientID", filterClientIds)
        Dim projectCond As String = BuildNameInClause("p.Name",  filterProjectNames)
        Dim userCond    As String = BuildInClause("u.UserID",    filterUserIds)

        Dim query As String =
            "SELECT tp.PunchID AS TimePunchID, u.UserID, u.Username, " &
            "CASE WHEN LTRIM(RTRIM(ISNULL(u.FirstName,'') + ' ' + ISNULL(u.LastName,''))) <> '' THEN LTRIM(RTRIM(ISNULL(u.FirstName,'') + ' ' + ISNULL(u.LastName,''))) ELSE u.Username END AS FullName, " &
            "    t.TicketID, t.Subject AS TicketSubject, " &
            "    cl.ClientName, p.Name AS ProjectName, " &
            "    tp.ClockIn, tp.ClockOut, " &
            "    CASE WHEN ISNULL(u.TimeRoundingMinutes,0)=0 " &
            "    THEN DATEDIFF(MINUTE,tp.ClockIn,tp.ClockOut) " &
            "    ELSE CAST(ROUND(CAST(DATEDIFF(MINUTE,tp.ClockIn,tp.ClockOut) AS FLOAT)/NULLIF(u.TimeRoundingMinutes,0),0)*u.TimeRoundingMinutes AS INT) " &
            "    END AS DurationMinutes " &
            "FROM TimePunch tp " &
            "INNER JOIN Users   u  ON tp.UserID   = u.UserID " &
            "INNER JOIN Tickets t  ON tp.TicketID  = t.TicketID " &
            "LEFT  JOIN Projects p  ON t.ProjectID  = p.ProjectID " &
            "LEFT  JOIN Clients  cl ON p.ClientID   = cl.ClientID " &
            "WHERE u.CompanyID = @CompanyID " &
            "    AND tp.ClockIn  >= @StartDate AND tp.ClockIn < @EndDate " &
            "    AND tp.ClockOut IS NOT NULL " &
            "    AND " & clientCond &
            "    AND " & projectCond &
            "    AND " & userCond &
            " ORDER BY tp.ClockIn DESC"

        Return ExecuteDataTable(query,
            New SqlParameter("@CompanyID", companyId),
            New SqlParameter("@StartDate", startDate),
            New SqlParameter("@EndDate", endDate))
    End Function

#End Region

#Region "Collaborator Methods"

    ''' <summary>
    ''' Adds a collaborator to a ticket
    ''' </summary>
    Public Shared Function AddCollaborator(ticketId As Guid, userId As Guid, addedBy As Guid) As Boolean
        Try
            Dim query As String = "INSERT INTO Collaborators (TicketID, UserID, AddedBy) VALUES (@TicketID, @UserID, @AddedBy)"
            ExecuteNonQuery(query,
                New SqlParameter("@TicketID", SqlDbType.UniqueIdentifier) With {.Value = ticketId},
                New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId},
                New SqlParameter("@AddedBy", SqlDbType.UniqueIdentifier) With {.Value = addedBy})
            Return True
        Catch ex As SqlException
            ' Handle duplicate key (already a collaborator)
            If ex.Number = 2627 Then
                Return False
            End If
            Throw
        End Try
    End Function

    ''' <summary>
    ''' Removes a collaborator from a ticket
    ''' </summary>
    Public Shared Function RemoveCollaborator(ticketId As Guid, userId As Guid) As Boolean
        Dim query As String = "DELETE FROM Collaborators WHERE TicketID = @TicketID AND UserID = @UserID"
        Return ExecuteNonQuery(query,
            New SqlParameter("@TicketID", SqlDbType.UniqueIdentifier) With {.Value = ticketId},
            New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId}) > 0
    End Function

#End Region

#Region "File Methods"

    ''' <summary>
    ''' Inserts a row into TicketFiles with the file bytes stored in the FileData column.
    ''' Pass ticketId = Nothing for the pre-upload flow (TicketID NULL until the ticket is saved).
    ''' </summary>
    Public Shared Function AddTicketFile(ticketId As Guid?, fileType As String,
                                        originalFileName As String, fileSize As Long,
                                        mimeType As String, fileData As Byte(),
                                        uploadedBy As Guid) As Guid
        Dim query As String = "INSERT INTO TicketFiles (TicketID, FileType, OriginalFileName, FileSize, MimeType, FileData, UploadedBy) " &
                             "OUTPUT INSERTED.FileID " &
                             "VALUES (@TicketID, @FileType, @OriginalFileName, @FileSize, @MimeType, @FileData, @UploadedBy);"

        Dim ticketParam As New SqlParameter("@TicketID", SqlDbType.UniqueIdentifier)
        ticketParam.Value = If(ticketId.HasValue, CObj(ticketId.Value), CObj(DBNull.Value))

        Dim mimeParam As New SqlParameter("@MimeType", SqlDbType.NVarChar, 200)
        mimeParam.Value = If(String.IsNullOrEmpty(mimeType), CObj(DBNull.Value), CObj(mimeType))

        Dim dataParam As New SqlParameter("@FileData", SqlDbType.VarBinary, -1)
        dataParam.Value = fileData

        Return Guid.Parse(ExecuteScalar(query,
            ticketParam,
            New SqlParameter("@FileType", fileType),
            New SqlParameter("@OriginalFileName", originalFileName),
            New SqlParameter("@FileSize", fileSize),
            mimeParam,
            dataParam,
            New SqlParameter("@UploadedBy", SqlDbType.UniqueIdentifier) With {.Value = uploadedBy}).ToString())
    End Function

    ''' <summary>
    ''' Reassigns previously-uploaded files (TicketID = NULL) to a newly-created ticket.
    ''' Used by the pre-upload flow on the new-ticket form: files are uploaded via AJAX
    ''' before the ticket exists, then claimed here once the ticket row is saved.
    ''' </summary>
    Public Shared Function ClaimUploadedFiles(fileIds As IEnumerable(Of Guid), ticketId As Guid) As Integer
        If fileIds Is Nothing Then Return 0
        Dim count As Integer = 0
        For Each fid As Guid In fileIds
            If fid = Guid.Empty Then Continue For
            count += ExecuteNonQuery(
                "UPDATE TicketFiles SET TicketID = @TicketID WHERE FileID = @FileID AND TicketID IS NULL",
                New SqlParameter("@TicketID", SqlDbType.UniqueIdentifier) With {.Value = ticketId},
                New SqlParameter("@FileID", SqlDbType.UniqueIdentifier) With {.Value = fid})
        Next
        Return count
    End Function

#End Region

#Region "Error Logging"

    ' ── SQL-backed logging ──────────────────────────────────────────────
    ' AppLogs holds error + perf entries (previously written to App_Data/Logs/*.txt).
    ' EnsureAppLogsTable runs once per AppDomain on first use; PurgeOldLogs runs at
    ' most once per 24h to trim entries older than 30 days. File-based fallback
    ' kicks in only when the SQL write itself throws (e.g. DB down at the moment
    ' an error is being logged), so we never lose visibility of failures.

    Private Const AppLogsRetentionDays As Integer = 30
    Private Const PerfLogThresholdMs As Long = 200
    Private Shared _appLogsTableEnsured As Boolean = False
    Private Shared _lastPurgeUtc As DateTime = DateTime.MinValue
    Private Shared ReadOnly _appLogsLock As New Object()

    ''' <summary>
    ''' Creates dbo.AppLogs and its indexes if missing. Idempotent; cheap after the
    ''' first call (we cache the "ensured" flag in a static field).
    ''' </summary>
    Public Shared Sub EnsureAppLogsTable()
        If _appLogsTableEnsured Then Return
        SyncLock _appLogsLock
            If _appLogsTableEnsured Then Return
            Try
                Dim ddl As String =
                    "IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AppLogs') " &
                    "BEGIN " &
                    "  CREATE TABLE dbo.AppLogs (" &
                    "    LogID       BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY, " &
                    "    CreatedOn   DATETIME       NOT NULL DEFAULT GETDATE(), " &
                    "    Category    NVARCHAR(60)   NOT NULL, " &
                    "    Method      NVARCHAR(200)  NULL, " &
                    "    Message     NVARCHAR(MAX)  NULL, " &
                    "    StackTrace  NVARCHAR(MAX)  NULL, " &
                    "    DurationMs  INT            NULL, " &
                    "    RowsAffected INT           NULL, " &
                    "    RequestPath NVARCHAR(500)  NULL, " &
                    "    UserId      UNIQUEIDENTIFIER NULL); " &
                    "  CREATE NONCLUSTERED INDEX IX_AppLogs_CreatedOn ON dbo.AppLogs(CreatedOn DESC); " &
                    "  CREATE NONCLUSTERED INDEX IX_AppLogs_Category_CreatedOn ON dbo.AppLogs(Category, CreatedOn DESC); " &
                    "END"
                Using conn As SqlConnection = GetConnection()
                    Using cmd As New SqlCommand(ddl, conn)
                        conn.Open()
                        cmd.ExecuteNonQuery()
                    End Using
                End Using
                _appLogsTableEnsured = True
            Catch
                ' Leave the flag false so we retry on next call. Don't throw —
                ' callers are LogError / LogPerf, which must never fail loudly.
            End Try
        End SyncLock
    End Sub

    ''' <summary>
    ''' Deletes AppLogs rows older than <paramref name="daysToKeep"/>. Called from
    ''' MaybePurgeOldLogs at most once per 24 hours.
    ''' </summary>
    Public Shared Function PurgeOldLogs(Optional daysToKeep As Integer = AppLogsRetentionDays) As Integer
        Try
            EnsureAppLogsTable()
            Using conn As SqlConnection = GetConnection()
                Using cmd As New SqlCommand("DELETE FROM dbo.AppLogs WHERE CreatedOn < DATEADD(DAY, -@Days, GETDATE())", conn)
                    cmd.Parameters.AddWithValue("@Days", daysToKeep)
                    cmd.CommandTimeout = 60
                    conn.Open()
                    Return cmd.ExecuteNonQuery()
                End Using
            End Using
        Catch
            Return 0
        End Try
    End Function

    ''' <summary>
    ''' Calls PurgeOldLogs at most once per 24h. Cheap to call from any code path;
    ''' the actual DELETE happens at most once a day per AppDomain.
    ''' </summary>
    Public Shared Sub MaybePurgeOldLogs()
        Dim now As DateTime = DateTime.UtcNow
        If (now - _lastPurgeUtc).TotalHours < 24 Then Return
        SyncLock _appLogsLock
            If (now - _lastPurgeUtc).TotalHours < 24 Then Return
            _lastPurgeUtc = now
        End SyncLock
        PurgeOldLogs(AppLogsRetentionDays)
    End Sub

    ''' <summary>
    ''' Central SQL log writer. Used by LogError, LogPerf, and any caller that
    ''' wants to drop a row into dbo.AppLogs. Failures fall through to a file
    ''' fallback so we don't lose log entries when the DB is unreachable.
    ''' </summary>
    Public Shared Sub WriteAppLog(category As String, method As String, message As String,
                                   Optional stackTrace As String = Nothing,
                                   Optional durationMs As Integer? = Nothing,
                                   Optional rowsAffected As Integer? = Nothing)
        Try
            EnsureAppLogsTable()
            Dim path As String = ""
            Dim uid As Object = DBNull.Value
            Try
                Dim ctx = System.Web.HttpContext.Current
                If ctx IsNot Nothing AndAlso ctx.Request IsNot Nothing Then
                    path = If(ctx.Request.Path, "")
                End If
            Catch
            End Try

            Using conn As SqlConnection = GetConnection()
                Using cmd As New SqlCommand(
                    "INSERT INTO dbo.AppLogs (Category, Method, Message, StackTrace, DurationMs, RowsAffected, RequestPath, UserId) " &
                    "VALUES (@Category, @Method, @Message, @StackTrace, @DurationMs, @RowsAffected, @RequestPath, @UserId)", conn)
                    cmd.Parameters.Add(New SqlParameter("@Category", SqlDbType.NVarChar, 60) With {.Value = If(category, "Unknown")})
                    cmd.Parameters.Add(New SqlParameter("@Method", SqlDbType.NVarChar, 200) With {.Value = If(method, CObj(DBNull.Value))})
                    cmd.Parameters.Add(New SqlParameter("@Message", SqlDbType.NVarChar, -1) With {.Value = If(message, CObj(DBNull.Value))})
                    cmd.Parameters.Add(New SqlParameter("@StackTrace", SqlDbType.NVarChar, -1) With {.Value = If(stackTrace, CObj(DBNull.Value))})
                    cmd.Parameters.Add(New SqlParameter("@DurationMs", SqlDbType.Int) With {.Value = If(durationMs.HasValue, CObj(durationMs.Value), DBNull.Value)})
                    cmd.Parameters.Add(New SqlParameter("@RowsAffected", SqlDbType.Int) With {.Value = If(rowsAffected.HasValue, CObj(rowsAffected.Value), DBNull.Value)})
                    cmd.Parameters.Add(New SqlParameter("@RequestPath", SqlDbType.NVarChar, 500) With {.Value = If(String.IsNullOrEmpty(path), CObj(DBNull.Value), CObj(path))})
                    cmd.Parameters.Add(New SqlParameter("@UserId", SqlDbType.UniqueIdentifier) With {.Value = uid})
                    cmd.CommandTimeout = 5  ' Keep logging fast; never block real work.
                    conn.Open()
                    cmd.ExecuteNonQuery()
                End Using
            End Using
        Catch
            ' SQL write failed — fall back to file so the entry isn't lost. This is
            ' the rare path: DB down, schema mismatch, transient connectivity issue.
            Try
                WriteAppLogFileFallback(category, method, message, stackTrace, durationMs, rowsAffected)
            Catch
            End Try
        End Try
    End Sub

    Private Shared Sub WriteAppLogFileFallback(category As String, method As String, message As String,
                                                stackTrace As String, durationMs As Integer?, rowsAffected As Integer?)
        Dim ctx = System.Web.HttpContext.Current
        If ctx Is Nothing Then Return
        Dim logPath As String = ctx.Server.MapPath("~/App_Data/Logs/")
        If Not System.IO.Directory.Exists(logPath) Then System.IO.Directory.CreateDirectory(logPath)
        Dim logFile As String = Path.Combine(logPath, String.Format("AppLogsFallback_{0:yyyyMMdd}.txt", DateTime.Now))
        Dim durPart As String = If(durationMs.HasValue, " " & durationMs.Value & "ms", "")
        Dim rowsPart As String = If(rowsAffected.HasValue, " rows=" & rowsAffected.Value, "")
        Dim line As String = String.Format("[{0:yyyy-MM-dd HH:mm:ss.fff}] {1}{2}{3} | {4} | {5}" & vbCrLf,
                                            DateTime.Now, category, durPart, rowsPart, If(method, ""), If(message, ""))
        If Not String.IsNullOrEmpty(stackTrace) Then line &= stackTrace & vbCrLf
        System.IO.File.AppendAllText(logFile, line)
    End Sub

    ''' <summary>
    ''' Logs errors. Writes to dbo.AppLogs (category=Error). Falls back to file on
    ''' SQL failure so we never silently lose an error.
    ''' </summary>
    Private Shared Sub LogError(methodName As String, ex As Exception)
        WriteAppLog("Error", methodName, ex.Message, ex.StackTrace)
        MaybePurgeOldLogs()
    End Sub

    ''' <summary>
    ''' Slow-query timing log. Writes to dbo.AppLogs (category=Perf) only when the
    ''' elapsed time crosses the threshold so the table stays useful as a hotspot
    ''' map rather than every-query noise. Triggered from a Finally block in each
    ''' Execute* method, so errors don't suppress the timing.
    ''' </summary>
    Private Shared Sub LogPerf(callerName As String, elapsedMs As Long, queryOrProc As String, Optional rowCount As Integer = -1)
        If elapsedMs < PerfLogThresholdMs Then Return
        Dim qSnippet As String = If(queryOrProc, "")
        qSnippet = System.Text.RegularExpressions.Regex.Replace(qSnippet, "\s+", " ").Trim()
        If qSnippet.Length > 1000 Then qSnippet = qSnippet.Substring(0, 1000) & "..."
        Dim rowsParam As Integer? = If(rowCount >= 0, CType(rowCount, Integer?), Nothing)
        WriteAppLog("Perf", callerName, qSnippet, Nothing, CType(elapsedMs, Integer?), rowsParam)
    End Sub

    ''' <summary>
    ''' Saves a chat message for a ticket
    ''' </summary>
    Public Shared Function SaveTicketChatMessage(ticketId As Guid, userId As Guid, message As String) As Guid
        Try
            Dim query As String = "INSERT INTO ChatMessages (TicketID, UserID, Message, Timestamp) OUTPUT INSERTED.MessageID VALUES (@TicketID, @UserID, @Message, GETDATE());"
            Return Guid.Parse(ExecuteScalar(query,
                New SqlParameter("@TicketID", SqlDbType.UniqueIdentifier) With {.Value = ticketId},
                New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId},
                New SqlParameter("@Message", message)).ToString())
        Catch ex As Exception
            Throw New Exception("Error saving chat message: " & ex.Message, ex)
        End Try
    End Function

    ''' <summary>
    ''' Gets chat messages for a ticket
    ''' </summary>
    Public Shared Function GetTicketChatMessages(ticketId As Guid) As DataTable
        Try
            Dim query As String = "SELECT cm.MessageID, cm.TicketID, cm.UserID, cm.Message, cm.Timestamp, u.Username AS UserName " & _
                                 "FROM ChatMessages cm " & _
                                 "INNER JOIN Users u ON cm.UserID = u.UserID " & _
                                 "WHERE cm.TicketID = @TicketID " & _
                                 "ORDER BY cm.Timestamp ASC"
            Return ExecuteDataTable(query, New SqlParameter("@TicketID", SqlDbType.UniqueIdentifier) With {.Value = ticketId})
        Catch ex As Exception
            Throw New Exception("Error getting chat messages: " & ex.Message, ex)
        End Try
    End Function

#End Region

#Region "New Features - Company-Scoped Ticketing, Global Search, References, Activity Stream"

    ''' <summary>
    ''' Gets the next ticket number for a company (company-scoped numbering)
    ''' </summary>
    Public Shared Function GetNextTicketNumber(clientId As Guid) As String
        Try
            Dim ticketNumber As String = ""
            Using conn As SqlConnection = GetConnection()
                conn.Open()
                Using cmd As New SqlCommand("sp_GetNextTicketNumber", conn)
                    cmd.CommandType = CommandType.StoredProcedure
                    cmd.Parameters.Add(New SqlParameter("@ClientID", SqlDbType.UniqueIdentifier) With {.Value = clientId})

                    Dim outputParam As New SqlParameter("@NextTicketNumber", SqlDbType.NVarChar, 50)
                    outputParam.Direction = ParameterDirection.Output
                    cmd.Parameters.Add(outputParam)

                    cmd.ExecuteNonQuery()
                    ticketNumber = outputParam.Value.ToString()
                End Using
            End Using
            Return ticketNumber
        Catch ex As Exception
            Throw New Exception("Error getting next ticket number: " & ex.Message, ex)
        End Try
    End Function

    ''' <summary>
    ''' Performs global search across tickets for a specific company
    ''' </summary>
    Public Shared Function GlobalSearch(searchTerm As String, companyId As Integer) As DataTable
        EnsureNameColumns()
        Try
            Dim query As String =
                "SELECT DISTINCT t.TicketID, t.TicketNumber, t.Subject, t.Description, " &
                "s.Name AS StatusName, s.StatusID, p.Name AS PriorityName, " &
                "cl.ClientName, proj.Name AS ProjectName, " &
                "u.Username AS AssignedToUsername, " &
                "CASE WHEN LTRIM(RTRIM(ISNULL(u.FirstName,'') + ' ' + ISNULL(u.LastName,''))) <> '' " &
                "THEN LTRIM(RTRIM(ISNULL(u.FirstName,'') + ' ' + ISNULL(u.LastName,''))) ELSE u.Username END AS AssignedToFullName, " &
                "t.CreatedOn, t.StartDate, t.DueDate " &
                "FROM dbo.Tickets t " &
                "INNER JOIN dbo.Projects proj ON t.ProjectID = proj.ProjectID " &
                "INNER JOIN dbo.Clients cl ON proj.ClientID = cl.ClientID " &
                "INNER JOIN dbo.Status s ON t.StatusID = s.StatusID " &
                "INNER JOIN dbo.Priority p ON t.PriorityID = p.PriorityID " &
                "LEFT JOIN dbo.Users u ON t.AssignedTo = u.UserID " &
                "WHERE t.IsActive = 1 AND t.IsArchived = 0 AND cl.CompanyID = @CompanyID " &
                "AND (CAST(t.TicketNumber AS NVARCHAR) LIKE '%' + @SearchTerm + '%' " &
                "OR t.Subject LIKE '%' + @SearchTerm + '%' " &
                "OR t.Description LIKE '%' + @SearchTerm + '%' " &
                "OR cl.ClientName LIKE '%' + @SearchTerm + '%' " &
                "OR proj.Name LIKE '%' + @SearchTerm + '%' " &
                "OR u.Username LIKE '%' + @SearchTerm + '%') " &
                "ORDER BY t.CreatedOn DESC"
            Return ExecuteDataTable(query,
                New SqlParameter("@SearchTerm", searchTerm),
                New SqlParameter("@CompanyID", companyId))
        Catch ex As Exception
            Throw New Exception("Error performing global search: " & ex.Message, ex)
        End Try
    End Function

    ''' <summary>
    ''' Gets collaborators for a client with their open ticket counts
    ''' </summary>
    Public Shared Function GetCollaboratorsByClient(clientId As Guid) As DataTable
        Try
            Using conn As SqlConnection = GetConnection()
                conn.Open()
                Using cmd As New SqlCommand("sp_GetCollaboratorsByClient", conn)
                    cmd.CommandType = CommandType.StoredProcedure
                    cmd.Parameters.Add(New SqlParameter("@ClientID", SqlDbType.UniqueIdentifier) With {.Value = clientId})

                    Using adapter As New SqlDataAdapter(cmd)
                        Dim dt As New DataTable()
                        adapter.Fill(dt)
                        Return dt
                    End Using
                End Using
            End Using
        Catch ex As Exception
            Throw New Exception("Error getting collaborators: " & ex.Message, ex)
        End Try
    End Function

    ''' <summary>
    ''' Gets ticket references (linked tickets) — bidirectional.
    ''' Returns all tickets linked to @TicketID regardless of which side created the link.
    ''' </summary>
    Public Shared Function GetTicketReferences(ticketId As Guid) As DataTable
        ' Bidirectional query: union forward references (this ticket → other)
        ' with reverse references (other ticket → this ticket).
        Dim query As String =
            "SELECT tr.ReferenceID, tr.ReferencedTicketID, tr.Notes, tr.CreatedOn, " &
            "       t.TicketNumber, t.Subject, s.Name AS Status, s.Name AS StatusName, " &
            "       pr.Name AS PriorityName, u.Username AS CreatedBy " &
            "FROM TicketReferences tr " &
            "INNER JOIN Tickets  t  ON tr.ReferencedTicketID = t.TicketID " &
            "INNER JOIN Status   s  ON t.StatusID  = s.StatusID " &
            "INNER JOIN Priority pr ON t.PriorityID = pr.PriorityID " &
            "INNER JOIN Users    u  ON tr.CreatedBy = u.UserID " &
            "WHERE tr.TicketID = @TicketID AND t.IsArchived = 0 " &
            "UNION ALL " &
            "SELECT tr.ReferenceID, tr.TicketID AS ReferencedTicketID, tr.Notes, tr.CreatedOn, " &
            "       t.TicketNumber, t.Subject, s.Name AS Status, s.Name AS StatusName, " &
            "       pr.Name AS PriorityName, u.Username AS CreatedBy " &
            "FROM TicketReferences tr " &
            "INNER JOIN Tickets  t  ON tr.TicketID  = t.TicketID " &
            "INNER JOIN Status   s  ON t.StatusID   = s.StatusID " &
            "INNER JOIN Priority pr ON t.PriorityID = pr.PriorityID " &
            "INNER JOIN Users    u  ON tr.CreatedBy = u.UserID " &
            "WHERE tr.ReferencedTicketID = @TicketID AND t.IsArchived = 0 " &
            "ORDER BY CreatedOn DESC"
        Return ExecuteDataTable(query,
            New SqlParameter("@TicketID", SqlDbType.UniqueIdentifier) With {.Value = ticketId})
    End Function

    ''' <summary>
    ''' Adds a ticket reference (links two tickets)
    ''' </summary>
    Public Shared Function AddTicketReference(ticketId As Guid, referencedTicketId As Guid, userId As Guid, Optional notes As String = "") As Guid
        Try
            ' If the reference already exists, return its ID (idempotent)
            Dim query As String =
                "IF NOT EXISTS (SELECT 1 FROM TicketReferences WHERE TicketID = @TicketID AND ReferencedTicketID = @ReferencedTicketID) " &
                "    INSERT INTO TicketReferences (TicketID, ReferencedTicketID, CreatedBy, Notes) " &
                "    OUTPUT INSERTED.ReferenceID " &
                "    VALUES (@TicketID, @ReferencedTicketID, @CreatedBy, @Notes) " &
                "ELSE " &
                "    SELECT ReferenceID FROM TicketReferences WHERE TicketID = @TicketID AND ReferencedTicketID = @ReferencedTicketID;"
            Return Guid.Parse(ExecuteScalar(query,
                New SqlParameter("@TicketID", SqlDbType.UniqueIdentifier) With {.Value = ticketId},
                New SqlParameter("@ReferencedTicketID", SqlDbType.UniqueIdentifier) With {.Value = referencedTicketId},
                New SqlParameter("@CreatedBy", SqlDbType.UniqueIdentifier) With {.Value = userId},
                New SqlParameter("@Notes", If(String.IsNullOrEmpty(notes), DBNull.Value, notes))).ToString())
        Catch ex As Exception
            Throw New Exception("Error adding ticket reference: " & ex.Message, ex)
        End Try
    End Function

    ''' <summary>
    ''' Gets unified activity stream for a ticket (comments + files)
    ''' </summary>
    Public Shared Function GetTicketActivity(ticketId As Guid) As DataTable
        EnsureNameColumns()
        ' Inline SQL so we can include ta.UserID (needed for edit/delete ownership checks)
        Dim query As String =
            "SELECT ta.ActivityID, ta.TicketID, ta.ActivityType, ta.ActivityText, " &
            "ta.CommentID, tc.ParentCommentID, ta.FileID, ta.CreatedOn, ta.IsPrivate, " &
            "ta.UserID, " &
            "CASE WHEN LTRIM(RTRIM(ISNULL(u.FirstName,'') + ' ' + ISNULL(u.LastName,''))) <> '' THEN LTRIM(RTRIM(ISNULL(u.FirstName,'') + ' ' + ISNULL(u.LastName,''))) ELSE u.Username END AS UserName " &
            "FROM TicketActivity ta " &
            "INNER JOIN Users u ON ta.UserID = u.UserID " &
            "LEFT JOIN TicketComments tc ON ta.CommentID = tc.CommentID " &
            "WHERE ta.TicketID = @TicketID " &
            "ORDER BY ta.CreatedOn ASC"
        Return ExecuteDataTable(query,
            New SqlParameter("@TicketID", SqlDbType.UniqueIdentifier) With {.Value = ticketId})
    End Function

    ''' <summary>
    ''' Updates the text of a comment. Only succeeds if the userId matches the comment creator.
    ''' </summary>
    Public Shared Sub UpdateComment(commentId As Guid, newText As String, userId As Guid)
        ExecuteNonQuery(
            "UPDATE TicketComments SET CommentText = @Text WHERE CommentID = @ID AND CreatedBy = @UserID",
            New SqlParameter("@Text", newText),
            New SqlParameter("@ID", SqlDbType.UniqueIdentifier) With {.Value = commentId},
            New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId})
        ExecuteNonQuery(
            "UPDATE TicketActivity SET ActivityText = @Text WHERE CommentID = @ID AND UserID = @UserID",
            New SqlParameter("@Text", newText),
            New SqlParameter("@ID", SqlDbType.UniqueIdentifier) With {.Value = commentId},
            New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId})
    End Sub

    ''' <summary>
    ''' <summary>
    ''' Returns the raw CommentText for a comment (used to extract embedded file links before deletion).
    ''' </summary>
    Public Shared Function GetCommentText(commentId As Guid) As String
        Dim result As Object = ExecuteScalar(
            "SELECT CommentText FROM TicketComments WHERE CommentID = @ID",
            New SqlParameter("@ID", SqlDbType.UniqueIdentifier) With {.Value = commentId})
        If result Is Nothing OrElse IsDBNull(result) Then Return ""
        Return result.ToString()
    End Function

    ''' Deletes a comment. Only succeeds if the userId matches the comment creator.
    ''' Hard-deletes leaf comments; soft-deletes comments that have threaded replies
    ''' (so the thread structure is preserved).
    ''' </summary>
    Public Shared Sub DeleteComment(commentId As Guid, userId As Guid)
        ' Ownership check built into the query — 0 rows returned = not owner
        Dim isOwner As Boolean = Convert.ToInt32(ExecuteScalar(
            "SELECT COUNT(1) FROM TicketComments WHERE CommentID = @ID AND CreatedBy = @UserID",
            New SqlParameter("@ID", SqlDbType.UniqueIdentifier) With {.Value = commentId},
            New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId})) > 0
        If Not isOwner Then Return

        Dim hasChildren As Boolean = Convert.ToInt32(ExecuteScalar(
            "SELECT COUNT(1) FROM TicketComments WHERE ParentCommentID = @ID",
            New SqlParameter("@ID", SqlDbType.UniqueIdentifier) With {.Value = commentId})) > 0

        If hasChildren Then
            ' Soft-delete: preserve the node so child replies stay connected
            Const removed As String = "<em class='comment-deleted-text'>This comment was removed.</em>"
            ExecuteNonQuery(
                "UPDATE TicketComments SET CommentText = @Text WHERE CommentID = @ID",
                New SqlParameter("@Text", removed),
                New SqlParameter("@ID", SqlDbType.UniqueIdentifier) With {.Value = commentId})
            ExecuteNonQuery(
                "UPDATE TicketActivity SET ActivityText = @Text WHERE CommentID = @ID",
                New SqlParameter("@Text", removed),
                New SqlParameter("@ID", SqlDbType.UniqueIdentifier) With {.Value = commentId})
        Else
            ' Hard-delete leaf comment
            ExecuteNonQuery(
                "DELETE FROM TicketActivity WHERE CommentID = @ID",
                New SqlParameter("@ID", SqlDbType.UniqueIdentifier) With {.Value = commentId})
            ExecuteNonQuery(
                "DELETE FROM TicketComments WHERE CommentID = @ID",
                New SqlParameter("@ID", SqlDbType.UniqueIdentifier) With {.Value = commentId})
        End If
    End Sub

    ''' <summary>
    ''' Saves a comment or file to the unified activity stream
    ''' </summary>
    Public Shared Function SaveTicketActivity(ticketId As Guid, userId As Guid, activityType As String, _
                                             Optional commentText As String = Nothing, _
                                             Optional filePath As String = Nothing, _
                                             Optional fileName As String = Nothing, _
                                             Optional fileType As String = Nothing, _
                                             Optional fileSize As Long = 0, _
                                             Optional isPrivate As Boolean = False, _
                                             Optional recipientUserId As Guid? = Nothing) As Guid
        Try
            Dim query As String = "INSERT INTO TicketActivity (TicketID, ActivityType, CommentText, FilePath, OriginalFileName, FileType, FileSize, CreatedBy, RecipientUserID, IsPrivate) " & _
                                 "OUTPUT INSERTED.ActivityID " & _
                                 "VALUES (@TicketID, @ActivityType, @CommentText, @FilePath, @FileName, @FileType, @FileSize, @CreatedBy, @RecipientUserID, @IsPrivate);"

            Dim parameters As New List(Of SqlParameter) From {
                New SqlParameter("@TicketID", SqlDbType.UniqueIdentifier) With {.Value = ticketId},
                New SqlParameter("@ActivityType", activityType),
                New SqlParameter("@CommentText", If(String.IsNullOrEmpty(commentText), DBNull.Value, commentText)),
                New SqlParameter("@FilePath", If(String.IsNullOrEmpty(filePath), DBNull.Value, filePath)),
                New SqlParameter("@FileName", If(String.IsNullOrEmpty(fileName), DBNull.Value, fileName)),
                New SqlParameter("@FileType", If(String.IsNullOrEmpty(fileType), DBNull.Value, fileType)),
                New SqlParameter("@FileSize", If(fileSize = 0, DBNull.Value, fileSize)),
                New SqlParameter("@CreatedBy", SqlDbType.UniqueIdentifier) With {.Value = userId},
                New SqlParameter("@RecipientUserID", SqlDbType.UniqueIdentifier) With {.Value = If(recipientUserId.HasValue, CObj(recipientUserId.Value), DBNull.Value)},
                New SqlParameter("@IsPrivate", isPrivate)
            }

            Return Guid.Parse(ExecuteScalar(query, parameters.ToArray()).ToString())
        Catch ex As Exception
            Throw New Exception("Error saving ticket activity: " & ex.Message, ex)
        End Try
    End Function

    ''' <summary>
    ''' Saves ticket activity with CommentID or FileID reference
    ''' </summary>
    Public Shared Sub SaveActivityRecord(ticketId As Guid, userId As Guid, activityType As String, _
                                        activityText As String, Optional commentId As Guid? = Nothing, _
                                        Optional fileId As Guid? = Nothing, Optional isPrivate As Boolean = False)
        Try
            Dim query As String = "INSERT INTO TicketActivity (TicketID, UserID, ActivityType, ActivityText, CommentID, FileID, IsPrivate) " & _
                                 "VALUES (@TicketID, @UserID, @ActivityType, @ActivityText, @CommentID, @FileID, @IsPrivate);"

            Dim parameters As New List(Of SqlParameter) From {
                New SqlParameter("@TicketID", SqlDbType.UniqueIdentifier) With {.Value = ticketId},
                New SqlParameter("@UserID", SqlDbType.UniqueIdentifier) With {.Value = userId},
                New SqlParameter("@ActivityType", activityType),
                New SqlParameter("@ActivityText", activityText),
                New SqlParameter("@CommentID", SqlDbType.UniqueIdentifier) With {.Value = If(commentId.HasValue, CObj(commentId.Value), DBNull.Value)},
                New SqlParameter("@FileID", SqlDbType.UniqueIdentifier) With {.Value = If(fileId.HasValue, CObj(fileId.Value), DBNull.Value)},
                New SqlParameter("@IsPrivate", isPrivate)
            }

            ExecuteNonQuery(query, parameters.ToArray())
        Catch ex As Exception
            Throw New Exception("Error saving activity record: " & ex.Message, ex)
        End Try
    End Sub

    ''' <summary>
    ''' Gets company short name by ClientID
    ''' </summary>
    Public Shared Function GetCompanyShortName(clientId As Guid) As String
        Try
            Dim query As String = "SELECT ShortName FROM Clients WHERE ClientID = @ClientID"
            Dim result As Object = ExecuteScalar(query, New SqlParameter("@ClientID", SqlDbType.UniqueIdentifier) With {.Value = clientId})
            Return If(result IsNot Nothing, result.ToString(), "")
        Catch ex As Exception
            Throw New Exception("Error getting company short name: " & ex.Message, ex)
        End Try
    End Function

    ''' <summary>
    ''' Searches for tickets by partial ticket number or subject for a specific company
    ''' </summary>
    Public Shared Function SearchTicketsForReference(searchTerm As String, currentTicketId As Guid, companyId As Integer) As DataTable
        Try
            Dim query As String =
                "SELECT TOP 20 t.TicketID, t.TicketNumber, t.Subject, s.Name AS Status " &
                "FROM Tickets t " &
                "INNER JOIN Status s ON t.StatusID = s.StatusID " &
                "INNER JOIN Projects p ON t.ProjectID = p.ProjectID " &
                "INNER JOIN Clients c ON p.ClientID = c.ClientID " &
                "WHERE t.IsActive = 1 AND t.IsArchived = 0 AND t.TicketID != @CurrentTicketID " &
                "AND c.CompanyID = @CompanyID " &
                "AND (t.TicketNumber LIKE @SearchTerm OR t.Subject LIKE @SearchTerm) " &
                "AND NOT EXISTS ( " &
                "    SELECT 1 FROM TicketReferences r " &
                "    WHERE (r.TicketID = @CurrentTicketID AND r.ReferencedTicketID = t.TicketID) " &
                "       OR (r.TicketID = t.TicketID AND r.ReferencedTicketID = @CurrentTicketID) " &
                ") " &
                "ORDER BY t.CreatedOn DESC"

            Return ExecuteDataTable(query,
                New SqlParameter("@SearchTerm", "%" & searchTerm & "%"),
                New SqlParameter("@CurrentTicketID", SqlDbType.UniqueIdentifier) With {.Value = currentTicketId},
                New SqlParameter("@CompanyID", companyId))
        Catch ex As Exception
            Throw New Exception("Error searching tickets: " & ex.Message, ex)
        End Try
    End Function

#End Region

End Class
