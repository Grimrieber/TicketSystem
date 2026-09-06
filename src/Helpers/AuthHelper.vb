Imports System
Imports System.Collections.Generic
Imports System.Configuration
Imports System.Data
Imports System.IO
Imports System.Linq
Imports System.Web
Imports System.Web.Security
Imports BCrypt.Net
Imports Microsoft.VisualBasic

''' <summary>
''' AuthHelper - Handles authentication and authorization
''' Provides secure password hashing using BCrypt
''' </summary>
Public Class AuthHelper

#Region "Password Hashing"

    ''' <summary>
    ''' Hashes a password using BCrypt
    ''' </summary>
    Public Shared Function HashPassword(password As String) As String
        Try
            Dim workFactor As Integer = 11
            If ConfigurationManager.AppSettings("BcryptWorkFactor") IsNot Nothing Then
                Integer.TryParse(ConfigurationManager.AppSettings("BcryptWorkFactor"), workFactor)
            End If

            Return BCrypt.Net.BCrypt.HashPassword(password, workFactor)
        Catch ex As Exception
            Throw New ApplicationException("Error hashing password.", ex)
        End Try
    End Function

    ''' <summary>
    ''' Verifies a password against a hash
    ''' </summary>
    Public Shared Function VerifyPassword(password As String, hash As String) As Boolean
        Try
            Return BCrypt.Net.BCrypt.Verify(password, hash)
        Catch ex As Exception
            ' Invalid hash format or other error
            Return False
        End Try
    End Function

#End Region

