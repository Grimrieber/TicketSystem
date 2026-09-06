Imports System
Imports System.Data

''' <summary>
''' Project Model - Represents a project in the system
''' </summary>
Public Class Project
    Public Property ProjectID As Guid
    Public Property Name As String
    Public Property ClientID As Guid
    Public Property Status As String
    Public Property Description As String
    Public Property CreatedOn As DateTime
    Public Property IsActive As Boolean

    ' Navigation property
    Public Property ClientName As String

    ''' <summary>
    ''' Creates a Project object from a DataRow
    ''' </summary>
    Public Shared Function FromDataRow(row As DataRow) As Project
        If row Is Nothing Then Return Nothing

        Return New Project With {
            .ProjectID = Guid.Parse(row("ProjectID").ToString()),
            .Name = row("Name").ToString(),
            .ClientID = Guid.Parse(row("ClientID").ToString()),
            .Status = row("Status").ToString(),
            .Description = If(IsDBNull(row("Description")), Nothing, row("Description").ToString()),
            .CreatedOn = Convert.ToDateTime(row("CreatedOn")),
            .IsActive = Convert.ToBoolean(row("IsActive")),
            .ClientName = If(row.Table.Columns.Contains("ClientName") AndAlso Not IsDBNull(row("ClientName")),
                           row("ClientName").ToString(), Nothing)
        }
    End Function
End Class
