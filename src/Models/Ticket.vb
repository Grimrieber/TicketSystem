Imports System
Imports System.Data

''' <summary>
''' Ticket Model - Represents a ticket in the system
''' </summary>
Public Class Ticket
    Public Property TicketID As Integer
    Public Property Subject As String
    Public Property Description As String
    Public Property ProjectID As Integer
    Public Property StatusID As Integer
    Public Property PriorityID As Integer
    Public Property DueDate As DateTime?
    Public Property CreatedBy As Integer
    Public Property AssignedTo As Integer?
    Public Property CreatedOn As DateTime
    Public Property UpdatedOn As DateTime
    Public Property CompletedOn As DateTime?
    Public Property IsActive As Boolean

    ' Navigation properties
    Public Property ProjectName As String
    Public Property ClientName As String
    Public Property ClientID As Integer
    Public Property StatusName As String
    Public Property PriorityName As String
    Public Property CreatedByName As String
    Public Property AssignedToName As String

    ''' <summary>
    ''' Creates a Ticket object from a DataRow
    ''' </summary>
    Public Shared Function FromDataRow(row As DataRow) As Ticket
        If row Is Nothing Then Return Nothing

        Dim ticket As New Ticket With {
            .TicketID = Convert.ToInt32(row("TicketID")),
            .Subject = row("Subject").ToString(),
            .Description = If(IsDBNull(row("Description")), Nothing, row("Description").ToString()),
            .ProjectID = Convert.ToInt32(row("ProjectID")),
            .StatusID = Convert.ToInt32(row("StatusID")),
            .PriorityID = Convert.ToInt32(row("PriorityID")),
            .DueDate = If(IsDBNull(row("DueDate")), Nothing, Convert.ToDateTime(row("DueDate"))),
            .CreatedBy = Convert.ToInt32(row("CreatedBy")),
            .AssignedTo = If(IsDBNull(row("AssignedTo")), Nothing, Convert.ToInt32(row("AssignedTo"))),
            .CreatedOn = Convert.ToDateTime(row("CreatedOn")),
            .UpdatedOn = Convert.ToDateTime(row("UpdatedOn")),
            .CompletedOn = If(IsDBNull(row("CompletedOn")), Nothing, Convert.ToDateTime(row("CompletedOn"))),
            .IsActive = Convert.ToBoolean(row("IsActive"))
        }

        ' Set navigation properties if available
        If row.Table.Columns.Contains("ProjectName") AndAlso Not IsDBNull(row("ProjectName")) Then
            ticket.ProjectName = row("ProjectName").ToString()
        End If

        If row.Table.Columns.Contains("ClientName") AndAlso Not IsDBNull(row("ClientName")) Then
            ticket.ClientName = row("ClientName").ToString()
        End If

        If row.Table.Columns.Contains("ClientID") AndAlso Not IsDBNull(row("ClientID")) Then
            ticket.ClientID = Convert.ToInt32(row("ClientID"))
        End If

        If row.Table.Columns.Contains("StatusName") AndAlso Not IsDBNull(row("StatusName")) Then
            ticket.StatusName = row("StatusName").ToString()
        End If

        If row.Table.Columns.Contains("PriorityName") AndAlso Not IsDBNull(row("PriorityName")) Then
            ticket.PriorityName = row("PriorityName").ToString()
        End If

        If row.Table.Columns.Contains("CreatedByName") AndAlso Not IsDBNull(row("CreatedByName")) Then
            ticket.CreatedByName = row("CreatedByName").ToString()
        End If

        If row.Table.Columns.Contains("AssignedToName") AndAlso Not IsDBNull(row("AssignedToName")) Then
            ticket.AssignedToName = row("AssignedToName").ToString()
        End If

        Return ticket
    End Function

    ''' <summary>
    ''' Checks if ticket is overdue
    ''' </summary>
    Public ReadOnly Property IsOverdue As Boolean
        Get
            Return DueDate.HasValue AndAlso DueDate.Value < DateTime.Now AndAlso StatusName <> "Completed"
        End Get
    End Property

    ''' <summary>
    ''' Gets priority CSS class for styling
    ''' </summary>
    Public ReadOnly Property PriorityCssClass As String
        Get
            Select Case PriorityName.ToLower()
                Case "urgent"
                    Return "priority-urgent"
                Case "high"
                    Return "priority-high"
                Case "medium"
                    Return "priority-medium"
                Case "low"
                    Return "priority-low"
                Case Else
                    Return "priority-default"
            End Select
        End Get
    End Property

    ''' <summary>
    ''' Gets status CSS class for styling
    ''' </summary>
    Public ReadOnly Property StatusCssClass As String
        Get
            Select Case StatusName.ToLower()
                Case "open"
                    Return "status-open"
                Case "in progress"
                    Return "status-in-progress"
                Case "pending review"
                    Return "status-pending"
                Case "completed"
                    Return "status-completed"
                Case "on hold"
                    Return "status-on-hold"
                Case "cancelled"
                    Return "status-cancelled"
                Case Else
                    Return "status-default"
            End Select
        End Get
    End Property
End Class
