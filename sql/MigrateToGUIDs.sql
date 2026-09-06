-- =============================================
-- GUID Migration Script
-- Converts all INT IDENTITY primary keys to UNIQUEIDENTIFIER (GUID)
-- =============================================
-- WARNING: This is a destructive migration. Backup your database first!
-- =============================================

USE TicketSystem;
GO

PRINT 'Starting GUID migration...';
PRINT 'WARNING: This will modify all primary and foreign keys!';
GO

-- =============================================
-- STEP 1: Add new GUID columns to all tables
-- =============================================

PRINT 'Step 1: Adding new GUID columns...';
GO

-- Add GUID columns to Users table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Users') AND name = 'UserGUID')
BEGIN
    ALTER TABLE dbo.Users ADD UserGUID UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID();
    PRINT '✓ Added UserGUID to Users table';
END
GO

-- Add GUID columns to Clients table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Clients') AND name = 'ClientGUID')
BEGIN
    ALTER TABLE dbo.Clients ADD ClientGUID UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID();
    PRINT '✓ Added ClientGUID to Clients table';
END
GO

-- Add GUID columns to Status table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Status') AND name = 'StatusGUID')
BEGIN
    ALTER TABLE dbo.Status ADD StatusGUID UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID();
    PRINT '✓ Added StatusGUID to Status table';
END
GO

-- Add GUID columns to Priority table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Priority') AND name = 'PriorityGUID')
BEGIN
    ALTER TABLE dbo.Priority ADD PriorityGUID UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID();
    PRINT '✓ Added PriorityGUID to Priority table';
END
GO

-- Add GUID columns to Projects table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Projects') AND name = 'ProjectGUID')
BEGIN
    ALTER TABLE dbo.Projects ADD ProjectGUID UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID();
    PRINT '✓ Added ProjectGUID to Projects table';
END
GO

-- Add GUID columns to Tickets table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Tickets') AND name = 'TicketGUID')
BEGIN
    ALTER TABLE dbo.Tickets ADD TicketGUID UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID();
    PRINT '✓ Added TicketGUID to Tickets table';
END
GO

-- Add GUID columns to SubTickets table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SubTickets') AND name = 'SubTicketGUID')
BEGIN
    ALTER TABLE dbo.SubTickets ADD SubTicketGUID UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID();
    PRINT '✓ Added SubTicketGUID to SubTickets table';
END
GO

-- Add GUID columns to TicketFiles table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketFiles') AND name = 'FileGUID')
BEGIN
    ALTER TABLE dbo.TicketFiles ADD FileGUID UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID();
    PRINT '✓ Added FileGUID to TicketFiles table';
END
GO

-- Add GUID columns to TicketComments table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketComments') AND name = 'CommentGUID')
BEGIN
    ALTER TABLE dbo.TicketComments ADD CommentGUID UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID();
    PRINT '✓ Added CommentGUID to TicketComments table';
END
GO

-- Add GUID columns to Collaborators table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Collaborators') AND name = 'CollaboratorGUID')
BEGIN
    ALTER TABLE dbo.Collaborators ADD CollaboratorGUID UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID();
    PRINT '✓ Added CollaboratorGUID to Collaborators table';
END
GO

-- Add GUID columns to TimePunch table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TimePunch') AND name = 'PunchGUID')
BEGIN
    ALTER TABLE dbo.TimePunch ADD PunchGUID UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID();
    PRINT '✓ Added PunchGUID to TimePunch table';
END
GO

