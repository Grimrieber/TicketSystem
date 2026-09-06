Imports System
Imports System.Data

''' <summary>
''' Client Model - Represents a client/company in the system
''' </summary>
Public Class Client
    Public Property ClientID As Guid
    Public Property ClientName As String
    Public Property Billable As Boolean
    Public Property ContactEmail As String
    Public Property ContactPhone As String
    Public Property CreatedOn As DateTime
    Public Property IsActive As Boolean

    ''' <summary>
    ''' Creates a Client object from a DataRow
    ''' </summary>
    Public Shared Function FromDataRow(row As DataRow) As Client
        If row Is Nothing Then Return Nothing

        Return New Client With {
            .ClientID = Guid.Parse(row("ClientID").ToString()),
            .ClientName = row("ClientName").ToString(),
            .Billable = Convert.ToBoolean(row("Billable")),
            .ContactEmail = If(IsDBNull(row("ContactEmail")), Nothing, row("ContactEmail").ToString()),
            .ContactPhone = If(IsDBNull(row("ContactPhone")), Nothing, row("ContactPhone").ToString()),
            .CreatedOn = Convert.ToDateTime(row("CreatedOn")),
            .IsActive = Convert.ToBoolean(row("IsActive"))
        }
    End Function
End Class
