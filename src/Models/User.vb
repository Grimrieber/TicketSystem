Imports System
Imports System.Data

''' <summary>
''' User Model - Represents a user in the system
''' </summary>
Public Class User
    Public Property UserID As Integer
    Public Property Username As String
    Public Property PasswordHash As String
    Public Property FirstName As String
    Public Property LastName As String
    Public Property Role As String
    Public Property Email As String
    Public Property CreatedOn As DateTime
    Public Property IsActive As Boolean
    Public Property LastLoginOn As DateTime?

    ''' <summary>
    ''' Creates a User object from a DataRow
    ''' </summary>
    Public Shared Function FromDataRow(row As DataRow) As User
        If row Is Nothing Then Return Nothing

        Return New User With {
            .UserID = Convert.ToInt32(row("UserID")),
            .Username = row("Username").ToString(),
            .PasswordHash = row("PasswordHash").ToString(),
            .FirstName = If(row.Table.Columns.Contains("FirstName") AndAlso Not IsDBNull(row("FirstName")), row("FirstName").ToString(), ""),
            .LastName = If(row.Table.Columns.Contains("LastName") AndAlso Not IsDBNull(row("LastName")), row("LastName").ToString(), ""),
            .Role = row("Role").ToString(),
            .Email = row("Email").ToString(),
            .CreatedOn = Convert.ToDateTime(row("CreatedOn")),
            .IsActive = Convert.ToBoolean(row("IsActive")),
            .LastLoginOn = If(IsDBNull(row("LastLoginOn")), Nothing, Convert.ToDateTime(row("LastLoginOn")))
        }
    End Function

    ''' <summary>
    ''' Returns "FirstName LastName" if available, otherwise Username
    ''' </summary>
    Public ReadOnly Property FullName As String
        Get
            Dim fn As String = (If(FirstName, "") & " " & If(LastName, "")).Trim()
            Return If(String.IsNullOrEmpty(fn), Username, fn)
        End Get
    End Property

    ''' <summary>
    ''' Checks if user has the specified role
    ''' </summary>
    Public Function HasRole(role As String) As Boolean
        Return Me.Role.Equals(role, StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>
    ''' Checks if user is an Admin
    ''' </summary>
    Public ReadOnly Property IsAdmin As Boolean
        Get
            Return HasRole("Admin")
        End Get
    End Property

    ''' <summary>
    ''' Checks if user is a Worker
    ''' </summary>
    Public ReadOnly Property IsWorker As Boolean
        Get
            Return HasRole("Worker")
        End Get
    End Property

    ''' <summary>
    ''' Checks if user is a Client
    ''' </summary>
    Public ReadOnly Property IsClient As Boolean
        Get
            Return HasRole("Client")
        End Get
    End Property
End Class
