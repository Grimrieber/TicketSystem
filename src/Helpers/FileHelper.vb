Imports System
Imports System.Data
Imports System.Configuration
Imports System.Data.SqlClient
Imports System.IO
Imports System.Web
Imports Microsoft.VisualBasic

''' <summary>
''' FileHelper - validates uploads, stores attachment bytes in TicketFiles.FileData,
''' and serves them back via the response. All file storage is in the database
''' (varbinary). Callers should never write to disk.
''' </summary>
Public Class FileHelper

#Region "Configuration"

    Private Shared ReadOnly MaxFileSizeMB As Integer = Convert.ToInt32(ConfigurationManager.AppSettings("MaxFileUploadSizeMB"))
    Private Shared ReadOnly AllowedImageExtensions As String() = ConfigurationManager.AppSettings("AllowedImageExtensions").Split(","c)
    Private Shared ReadOnly AllowedVideoExtensions As String() = ConfigurationManager.AppSettings("AllowedVideoExtensions").Split(","c)
    Private Shared ReadOnly AllowedAudioExtensions As String() = ConfigurationManager.AppSettings("AllowedAudioExtensions").Split(","c)

#End Region

#Region "File Upload"

    ''' <summary>
    ''' Reads the uploaded file into memory, classifies it, and inserts a TicketFiles row
    ''' with the bytes in FileData. Pass ticketId = Nothing for the pre-upload (TicketID NULL)
    ''' flow used by the new-ticket form, which claims the rows after the ticket is saved.
    ''' </summary>
    Public Shared Function UploadFile(file As HttpPostedFile, ticketId As Guid?, uploadedBy As Guid) As UploadResult
        Dim result As New UploadResult()

        Try
            If file Is Nothing OrElse file.ContentLength = 0 Then
                result.Success = False
                result.ErrorMessage = "No file selected."
                Return result
            End If

            If Not ValidateFileSize(file.ContentLength) Then
                result.Success = False
                result.ErrorMessage = String.Format("File size exceeds maximum allowed size of {0} MB.", MaxFileSizeMB)
                Return result
            End If

            Dim originalName As String = Path.GetFileName(file.FileName)
            Dim extension As String = Path.GetExtension(originalName).ToLower()
            Dim fileType As String = GetFileType(extension)
            Dim mimeType As String = If(String.IsNullOrEmpty(file.ContentType), GetContentType(extension), file.ContentType)

            ' Read bytes into memory
            Dim bytes(file.ContentLength - 1) As Byte
            file.InputStream.Read(bytes, 0, file.ContentLength)

            Dim fileId As Guid = DatabaseHelper.AddTicketFile(ticketId, fileType,
                                                              originalName, file.ContentLength,
                                                              mimeType, bytes, uploadedBy)

            result.Success = True
            result.FileId = fileId
            result.OriginalFileName = originalName
            result.FileType = fileType
            result.FileSize = file.ContentLength
            result.MimeType = mimeType

        Catch ex As Exception
            result.Success = False
            result.ErrorMessage = "Error uploading file: " & ex.Message
            LogError("UploadFile", ex)
        End Try

        Return result
    End Function

    ''' <summary>
    ''' Persists raw bytes (e.g. base64 inline images extracted from rich-text descriptions).
    ''' Caller supplies the original filename and (optionally) a known mime type.
    ''' </summary>
    Public Shared Function UploadBytes(bytes As Byte(), fileName As String, mimeType As String,
                                       ticketId As Guid?, uploadedBy As Guid) As UploadResult
        Dim result As New UploadResult()
        Try
            If bytes Is Nothing OrElse bytes.Length = 0 Then
                result.Success = False
                result.ErrorMessage = "No bytes to upload."
                Return result
            End If

            If Not ValidateFileSize(bytes.Length) Then
                result.Success = False
                result.ErrorMessage = String.Format("Size exceeds maximum {0} MB.", MaxFileSizeMB)
                Return result
            End If

            Dim originalName As String = Path.GetFileName(If(fileName, "upload"))
            Dim extension As String = Path.GetExtension(originalName).ToLower()
            Dim fileType As String = GetFileType(extension)
            Dim resolvedMime As String = If(String.IsNullOrEmpty(mimeType), GetContentType(extension), mimeType)

            Dim fileId As Guid = DatabaseHelper.AddTicketFile(ticketId, fileType,
                                                              originalName, bytes.Length,
                                                              resolvedMime, bytes, uploadedBy)
            result.Success = True
            result.FileId = fileId
            result.OriginalFileName = originalName
            result.FileType = fileType
            result.FileSize = bytes.Length
            result.MimeType = resolvedMime
        Catch ex As Exception
            result.Success = False
            result.ErrorMessage = "Error uploading bytes: " & ex.Message
            LogError("UploadBytes", ex)
        End Try
        Return result
    End Function

    ''' <summary>
    ''' Convenience overload for the bulk-upload case (multiple files, all attached to the same ticket).
    ''' </summary>
    Public Shared Function UploadMultipleFiles(files As HttpFileCollection, ticketId As Guid, uploadedBy As Guid) As List(Of UploadResult)
        Dim results As New List(Of UploadResult)()
        For i As Integer = 0 To files.Count - 1
            Dim file As HttpPostedFile = files(i)
            If file IsNot Nothing AndAlso file.ContentLength > 0 Then
                results.Add(UploadFile(file, ticketId, uploadedBy))
            End If
        Next
        Return results
    End Function

#End Region

#Region "File Download"

    ''' <summary>Streams the file's bytes back to the response as an attachment.</summary>
    Public Shared Sub DownloadFile(fileId As Guid)
        WriteFileToResponse(fileId, "attachment")
    End Sub

    ''' <summary>Streams the file's bytes back to the response inline (for image/PDF preview).</summary>
    Public Shared Sub DisplayFile(fileId As Guid)
        WriteFileToResponse(fileId, "inline")
    End Sub

    Private Shared Sub WriteFileToResponse(fileId As Guid, disposition As String)
        Try
            Dim row As DataRow = LoadFileRow(fileId)

            If IsDBNull(row("FileData")) Then
                Throw New FileNotFoundException("File data not found in database for FileID " & fileId.ToString())
            End If

            Dim bytes As Byte() = DirectCast(row("FileData"), Byte())
            Dim originalName As String = row("OriginalFileName").ToString()

            Dim contentType As String
            If Not IsDBNull(row("MimeType")) AndAlso Not String.IsNullOrEmpty(row("MimeType").ToString()) Then
                contentType = row("MimeType").ToString()
            Else
                contentType = GetContentType(Path.GetExtension(originalName))
            End If

            Dim response As HttpResponse = HttpContext.Current.Response
            response.Clear()
            response.ContentType = contentType
            response.AddHeader("Content-Disposition", disposition & "; filename=""" & originalName & """")
            response.AddHeader("Content-Length", bytes.Length.ToString())
            response.BinaryWrite(bytes)
            response.Flush()
            response.End()

        Catch ex As System.Threading.ThreadAbortException
            ' Response.End raises this — ignore.
            Throw
        Catch ex As Exception
            LogError(If(disposition = "inline", "DisplayFile", "DownloadFile"), ex)
            Throw New ApplicationException("Error " & disposition & " file.", ex)
        End Try
    End Sub

    Private Shared Function LoadFileRow(fileId As Guid) As DataRow
        Dim dt As DataTable = DatabaseHelper.ExecuteDataTable(
            "SELECT * FROM TicketFiles WHERE FileID = @FileID",
            New SqlParameter("@FileID", SqlDbType.UniqueIdentifier) With {.Value = fileId})

        If dt.Rows.Count = 0 Then
            Throw New FileNotFoundException("File not found in database (FileID " & fileId.ToString() & ").")
        End If
        Return dt.Rows(0)
    End Function

#End Region

#Region "File Delete"

    ''' <summary>Removes a file record from the database. No filesystem operations.</summary>
    Public Shared Function DeleteFile(fileId As Guid) As Boolean
        Try
            Dim affected As Integer = DatabaseHelper.ExecuteNonQuery(
                "DELETE FROM TicketFiles WHERE FileID = @FileID",
                New SqlParameter("@FileID", SqlDbType.UniqueIdentifier) With {.Value = fileId})
            Return affected > 0
        Catch ex As Exception
            LogError("DeleteFile", ex)
            Return False
        End Try
    End Function

