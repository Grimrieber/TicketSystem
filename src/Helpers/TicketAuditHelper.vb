Imports System
Imports System.Data
Imports System.Data.SqlClient

''' <summary>
''' Shared audit-trail helpers for ticket field changes. Lives in App_Code so it can
''' be referenced from any page (Default.aspx, TicketForm.aspx, ...) — page-class
''' references don't work across pages once the root falls into NonBatch compilation.
''' </summary>
Public Class TicketAuditHelper

    ''' <summary>Resolves a UserID to a display name (FullName fallback to Username), or "Unassigned" for empty.</summary>
    Public Shared Function GetUserDisplayName(userId As Guid) As String
        If userId = Guid.Empty Then Return "Unassigned"
        Try
            Dim nameObj As Object = DatabaseHelper.ExecuteScalar(
                "SELECT CASE WHEN LTRIM(RTRIM(ISNULL(FirstName,'') + ' ' + ISNULL(LastName,''))) <> '' " &
                "THEN LTRIM(RTRIM(ISNULL(FirstName,'') + ' ' + ISNULL(LastName,''))) ELSE Username END " &
                "FROM Users WHERE UserID = @U",
                New SqlParameter("@U", SqlDbType.UniqueIdentifier) With {.Value = userId})
            If nameObj Is Nothing OrElse IsDBNull(nameObj) Then Return "(unknown user)"
            Return nameObj.ToString()
        Catch
            Return "(unknown user)"
        End Try
    End Function

    ''' <summary>Looks up the Name of a Status row by StatusID.</summary>
    Public Shared Function GetStatusName(statusId As Guid) As String
        If statusId = Guid.Empty Then Return ""
        Try
            Dim n As Object = DatabaseHelper.ExecuteScalar(
                "SELECT Name FROM Status WHERE StatusID = @S",
                New SqlParameter("@S", SqlDbType.UniqueIdentifier) With {.Value = statusId})
            If n Is Nothing OrElse IsDBNull(n) Then Return ""
            Return n.ToString()
        Catch
            Return ""
        End Try
    End Function

    ''' <summary>Looks up the Name of a Priority row by PriorityID.</summary>
    Public Shared Function GetPriorityName(priorityId As Guid) As String
        If priorityId = Guid.Empty Then Return ""
        Try
            Dim n As Object = DatabaseHelper.ExecuteScalar(
                "SELECT Name FROM Priority WHERE PriorityID = @P",
                New SqlParameter("@P", SqlDbType.UniqueIdentifier) With {.Value = priorityId})
            If n Is Nothing OrElse IsDBNull(n) Then Return ""
            Return n.ToString()
        Catch
            Return ""
        End Try
    End Function

    ''' <summary>
    ''' Maps a logical field name (matching SaveTicketField.Case) to the ActivityType
    ''' value persisted in TicketActivity. Keeps audit-trail filters consistent.
    ''' </summary>
    Public Shared Function AuditTypeForField(field As String) As String
        Select Case field.ToLower()
            Case "status"      : Return "statuschange"
            Case "priority"    : Return "prioritychange"
            Case "assignedto"  : Return "assignmentchange"
            Case "duedate"     : Return "duedatechange"
            Case "startdate"   : Return "startdatechange"
            Case "subject"     : Return "subjectchange"
            Case "description" : Return "descriptionchange"
            Case Else          : Return "fieldchange"
        End Select
    End Function

    ''' <summary>
    ''' Builds a human-readable audit text for a single field change. Returns
    ''' Nothing/"" when there's nothing meaningful to log (e.g. value didn't change).
    ''' </summary>
    Public Shared Function BuildFieldChangeAuditText(field As String, newValue As String, oldRow As DataRow) As String
        Select Case field.ToLower()
            Case "status"
                Dim newGuid As Guid
                If Not Guid.TryParse(newValue, newGuid) Then Return ""
                Dim oldGuid As Guid = Guid.Empty
                If oldRow IsNot Nothing AndAlso Not IsDBNull(oldRow("StatusID")) Then
                    Guid.TryParse(oldRow("StatusID").ToString(), oldGuid)
                End If
                If oldGuid = newGuid Then Return ""
                Dim oldName As String = If(oldGuid = Guid.Empty, "—", GetStatusName(oldGuid))
                Dim newName As String = GetStatusName(newGuid)
                Return "Changed status from " & oldName & " to " & newName

            Case "priority"
                Dim newGuid As Guid
                If Not Guid.TryParse(newValue, newGuid) Then Return ""
                Dim oldGuid As Guid = Guid.Empty
                If oldRow IsNot Nothing AndAlso Not IsDBNull(oldRow("PriorityID")) Then
                    Guid.TryParse(oldRow("PriorityID").ToString(), oldGuid)
                End If
                If oldGuid = newGuid Then Return ""
                Dim oldName As String = If(oldGuid = Guid.Empty, "—", GetPriorityName(oldGuid))
                Dim newName As String = GetPriorityName(newGuid)
                Return "Changed priority from " & oldName & " to " & newName

            Case "assignedto"
                Dim newGuid As Guid = Guid.Empty
                Guid.TryParse(newValue, newGuid)
                Dim oldGuid As Guid = Guid.Empty
                If oldRow IsNot Nothing AndAlso Not IsDBNull(oldRow("AssignedTo")) Then
                    Guid.TryParse(oldRow("AssignedTo").ToString(), oldGuid)
                End If
                If oldGuid = newGuid Then Return ""
                If oldGuid = Guid.Empty AndAlso newGuid <> Guid.Empty Then
                    Return "Assigned to " & GetUserDisplayName(newGuid)
                ElseIf oldGuid <> Guid.Empty AndAlso newGuid = Guid.Empty Then
                    Return "Unassigned (was " & GetUserDisplayName(oldGuid) & ")"
                Else
                    Return "Reassigned from " & GetUserDisplayName(oldGuid) & " to " & GetUserDisplayName(newGuid)
                End If

            Case "duedate", "startdate"
                Dim col As String = If(field.ToLower() = "duedate", "DueDate", "StartDate")
                Dim labelTitle As String = If(col = "DueDate", "Due date", "Start date")
                Dim newDt As DateTime
                Dim hasNew As Boolean = DateTime.TryParse(newValue, newDt)
                Dim hasOld As Boolean = (oldRow IsNot Nothing AndAlso Not IsDBNull(oldRow(col)))
                Dim oldDt As DateTime = If(hasOld, Convert.ToDateTime(oldRow(col)), DateTime.MinValue)
                If hasOld AndAlso hasNew AndAlso oldDt = newDt Then Return ""
                If Not hasOld AndAlso Not hasNew Then Return ""
                If hasOld AndAlso Not hasNew Then
                    Return "Cleared " & labelTitle.ToLower() & " (was " & oldDt.ToString("MM/dd/yyyy h:mm tt") & ")"
                ElseIf Not hasOld AndAlso hasNew Then
                    Return "Set " & labelTitle.ToLower() & " to " & newDt.ToString("MM/dd/yyyy h:mm tt")
                Else
                    Return "Changed " & labelTitle.ToLower() & " from " & oldDt.ToString("MM/dd/yyyy h:mm tt") & " to " & newDt.ToString("MM/dd/yyyy h:mm tt")
                End If

            Case Else
                Return ""
        End Select
    End Function

End Class
