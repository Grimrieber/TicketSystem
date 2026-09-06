Imports System
Imports System.Data

''' <summary>
''' Comment Model - Represents a comment on a ticket
''' </summary>
Public Class Comment
    Public Property CommentID As Integer
    Public Property TicketID As Integer
    Public Property CommentText As String
    Public Property CreatedBy As Integer
    Public Property RecipientUserID As Integer?
    Public Property Timestamp As DateTime
    Public Property [Private] As Boolean
    Public Property IsEdited As Boolean
    Public Property EditedOn As DateTime?

    ' Navigation properties
    Public Property CreatedByName As String

    ''' <summary>
    ''' Creates a Comment object from a DataRow
    ''' </summary>
    Public Shared Function FromDataRow(row As DataRow) As Comment
        If row Is Nothing Then Return Nothing

        Return New Comment With {
            .CommentID = Convert.ToInt32(row("CommentID")),
            .TicketID = Convert.ToInt32(row("TicketID")),
            .CommentText = row("CommentText").ToString(),
            .CreatedBy = Convert.ToInt32(row("CreatedBy")),
            .RecipientUserID = If(IsDBNull(row("RecipientUserID")), Nothing, Convert.ToInt32(row("RecipientUserID"))),
            .Timestamp = Convert.ToDateTime(row("Timestamp")),
            .Private = Convert.ToBoolean(row("Private")),
            .IsEdited = Convert.ToBoolean(row("IsEdited")),
            .EditedOn = If(IsDBNull(row("EditedOn")), Nothing, Convert.ToDateTime(row("EditedOn"))),
            .CreatedByName = If(row.Table.Columns.Contains("CreatedByName") AndAlso Not IsDBNull(row("CreatedByName")),
                              row("CreatedByName").ToString(), Nothing)
        }
    End Function

    ''' <summary>
    ''' Gets formatted timestamp for display
    ''' </summary>
    Public ReadOnly Property FormattedTimestamp As String
        Get
            Dim timeSpan As TimeSpan = DateTime.Now - Timestamp

            If timeSpan.TotalMinutes < 1 Then
                Return "Just now"
            ElseIf timeSpan.TotalMinutes < 60 Then
                Return String.Format("{0} minute{1} ago", Math.Floor(timeSpan.TotalMinutes), If(Math.Floor(timeSpan.TotalMinutes) = 1, "", "s"))
            ElseIf timeSpan.TotalHours < 24 Then
                Return String.Format("{0} hour{1} ago", Math.Floor(timeSpan.TotalHours), If(Math.Floor(timeSpan.TotalHours) = 1, "", "s"))
            ElseIf timeSpan.TotalDays < 7 Then
                Return String.Format("{0} day{1} ago", Math.Floor(timeSpan.TotalDays), If(Math.Floor(timeSpan.TotalDays) = 1, "", "s"))
            Else
                Return Timestamp.ToString("MMM dd, yyyy 'at' h:mm tt")
            End If
        End Get
    End Property
End Class