#End Region

#Region "File Validation"

    Private Shared Function ValidateFileSize(contentLength As Integer) As Boolean
        Dim maxBytes As Long = CLng(MaxFileSizeMB) * 1024 * 1024
        Return contentLength <= maxBytes
    End Function

    ''' <summary>
    ''' Classifies an extension into a high-level file type. Anything not recognized as
    ''' image/video/audio falls back to "Document" so the upload is accepted.
    ''' </summary>
    Public Shared Function GetFileType(extension As String) As String
        extension = If(extension, "").ToLower()

        If AllowedImageExtensions.Contains(extension) Then
            Return "Screenshot"
        ElseIf AllowedVideoExtensions.Contains(extension) Then
            Return "Video"
        ElseIf AllowedAudioExtensions.Contains(extension) Then
            Return "Audio"
        Else
            Return "Document"
        End If
    End Function

    ''' <summary>True for any extension — kept for callers that pre-check, always returns true.</summary>
    Public Shared Function IsFileTypeAllowed(fileName As String) As Boolean
        Return Not String.IsNullOrEmpty(GetFileType(Path.GetExtension(fileName)))
    End Function

#End Region

#Region "Mime Type"

    ''' <summary>Best-effort mapping from extension to a content-type string.</summary>
    Public Shared Function GetContentType(extension As String) As String
        Select Case If(extension, "").ToLower()
            Case ".jpg", ".jpeg" : Return "image/jpeg"
            Case ".png" : Return "image/png"
            Case ".gif" : Return "image/gif"
            Case ".bmp" : Return "image/bmp"
            Case ".webp" : Return "image/webp"
            Case ".svg" : Return "image/svg+xml"
            Case ".mp4" : Return "video/mp4"
            Case ".avi" : Return "video/x-msvideo"
            Case ".mov" : Return "video/quicktime"
            Case ".wmv" : Return "video/x-ms-wmv"
            Case ".flv" : Return "video/x-flv"
            Case ".webm" : Return "video/webm"
            Case ".mp3" : Return "audio/mpeg"
            Case ".wav" : Return "audio/wav"
            Case ".ogg" : Return "audio/ogg"
            Case ".m4a" : Return "audio/mp4"
            Case ".flac" : Return "audio/flac"
            Case ".pdf" : Return "application/pdf"
            Case ".doc" : Return "application/msword"
            Case ".docx" : Return "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            Case ".xls" : Return "application/vnd.ms-excel"
            Case ".xlsx" : Return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            Case ".txt", ".md", ".log" : Return "text/plain"
            Case ".html", ".htm" : Return "text/html"
            Case ".css" : Return "text/css"
            Case ".js" : Return "application/javascript"
            Case ".json" : Return "application/json"
            Case ".xml" : Return "application/xml"
            Case ".zip" : Return "application/zip"
            Case Else : Return "application/octet-stream"
        End Select
    End Function