-- Add GUID columns to ChatMessages table if it exists
IF OBJECT_ID('dbo.ChatMessages', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ChatMessages') AND name = 'MessageGUID')
    BEGIN
        ALTER TABLE dbo.ChatMessages ADD MessageGUID UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID();
        PRINT '✓ Added MessageGUID to ChatMessages table';
    END
END
GO

-- Add GUID columns to TicketActivity table if it exists
IF OBJECT_ID('dbo.TicketActivity', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketActivity') AND name = 'ActivityGUID')
    BEGIN
        ALTER TABLE dbo.TicketActivity ADD ActivityGUID UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID();
        PRINT '✓ Added ActivityGUID to TicketActivity table';
    END
END
GO

-- Add GUID columns to TicketReferences table if it exists
IF OBJECT_ID('dbo.TicketReferences', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketReferences') AND name = 'ReferenceGUID')
    BEGIN
        ALTER TABLE dbo.TicketReferences ADD ReferenceGUID UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID();
        PRINT '✓ Added ReferenceGUID to TicketReferences table';
    END
END
GO

-- =============================================
-- STEP 2: Add foreign key GUID columns
-- =============================================

PRINT 'Step 2: Adding foreign key GUID columns...';
GO

-- Projects table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Projects') AND name = 'ClientGUID')
BEGIN
    ALTER TABLE dbo.Projects ADD ClientGUID UNIQUEIDENTIFIER NULL;
    UPDATE p SET p.ClientGUID = c.ClientGUID
    FROM dbo.Projects p
    INNER JOIN dbo.Clients c ON p.ClientID = c.ClientID;
    PRINT '✓ Added ClientGUID to Projects table';
END
GO

-- Tickets table - multiple foreign keys
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Tickets') AND name = 'ProjectGUID_FK')
BEGIN
    ALTER TABLE dbo.Tickets ADD ProjectGUID_FK UNIQUEIDENTIFIER NULL;
    UPDATE t SET t.ProjectGUID_FK = p.ProjectGUID
    FROM dbo.Tickets t
    INNER JOIN dbo.Projects p ON t.ProjectID = p.ProjectID;
    PRINT '✓ Added ProjectGUID_FK to Tickets table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Tickets') AND name = 'StatusGUID_FK')
BEGIN
    ALTER TABLE dbo.Tickets ADD StatusGUID_FK UNIQUEIDENTIFIER NULL;
    UPDATE t SET t.StatusGUID_FK = s.StatusGUID
    FROM dbo.Tickets t
    INNER JOIN dbo.Status s ON t.StatusID = s.StatusID;
    PRINT '✓ Added StatusGUID_FK to Tickets table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Tickets') AND name = 'PriorityGUID_FK')
BEGIN
    ALTER TABLE dbo.Tickets ADD PriorityGUID_FK UNIQUEIDENTIFIER NULL;
    UPDATE t SET t.PriorityGUID_FK = p.PriorityGUID
    FROM dbo.Tickets t
    INNER JOIN dbo.Priority p ON t.PriorityID = p.PriorityID;
    PRINT '✓ Added PriorityGUID_FK to Tickets table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Tickets') AND name = 'CreatedByGUID')
BEGIN
    ALTER TABLE dbo.Tickets ADD CreatedByGUID UNIQUEIDENTIFIER NULL;
    UPDATE t SET t.CreatedByGUID = u.UserGUID
    FROM dbo.Tickets t
    INNER JOIN dbo.Users u ON t.CreatedBy = u.UserID;
    PRINT '✓ Added CreatedByGUID to Tickets table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Tickets') AND name = 'AssignedToGUID')
BEGIN
    ALTER TABLE dbo.Tickets ADD AssignedToGUID UNIQUEIDENTIFIER NULL;
    UPDATE t SET t.AssignedToGUID = u.UserGUID
    FROM dbo.Tickets t
    LEFT JOIN dbo.Users u ON t.AssignedTo = u.UserID
    WHERE t.AssignedTo IS NOT NULL;
    PRINT '✓ Added AssignedToGUID to Tickets table';
END
GO

-- SubTickets table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SubTickets') AND name = 'ParentTicketGUID')
BEGIN
    ALTER TABLE dbo.SubTickets ADD ParentTicketGUID UNIQUEIDENTIFIER NULL;
    UPDATE st SET st.ParentTicketGUID = t.TicketGUID
    FROM dbo.SubTickets st
    INNER JOIN dbo.Tickets t ON st.ParentTicketID = t.TicketID;
    PRINT '✓ Added ParentTicketGUID to SubTickets table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SubTickets') AND name = 'StatusGUID_FK')
BEGIN
    ALTER TABLE dbo.SubTickets ADD StatusGUID_FK UNIQUEIDENTIFIER NULL;
    UPDATE st SET st.StatusGUID_FK = s.StatusGUID
    FROM dbo.SubTickets st
    INNER JOIN dbo.Status s ON st.StatusID = s.StatusID;
    PRINT '✓ Added StatusGUID_FK to SubTickets table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SubTickets') AND name = 'PriorityGUID_FK')
BEGIN
    ALTER TABLE dbo.SubTickets ADD PriorityGUID_FK UNIQUEIDENTIFIER NULL;
    UPDATE st SET st.PriorityGUID_FK = p.PriorityGUID
    FROM dbo.SubTickets st
    INNER JOIN dbo.Priority p ON st.PriorityID = p.PriorityID;
    PRINT '✓ Added PriorityGUID_FK to SubTickets table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SubTickets') AND name = 'CreatedByGUID')