#Region "Transport Security"

    ''' <summary>
    ''' True when the app is configured to run over HTTPS (Web.config appSetting
    ''' EnableSSL). Single source of truth for every HTTPS-dependent decision —
    ''' the Secure cookie flag, the Global.asax redirect, and anything added later.
    ''' Defaults to True: if the setting is missing or unparseable, fail closed
    ''' towards the secure behaviour rather than silently downgrading.
    ''' </summary>
    Public Shared Function IsSslEnabled() As Boolean
        Dim raw As String = ConfigurationManager.AppSettings("EnableSSL")
        If String.IsNullOrWhiteSpace(raw) Then Return True

        Dim enabled As Boolean
        If Boolean.TryParse(raw.Trim(), enabled) Then Return enabled
        Return True
    End Function

    ''' <summary>
    ''' True if the current request actually arrived over TLS.
    '''
    ''' Request.IsSecureConnection alone is wrong behind anything that terminates
    ''' TLS upstream — a load balancer, a reverse proxy, most shared hosting. In
    ''' that setup the origin sees plain HTTP, IsSecureConnection returns False,
    ''' and a redirect-to-HTTPS becomes an infinite loop. Honouring the forwarded
    ''' headers is what prevents that.
    ''' </summary>
    Public Shared Function RequestIsSecure(req As HttpRequest) As Boolean
        If req Is Nothing Then Return False
        If req.IsSecureConnection Then Return True

        ' Set by most proxies/CDNs when they terminate TLS on the app's behalf.
        Dim proto As String = req.Headers("X-Forwarded-Proto")
        If Not String.IsNullOrEmpty(proto) AndAlso
           proto.Split(","c)(0).Trim().Equals("https", StringComparison.OrdinalIgnoreCase) Then
            Return True
        End If

        ' Some older IIS/ARR front ends use this instead.
        If String.Equals(req.Headers("Front-End-Https"), "on", StringComparison.OrdinalIgnoreCase) Then
            Return True
        End If

        Return False
    End Function

#End Region

#Region "Authentication"

    ''' <summary>
    ''' Authenticates a user with email and password
    ''' Returns UserID if successful, Guid.Empty if failed
    ''' </summary>
    Public Shared Function AuthenticateUser(email As String, password As String) As Guid
        Try
            ' Get user from database by email
            Dim userRow As DataRow = DatabaseHelper.GetUserByEmail(email)

            If userRow IsNot Nothing Then
                Dim passwordHash As String = userRow("PasswordHash").ToString()

                ' Verify password
                If VerifyPassword(password, passwordHash) Then
                    Dim userId As Guid = Guid.Parse(userRow("UserID").ToString())

                    ' Update last login
                    DatabaseHelper.UpdateLastLogin(userId)

                    ' Log successful login
                    LogLoginAttempt(email, True, "")

                    Return userId
                Else
                    ' Log failed login - wrong password
                    LogLoginAttempt(email, False, "Invalid password")
                    Return Guid.Empty
                End If
            Else
                ' Log failed login - user not found
                LogLoginAttempt(email, False, "User not found")
                Return Guid.Empty
            End If
        Catch ex As Exception
            LogLoginAttempt(email, False, "System error: " & ex.Message)
            Throw New ApplicationException("Authentication error occurred.", ex)
        End Try
    End Function

    ''' <summary>
    ''' Creates a forms authentication ticket and cookie
    ''' </summary>
    Public Shared Sub CreateAuthenticationTicket(userId As Guid, email As String, role As String,
                                                 rememberMe As Boolean,
                                                 Optional companyId As Integer = 0,
                                                 Optional fullName As String = "")
        Try
            ' Create forms authentication ticket
            ' Parameters: version, name (email), issueDate, expiration, isPersistent, userData, cookiePath
            Dim expirationMinutes As Integer = If(rememberMe, 43200, 60) ' 30 days or 1 hour

            ' userData carries everything the session needs, not just the role.
            ' Session state is InProc, so it dies on every app-pool recycle — and
            ' rebuilding it used to mean a database round trip on the next
            ' request. Against a remote database that call can time out, and the
            ' user is silently thrown back to the login page holding a perfectly
            ' valid cookie. Packing the values into the (encrypted, signed)
            ' ticket removes the database from that path entirely.
            ' Pipe-separated; FullName is last so a name containing a pipe cannot
            ' shift the fields before it.
            Dim userData As String = String.Join("|", New String() {
                role,
                userId.ToString(),
                companyId.ToString(),
                If(fullName, "")
            })

            Dim ticket As New FormsAuthenticationTicket(
                1,
                email,
                DateTime.Now,
                DateTime.Now.AddMinutes(expirationMinutes),
                rememberMe,
                userData,
                FormsAuthentication.FormsCookiePath
            )

            ' Encrypt the ticket
            Dim encryptedTicket As String = FormsAuthentication.Encrypt(ticket)

            ' Create cookie
            Dim cookie As New HttpCookie(FormsAuthentication.FormsCookieName, encryptedTicket)
            cookie.HttpOnly = True
            ' Secure is driven by the EnableSSL app setting rather than hardcoded, so
            ' the flag flips with the rest of the HTTPS configuration in one place.
            ' A Secure cookie is simply never sent over plain HTTP — which is the
            ' point: without it, anyone on the network path can lift the auth ticket
            ' and reuse the session, and no amount of MFA at login helps once they
            ' have a valid ticket.
            cookie.Secure = IsSslEnabled()
            cookie.SameSite = SameSiteMode.Lax
            If rememberMe Then
                cookie.Expires = ticket.Expiration
            End If

            ' Add cookie to response
            HttpContext.Current.Response.Cookies.Add(cookie)

        Catch ex As Exception
            Throw New ApplicationException("Error creating authentication ticket.", ex)
        End Try
    End Sub

    ''' <summary>
    ''' The four values packed into the FormsAuth ticket at sign-in:
    ''' {role, userId, companyId, fullName}. Returns Nothing for a ticket issued
    ''' before that format existed, or when there is no ticket at all.
    '''
    ''' The ticket is encrypted and signed by the framework, so its contents are
    ''' exactly as trustworthy as the cookie itself — and unlike session state it
    ''' survives an app-pool recycle, which is the entire point of reading from
    ''' it here.
    ''' </summary>
    Private Shared Function TicketData() As String()
        Try
            Dim ctx As HttpContext = HttpContext.Current
            If ctx Is Nothing OrElse ctx.User Is Nothing Then Return Nothing

            Dim fid = TryCast(ctx.User.Identity, FormsIdentity)
            If fid Is Nothing OrElse fid.Ticket Is Nothing Then Return Nothing

            Dim parts As String() = Convert.ToString(fid.Ticket.UserData).Split(New Char() {"|"c}, 4)
            If parts.Length <> 4 Then Return Nothing
            Return parts
        Catch
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Gets the current authenticated user's ID.
    '''
    ''' Session first, then the auth ticket. Session state is InProc, so it is
    ''' wiped by every app-pool recycle — a deploy, a file change, an idle
    ''' timeout. Treating it as the only source of identity is what turned a
    ''' routine recycle into "clicking any link logs me out", because the ticket
    ''' in the browser was still perfectly valid the whole time.
    ''' </summary>
    Public Shared Function GetCurrentUserId() As Guid?
        Dim ctx As HttpContext = HttpContext.Current

        If ctx IsNot Nothing AndAlso ctx.Session IsNot Nothing AndAlso
           ctx.Session("UserID") IsNot Nothing Then
            Dim fromSession As Guid
            If Guid.TryParse(ctx.Session("UserID").ToString(), fromSession) Then
                Return fromSession
            End If
        End If

        Dim parts As String() = TicketData()
        If parts IsNot Nothing Then
            Dim fromTicket As Guid
            If Guid.TryParse(parts(1), fromTicket) Then Return fromTicket
        End If

        Return Nothing
    End Function

    ''' <summary>
    ''' Gets the current authenticated user's email from session
    ''' </summary>
    Public Shared Function GetCurrentEmail() As String
        Dim ctx As HttpContext = HttpContext.Current

        If ctx IsNot Nothing AndAlso ctx.Session IsNot Nothing AndAlso
           ctx.Session("Email") IsNot Nothing Then
            Return ctx.Session("Email").ToString()
        End If

        ' The ticket's Name IS the email (set in CreateAuthenticationTicket).
        If ctx IsNot Nothing AndAlso ctx.User IsNot Nothing AndAlso
           ctx.User.Identity IsNot Nothing AndAlso ctx.User.Identity.IsAuthenticated Then
            Return ctx.User.Identity.Name
        End If

        Return Nothing
    End Function

    ''' <summary>
    ''' Gets the current authenticated user's display name (FirstName LastName, falls back to Email)
    ''' </summary>
    Public Shared Function GetCurrentDisplayName() As String
        Dim ctx As HttpContext = HttpContext.Current

        If ctx IsNot Nothing AndAlso ctx.Session IsNot Nothing AndAlso
           ctx.Session("FullName") IsNot Nothing Then
            Dim fn As String = ctx.Session("FullName").ToString()
            If Not String.IsNullOrEmpty(fn) Then Return fn
        End If

        Dim parts As String() = TicketData()
        If parts IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(parts(3)) Then
            Return parts(3)
        End If

        Return GetCurrentEmail()
    End Function

    ''' <summary>
    ''' Gets the current authenticated user's role from session
    ''' </summary>
    Public Shared Function GetCurrentUserRole() As String
        Dim ctx As HttpContext = HttpContext.Current

        If ctx IsNot Nothing AndAlso ctx.Session IsNot Nothing AndAlso
           ctx.Session("Role") IsNot Nothing Then
            Return ctx.Session("Role").ToString()
        End If

        Dim parts As String() = TicketData()
        If parts IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(parts(0)) Then
            Return parts(0)
        End If

        Return Nothing
    End Function

    ''' <summary>
    ''' Gets the current authenticated user's CompanyID from session
    ''' </summary>
    Public Shared Function GetCurrentUserCompanyId() As Integer?
        Dim ctx As HttpContext = HttpContext.Current

        If ctx IsNot Nothing AndAlso ctx.Session IsNot Nothing AndAlso
           ctx.Session("CompanyID") IsNot Nothing Then
            Dim fromSession As Integer
            If Integer.TryParse(ctx.Session("CompanyID").ToString(), fromSession) Then
                Return fromSession
            End If
        End If

        Dim parts As String() = TicketData()
        If parts IsNot Nothing Then
            Dim fromTicket As Integer
            If Integer.TryParse(parts(2), fromTicket) Then Return fromTicket
        End If

        Return Nothing
    End Function

    ''' <summary>
    ''' Checks if user is authenticated.
    '''
    ''' Also self-heals "Remember Me" sessions: if the FormsAuth cookie is still
    ''' valid (30-day persistent ticket) but the in-process session has been lost
    ''' — app pool recycled, server restarted, session timeout (60 min) elapsed
    ''' between visits — re-populate the session from the database before
    ''' returning. Without this, Login.aspx's Page_Load (and any other caller
    ''' that gates on IsAuthenticated) treats a returning Remember-Me user as
    ''' logged out and forces them through the form again, which is exactly the
    ''' "Remember Me for 30 days doesn't work" symptom.
    ''' </summary>
    Public Shared Function IsAuthenticated() As Boolean
        Dim ctx As HttpContext = HttpContext.Current
        If ctx Is Nothing Then Return False

        ' The signed, encrypted FormsAuth ticket is the source of truth. Session
        ' state is InProc and therefore disappears on every app-pool recycle —
        ' a deploy, a config change, an idle timeout. Requiring it here meant a
        ' recycle silently logged everyone out mid-click, even though the ticket
        ' in their browser was still valid: sign in, click a link, back to the
        ' login page.
        If ctx.User Is Nothing OrElse ctx.User.Identity Is Nothing Then Return False
        If Not ctx.User.Identity.IsAuthenticated Then Return False

        ' Refill the session cache when it has been lost. This is best-effort
        ' and NOT a condition of being authenticated — the getters fall back to
        ' the ticket on their own, so a failure here degrades performance, not
        ' access.
        If ctx.Session IsNot Nothing AndAlso ctx.Session("UserID") Is Nothing Then
            TryRefreshSession()
        End If

        ' Still require an identifiable user: a ticket we cannot resolve to a
        ' UserID is no use to any page downstream.
        Return GetCurrentUserId().HasValue
    End Function

    ''' <summary>
    ''' Signs out the current user
    ''' </summary>
    Public Shared Sub SignOut()
        Try
            ' Clear session
            HttpContext.Current.Session.Clear()
            HttpContext.Current.Session.Abandon()

            ' Sign out forms authentication
            FormsAuthentication.SignOut()

            ' Clear cookies
            Dim authCookie As New HttpCookie(FormsAuthentication.FormsCookieName) With {
                .Expires = DateTime.Now.AddDays(-1)
            }
            HttpContext.Current.Response.Cookies.Add(authCookie)

            Dim sessionCookie As New HttpCookie("ASP.NET_SessionId") With {
                .Expires = DateTime.Now.AddDays(-1)
            }
            HttpContext.Current.Response.Cookies.Add(sessionCookie)

        Catch ex As Exception
            Throw New ApplicationException("Error signing out user.", ex)
        End Try
    End Sub

#End Region

#Region "Authorization"

    ''' <summary>
    ''' Checks if current user has the specified role
    ''' </summary>
    Public Shared Function HasRole(role As String) As Boolean
        Dim userRole As String = GetCurrentUserRole()
        Return userRole IsNot Nothing AndAlso userRole.Equals(role, StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>
    ''' Checks if current user has any of the specified roles
    ''' </summary>
    Public Shared Function HasAnyRole(ParamArray roles() As String) As Boolean
        Dim userRole As String = GetCurrentUserRole()
        If userRole Is Nothing Then
            Return False
        End If

        For Each role As String In roles
            If userRole.Equals(role, StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If
        Next

        Return False
    End Function

    ''' <summary>
    ''' Checks if current user is an Admin
    ''' </summary>
    Public Shared Function IsAdmin() As Boolean
        Return HasRole("Admin")
    End Function

    ''' <summary>
    ''' Checks if current user is a Worker
    ''' </summary>
    Public Shared Function IsWorker() As Boolean
        Return HasRole("Worker")
    End Function

    ''' <summary>
    ''' Checks if current user is a Client
    ''' </summary>
    Public Shared Function IsClient() As Boolean
        Return HasRole("Client")
    End Function

    ''' <summary>
    ''' Checks if current user can edit ticket
    ''' Admin and Worker can edit, Client can only edit their own tickets
    ''' </summary>
    Public Shared Function CanEditTicket(ticketCreatedBy As Guid) As Boolean
        If Not IsAuthenticated() Then
            Return False
        End If

        If IsAdmin() OrElse IsWorker() Then
            Return True
        End If

        If IsClient() Then
            Dim currentUserId As Guid? = GetCurrentUserId()
            Return currentUserId.HasValue AndAlso currentUserId.Value = ticketCreatedBy
        End If

        Return False
    End Function

    ''' <summary>
    ''' Checks if current user can delete ticket (Admin only)
    ''' </summary>
    Public Shared Function CanDeleteTicket() As Boolean
        Return IsAuthenticated() AndAlso IsAdmin()
    End Function

    ''' <summary>
    ''' Checks if current user can reassign ticket (Admin and Worker)
    ''' </summary>
    Public Shared Function CanReassignTicket() As Boolean
        Return IsAuthenticated() AndAlso (IsAdmin() OrElse IsWorker())
    End Function

    ''' <summary>
    ''' Checks if current user can view private comments (Workers only)
    ''' </summary>
    Public Shared Function CanViewPrivateComments() As Boolean
        Return IsAuthenticated() AndAlso IsWorker()
    End Function

    ''' <summary>
    ''' Redirects to login page if not authenticated.
    ''' If the FormsAuth cookie is still valid (Remember Me) but the server-side session
    ''' has expired, the session is automatically re-hydrated from the database so the
    ''' user does not have to log in again.
    ''' </summary>
    Public Shared Sub RequireAuthentication()
        Dim ctx As HttpContext = HttpContext.Current

        ' Session rehydration now lives inside IsAuthenticated(), so it happens
        ' on every entry point rather than only this one.
        If Not IsAuthenticated() Then
            LogAuthBounce(ctx)
            ctx.Response.Redirect("~/Login.aspx?ReturnUrl=" & HttpUtility.UrlEncode(ctx.Request.Url.PathAndQuery))
        End If
    End Sub

    ''' <summary>
    ''' Records exactly why a request was bounced to the login page.
    '''
    ''' "It logged me out" has half a dozen possible causes — no auth cookie, a
    ''' cookie the browser refused to send back, a dead session that failed to
    ''' rehydrate, the wrong scheme or port — and they are indistinguishable from
    ''' the outside. This writes down which one it actually was.
    '''
    ''' Diagnostic only; safe to remove once the cause is known. Logs no cookie
    ''' values, only whether they were present.
    ''' </summary>
    Private Shared Sub LogAuthBounce(ctx As HttpContext)
        Try
            Dim authCookie As HttpCookie = ctx.Request.Cookies(FormsAuthentication.FormsCookieName)
            Dim sessCookie As HttpCookie = ctx.Request.Cookies("TicketSystemSession")

            Dim identityName As String = "(none)"
            Dim isAuth As Boolean = False
            If ctx.User IsNot Nothing AndAlso ctx.User.Identity IsNot Nothing Then
                isAuth = ctx.User.Identity.IsAuthenticated
                If isAuth Then identityName = ctx.User.Identity.Name
            End If

            Dim sessionId As String = "(no session)"
            Try
                If ctx.Session IsNot Nothing Then sessionId = ctx.Session.SessionID
            Catch
            End Try

            Dim line As String = String.Format(
                "[{0:yyyy-MM-dd HH:mm:ss}] BOUNCE {1}" & vbCrLf &
                "   url          : {2}" & vbCrLf &
                "   secure       : {3}   forwardedProto={4}" & vbCrLf &
                "   authCookie   : {5}" & vbCrLf &
                "   sessionCookie: {6}   sessionId={7}" & vbCrLf &
                "   identity     : isAuthenticated={8} name={9}" & vbCrLf &
                "   session.UserID: {10}" & vbCrLf &
                "   userAgent    : {11}" & vbCrLf & vbCrLf,
                DateTime.Now,
                ctx.Request.Path,
                ctx.Request.Url.AbsoluteUri,
                ctx.Request.IsSecureConnection,
                If(ctx.Request.Headers("X-Forwarded-Proto"), "-"),
                If(authCookie Is Nothing, "ABSENT", "present (len " & authCookie.Value.Length & ")"),
                If(sessCookie Is Nothing, "ABSENT", "present"),
                sessionId,
                isAuth, identityName,
                If(ctx.Session Is Nothing OrElse ctx.Session("UserID") Is Nothing, "ABSENT", "present"),
                If(ctx.Request.UserAgent, "-"))

            Dim logPath As String = ctx.Server.MapPath("~/App_Data/Logs/")
            If Not Directory.Exists(logPath) Then Directory.CreateDirectory(logPath)
            File.AppendAllText(Path.Combine(logPath, String.Format("AuthBounce_{0:yyyyMMdd}.txt", DateTime.Now)), line)
        Catch
            ' Diagnostics must never break the redirect they are describing.
        End Try
    End Sub

    ''' <summary>
    ''' Re-populates the session from the database using the identity stored in the
    ''' FormsAuthentication ticket.  Called automatically when a valid Remember Me
    ''' cookie is present but the server-side session has expired.
    ''' </summary>
    Private Shared Sub TryRefreshSession()
        Try
            ' Identity.Name now stores email (set during CreateAuthenticationTicket)
            Dim email As String = HttpContext.Current.User.Identity.Name
            If String.IsNullOrEmpty(email) Then Return

            ' ── Fast path: rebuild straight from the ticket ─────────────────
            ' The ticket is encrypted and signed by the framework, so its
            ' contents are as trustworthy as the cookie itself — and this path
            ' touches no database, which is the whole point. A remote-database
            ' hiccup must not be able to log somebody out.
            Dim fid = TryCast(HttpContext.Current.User.Identity, FormsIdentity)
            If fid IsNot Nothing AndAlso fid.Ticket IsNot Nothing Then
                Dim parts As String() = Convert.ToString(fid.Ticket.UserData).Split(New Char() {"|"c}, 4)
                If parts.Length = 4 Then
                    Dim uid As Guid
                    Dim cid As Integer
                    If Guid.TryParse(parts(1), uid) AndAlso Integer.TryParse(parts(2), cid) Then
                        InitializeSession(uid, email, parts(0), cid, parts(3))
                        Return
                    End If
                End If
            End If

            ' ── Fallback: tickets issued before userData carried these fields ──
            Dim userRow As DataRow = DatabaseHelper.GetUserByEmail(email)
            If userRow Is Nothing Then Return

            ' Do not re-hydrate for deactivated accounts
            If Not Convert.ToBoolean(userRow("IsActive")) Then Return

            Dim userId As Guid    = Guid.Parse(userRow("UserID").ToString())
            Dim role As String    = userRow("Role").ToString()
            Dim companyId As Integer = Convert.ToInt32(userRow("CompanyID"))
            Dim firstName As String = If(userRow.Table.Columns.Contains("FirstName") AndAlso Not IsDBNull(userRow("FirstName")), userRow("FirstName").ToString(), "")
            Dim lastName As String = If(userRow.Table.Columns.Contains("LastName") AndAlso Not IsDBNull(userRow("LastName")), userRow("LastName").ToString(), "")
            Dim fullName As String = (firstName & " " & lastName).Trim()

            InitializeSession(userId, email, role, companyId, fullName)

        Catch ex As Exception
            ' Do NOT fail silently. This is the only thing standing between a
            ' valid auth cookie and being thrown back to the login page: if the
            ' database call here times out — which is easy against a remote
            ' server under load — the user is logged out with no trace anywhere.
            ' Swallowing it made an intermittent, unexplainable logout.
            LogRefreshFailure(ex)
        End Try
    End Sub

    ''' <summary>
    ''' Records why a session could not be rebuilt from a valid auth cookie.
    ''' </summary>
    Private Shared Sub LogRefreshFailure(ex As Exception)
        Try
            Dim ctx As HttpContext = HttpContext.Current
            Dim logPath As String = ctx.Server.MapPath("~/App_Data/Logs/")
            If Not Directory.Exists(logPath) Then Directory.CreateDirectory(logPath)
            File.AppendAllText(
                Path.Combine(logPath, String.Format("SessionRefresh_{0:yyyyMMdd}.txt", DateTime.Now)),
                String.Format("[{0:yyyy-MM-dd HH:mm:ss}] {1} {2}" & vbCrLf & "   {3}: {4}" & vbCrLf & vbCrLf,
                              DateTime.Now, ctx.Request.HttpMethod, ctx.Request.Path,
                              ex.GetType().Name, ex.Message))
        Catch
        End Try
    End Sub

    ''' <summary>
    ''' Redirects to access denied page if user doesn't have required role
    ''' </summary>
    Public Shared Sub RequireRole(role As String)
        RequireAuthentication()
        If Not HasRole(role) Then
            HttpContext.Current.Response.Redirect("~/AccessDenied.aspx")
        End If
    End Sub

    ''' <summary>
    ''' Redirects to access denied page if user doesn't have any of the required roles
    ''' </summary>
    Public Shared Sub RequireAnyRole(ParamArray roles() As String)
        RequireAuthentication()
        If Not HasAnyRole(roles) Then
            HttpContext.Current.Response.Redirect("~/AccessDenied.aspx")
        End If
    End Sub

#End Region

#Region "Session Management"

    ''' <summary>
    ''' Initializes user session after successful login
    ''' </summary>
    Public Shared Sub InitializeSession(userId As Guid, email As String, role As String, companyId As Integer, Optional fullName As String = "")
        Try
            HttpContext.Current.Session("UserID") = userId.ToString()
            HttpContext.Current.Session("Email") = email
            HttpContext.Current.Session("FullName") = If(String.IsNullOrEmpty(fullName), email, fullName)
            HttpContext.Current.Session("Role") = role
            HttpContext.Current.Session("CompanyID") = companyId
            HttpContext.Current.Session("LoginTime") = DateTime.Now
        Catch ex As Exception
            Throw New ApplicationException("Error initializing session.", ex)
        End Try
    End Sub

    ''' <summary>
    ''' Gets user session information as a dictionary
    ''' </summary>
    Public Shared Function GetSessionInfo() As Dictionary(Of String, Object)
        Dim info As New Dictionary(Of String, Object)

        If IsAuthenticated() Then
            info.Add("UserID", HttpContext.Current.Session("UserID"))
            info.Add("Email", HttpContext.Current.Session("Email"))
            info.Add("Role", HttpContext.Current.Session("Role"))
            info.Add("Email", HttpContext.Current.Session("Email"))
            info.Add("LoginTime", HttpContext.Current.Session("LoginTime"))
        End If

        Return info
    End Function

#End Region

#Region "Login Attempt Logging"

    ''' <summary>
    ''' Logs login attempts for security auditing
    ''' </summary>
    Private Shared Sub LogLoginAttempt(username As String, success As Boolean, reason As String)
        Try
            Dim logPath As String = HttpContext.Current.Server.MapPath("~/App_Data/Logs/")
            If Not System.IO.Directory.Exists(logPath) Then
                System.IO.Directory.CreateDirectory(logPath)
            End If

            Dim logFile As String = Path.Combine(logPath, String.Format("LoginAttempts_{0:yyyyMMdd}.txt", DateTime.Now))
            Dim logMessage As String = String.Format("[{0:yyyy-MM-dd HH:mm:ss}] Username: {1} | Success: {2} | Reason: {3} | IP: {4}" & vbCrLf,
                                                    DateTime.Now,
                                                    username,
                                                    success,
                                                    reason,
                                                    GetClientIPAddress())

            System.IO.File.AppendAllText(logFile, logMessage)
        Catch
            ' Ignore logging errors
        End Try
    End Sub

    ''' <summary>
    ''' Gets the client's IP address
    ''' </summary>
    Private Shared Function GetClientIPAddress() As String
        Try
            Dim ip As String = HttpContext.Current.Request.ServerVariables("HTTP_X_FORWARDED_FOR")
            If String.IsNullOrEmpty(ip) Then
                ip = HttpContext.Current.Request.ServerVariables("REMOTE_ADDR")
            Else
                ' X_FORWARDED_FOR returns client1, proxy1, proxy2
                ip = ip.Split(",")(0)
            End If
            Return ip
        Catch
            Return "Unknown"
        End Try
    End Function

#End Region

#Region "Password Utilities"

    ''' <summary>
    ''' Validates password strength
    ''' Minimum 8 characters, at least one uppercase, one lowercase, one number, one special character
    ''' </summary>
    Public Shared Function IsPasswordStrong(password As String) As Boolean
        If String.IsNullOrEmpty(password) OrElse password.Length < 8 Then
            Return False
        End If

        Dim hasUpper As Boolean = password.Any(Function(c) Char.IsUpper(c))
        Dim hasLower As Boolean = password.Any(Function(c) Char.IsLower(c))
        Dim hasDigit As Boolean = password.Any(Function(c) Char.IsDigit(c))
        Dim hasSpecial As Boolean = password.Any(Function(c) Not Char.IsLetterOrDigit(c))

        Return hasUpper AndAlso hasLower AndAlso hasDigit AndAlso hasSpecial
    End Function

    ''' <summary>
    ''' Gets password strength validation message
    ''' </summary>
    Public Shared Function GetPasswordStrengthMessage() As String
        Return "Password must be at least 8 characters and contain: uppercase letter, lowercase letter, number, and special character."
    End Function

#End Region

End Class