#End Region

#Region "Display Helpers"

    Public Shared Function FormatFileSize(bytes As Long) As String
        Const KB As Long = 1024
        Const MB As Long = KB * 1024
        Const GB As Long = MB * 1024

        If bytes >= GB Then
            Return String.Format("{0:0.##} GB", CDbl(bytes) / GB)
        ElseIf bytes >= MB Then
            Return String.Format("{0:0.##} MB", CDbl(bytes) / MB)
        ElseIf bytes >= KB Then
            Return String.Format("{0:0.##} KB", CDbl(bytes) / KB)
        Else
            Return String.Format("{0} bytes", bytes)
        End If
    End Function

#End Region

#Region "Error Logging"

    Private Shared Sub LogError(methodName As String, ex As Exception)
        Try
            Dim logPath As String = HttpContext.Current.Server.MapPath("~/App_Data/Logs/")
            If Not Directory.Exists(logPath) Then
                Directory.CreateDirectory(logPath)
            End If

            Dim logFile As String = Path.Combine(logPath, String.Format("FileErrors_{0:yyyyMMdd}.txt", DateTime.Now))
            Dim logMessage As String = String.Format("[{0:yyyy-MM-dd HH:mm:ss}] Method: {1}" & vbCrLf & "Error: {2}" & vbCrLf & "Stack: {3}" & vbCrLf & vbCrLf,
                                                    DateTime.Now, methodName, ex.Message, ex.StackTrace)

            File.AppendAllText(logFile, logMessage)
        Catch
            ' Ignore logging errors
        End Try
    End Sub

#End Region

End Class

''' <summary>Result returned from FileHelper.UploadFile.</summary>
Public Class UploadResult
    Public Property Success As Boolean
    Public Property FileId As Guid
    Public Property OriginalFileName As String
    Public Property FileType As String
    Public Property FileSize As Long
    Public Property MimeType As String
    Public Property ErrorMessage As String
End Class