BEGIN
    ALTER TABLE dbo.SubTickets ADD CreatedByGUID UNIQUEIDENTIFIER NULL;
    UPDATE st SET st.CreatedByGUID = u.UserGUID
    FROM dbo.SubTickets st
    INNER JOIN dbo.Users u ON st.CreatedBy = u.UserID;
    PRINT '✓ Added CreatedByGUID to SubTickets table';
END
GO

-- TicketFiles table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketFiles') AND name = 'TicketGUID_FK')
BEGIN
    ALTER TABLE dbo.TicketFiles ADD TicketGUID_FK UNIQUEIDENTIFIER NULL;
    UPDATE tf SET tf.TicketGUID_FK = t.TicketGUID
    FROM dbo.TicketFiles tf
    INNER JOIN dbo.Tickets t ON tf.TicketID = t.TicketID;
    PRINT '✓ Added TicketGUID_FK to TicketFiles table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketFiles') AND name = 'UploadedByGUID')
BEGIN
    ALTER TABLE dbo.TicketFiles ADD UploadedByGUID UNIQUEIDENTIFIER NULL;
    UPDATE tf SET tf.UploadedByGUID = u.UserGUID
    FROM dbo.TicketFiles tf
    INNER JOIN dbo.Users u ON tf.UploadedBy = u.UserID;
    PRINT '✓ Added UploadedByGUID to TicketFiles table';
END
GO

-- TicketComments table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketComments') AND name = 'TicketGUID_FK')
BEGIN
    ALTER TABLE dbo.TicketComments ADD TicketGUID_FK UNIQUEIDENTIFIER NULL;
    UPDATE tc SET tc.TicketGUID_FK = t.TicketGUID
    FROM dbo.TicketComments tc
    INNER JOIN dbo.Tickets t ON tc.TicketID = t.TicketID;
    PRINT '✓ Added TicketGUID_FK to TicketComments table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketComments') AND name = 'CreatedByGUID')
BEGIN
    ALTER TABLE dbo.TicketComments ADD CreatedByGUID UNIQUEIDENTIFIER NULL;
    UPDATE tc SET tc.CreatedByGUID = u.UserGUID
    FROM dbo.TicketComments tc
    INNER JOIN dbo.Users u ON tc.CreatedBy = u.UserID;
    PRINT '✓ Added CreatedByGUID to TicketComments table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketComments') AND name = 'RecipientUserGUID')
BEGIN
    ALTER TABLE dbo.TicketComments ADD RecipientUserGUID UNIQUEIDENTIFIER NULL;
    UPDATE tc SET tc.RecipientUserGUID = u.UserGUID
    FROM dbo.TicketComments tc
    LEFT JOIN dbo.Users u ON tc.RecipientUserID = u.UserID
    WHERE tc.RecipientUserID IS NOT NULL;
    PRINT '✓ Added RecipientUserGUID to TicketComments table';
END
GO

-- Collaborators table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Collaborators') AND name = 'TicketGUID_FK')
BEGIN
    ALTER TABLE dbo.Collaborators ADD TicketGUID_FK UNIQUEIDENTIFIER NULL;
    UPDATE c SET c.TicketGUID_FK = t.TicketGUID
    FROM dbo.Collaborators c
    INNER JOIN dbo.Tickets t ON c.TicketID = t.TicketID;
    PRINT '✓ Added TicketGUID_FK to Collaborators table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Collaborators') AND name = 'UserGUID_FK')
BEGIN
    ALTER TABLE dbo.Collaborators ADD UserGUID_FK UNIQUEIDENTIFIER NULL;
    UPDATE c SET c.UserGUID_FK = u.UserGUID
    FROM dbo.Collaborators c
    INNER JOIN dbo.Users u ON c.UserID = u.UserID;
    PRINT '✓ Added UserGUID_FK to Collaborators table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Collaborators') AND name = 'AddedByGUID')
BEGIN
    ALTER TABLE dbo.Collaborators ADD AddedByGUID UNIQUEIDENTIFIER NULL;
    UPDATE c SET c.AddedByGUID = u.UserGUID
    FROM dbo.Collaborators c
    INNER JOIN dbo.Users u ON c.AddedBy = u.UserID;
    PRINT '✓ Added AddedByGUID to Collaborators table';
END
GO

-- TimePunch table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TimePunch') AND name = 'TicketGUID_FK')
BEGIN
    ALTER TABLE dbo.TimePunch ADD TicketGUID_FK UNIQUEIDENTIFIER NULL;
    UPDATE tp SET tp.TicketGUID_FK = t.TicketGUID
    FROM dbo.TimePunch tp
    INNER JOIN dbo.Tickets t ON tp.TicketID = t.TicketID;
    PRINT '✓ Added TicketGUID_FK to TimePunch table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TimePunch') AND name = 'UserGUID_FK')
BEGIN
    ALTER TABLE dbo.TimePunch ADD UserGUID_FK UNIQUEIDENTIFIER NULL;
    UPDATE tp SET tp.UserGUID_FK = u.UserGUID
    FROM dbo.TimePunch tp
    INNER JOIN dbo.Users u ON tp.UserID = u.UserID;
    PRINT '✓ Added UserGUID_FK to TimePunch table';
END
GO

-- TicketActivity table (if exists)
IF OBJECT_ID('dbo.TicketActivity', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketActivity') AND name = 'TicketGUID_FK')
    BEGIN
        ALTER TABLE dbo.TicketActivity ADD TicketGUID_FK UNIQUEIDENTIFIER NULL;
        UPDATE ta SET ta.TicketGUID_FK = t.TicketGUID
        FROM dbo.TicketActivity ta
        INNER JOIN dbo.Tickets t ON ta.TicketID = t.TicketID;
        PRINT '✓ Added TicketGUID_FK to TicketActivity table';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketActivity') AND name = 'UserGUID_FK')
    BEGIN
        ALTER TABLE dbo.TicketActivity ADD UserGUID_FK UNIQUEIDENTIFIER NULL;
        UPDATE ta SET ta.UserGUID_FK = u.UserGUID
        FROM dbo.TicketActivity ta
        INNER JOIN dbo.Users u ON ta.UserID = u.UserID;
        PRINT '✓ Added UserGUID_FK to TicketActivity table';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketActivity') AND name = 'CommentGUID_FK')
    BEGIN
        ALTER TABLE dbo.TicketActivity ADD CommentGUID_FK UNIQUEIDENTIFIER NULL;
        UPDATE ta SET ta.CommentGUID_FK = tc.CommentGUID
        FROM dbo.TicketActivity ta
        LEFT JOIN dbo.TicketComments tc ON ta.CommentID = tc.CommentID
        WHERE ta.CommentID IS NOT NULL;
        PRINT '✓ Added CommentGUID_FK to TicketActivity table';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketActivity') AND name = 'FileGUID_FK')
    BEGIN
        ALTER TABLE dbo.TicketActivity ADD FileGUID_FK UNIQUEIDENTIFIER NULL;
        UPDATE ta SET ta.FileGUID_FK = tf.FileGUID
        FROM dbo.TicketActivity ta
        LEFT JOIN dbo.TicketFiles tf ON ta.FileID = tf.FileID
        WHERE ta.FileID IS NOT NULL;
        PRINT '✓ Added FileGUID_FK to TicketActivity table';
    END
END
GO

-- TicketReferences table (if exists)
IF OBJECT_ID('dbo.TicketReferences', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketReferences') AND name = 'TicketGUID_FK')
    BEGIN
        ALTER TABLE dbo.TicketReferences ADD TicketGUID_FK UNIQUEIDENTIFIER NULL;
        UPDATE tr SET tr.TicketGUID_FK = t.TicketGUID
        FROM dbo.TicketReferences tr
        INNER JOIN dbo.Tickets t ON tr.TicketID = t.TicketID;
        PRINT '✓ Added TicketGUID_FK to TicketReferences table';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketReferences') AND name = 'ReferencedTicketGUID')
    BEGIN
        ALTER TABLE dbo.TicketReferences ADD ReferencedTicketGUID UNIQUEIDENTIFIER NULL;
        UPDATE tr SET tr.ReferencedTicketGUID = t.TicketGUID
        FROM dbo.TicketReferences tr
        INNER JOIN dbo.Tickets t ON tr.ReferencedTicketID = t.TicketID;
        PRINT '✓ Added ReferencedTicketGUID to TicketReferences table';
    END
END
GO

PRINT 'GUID migration columns created successfully!';
PRINT '';
PRINT 'NEXT STEPS:';
PRINT '1. Review the new GUID columns';
PRINT '2. Run MigrateToGUIDs_Part2.sql to complete the migration';
PRINT '3. Update all application code to use GUIDs instead of INTs';
PRINT '';
PRINT 'WARNING: Do not drop INT columns until application code is updated!';
GO
