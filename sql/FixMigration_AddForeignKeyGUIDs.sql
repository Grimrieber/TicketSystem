-- Fix Migration Part 1 - Add Missing Foreign Key GUID Columns
USE TicketSystem;
GO

SET QUOTED_IDENTIFIER ON;
GO

PRINT 'Adding missing foreign key GUID columns...';
GO

-- Projects table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Projects') AND name = 'ClientGUID')
BEGIN
    ALTER TABLE dbo.Projects ADD ClientGUID UNIQUEIDENTIFIER NULL;
    UPDATE p SET p.ClientGUID = c.ClientGUID
    FROM dbo.Projects p
    INNER JOIN dbo.Clients c ON p.ClientID = c.ClientID;
    PRINT '✓ Added and populated ClientGUID to Projects table';
END
GO

-- Tickets table - multiple foreign keys
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Tickets') AND name = 'ProjectGUID_FK')
BEGIN
    ALTER TABLE dbo.Tickets ADD ProjectGUID_FK UNIQUEIDENTIFIER NULL;
    UPDATE t SET t.ProjectGUID_FK = p.ProjectGUID
    FROM dbo.Tickets t
    INNER JOIN dbo.Projects p ON t.ProjectID = p.ProjectID;
    PRINT '✓ Added and populated ProjectGUID_FK to Tickets table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Tickets') AND name = 'StatusGUID_FK')
BEGIN
    ALTER TABLE dbo.Tickets ADD StatusGUID_FK UNIQUEIDENTIFIER NULL;
    UPDATE t SET t.StatusGUID_FK = s.StatusGUID
    FROM dbo.Tickets t
    INNER JOIN dbo.Status s ON t.StatusID = s.StatusID;
    PRINT '✓ Added and populated StatusGUID_FK to Tickets table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Tickets') AND name = 'PriorityGUID_FK')
BEGIN
    ALTER TABLE dbo.Tickets ADD PriorityGUID_FK UNIQUEIDENTIFIER NULL;
    UPDATE t SET t.PriorityGUID_FK = p.PriorityGUID
    FROM dbo.Tickets t
    INNER JOIN dbo.Priority p ON t.PriorityID = p.PriorityID;
    PRINT '✓ Added and populated PriorityGUID_FK to Tickets table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Tickets') AND name = 'CreatedByGUID')
BEGIN
    ALTER TABLE dbo.Tickets ADD CreatedByGUID UNIQUEIDENTIFIER NULL;
    UPDATE t SET t.CreatedByGUID = u.UserGUID
    FROM dbo.Tickets t
    INNER JOIN dbo.Users u ON t.CreatedBy = u.UserID;
    PRINT '✓ Added and populated CreatedByGUID to Tickets table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Tickets') AND name = 'AssignedToGUID')
BEGIN
    ALTER TABLE dbo.Tickets ADD AssignedToGUID UNIQUEIDENTIFIER NULL;
    UPDATE t SET t.AssignedToGUID = u.UserGUID
    FROM dbo.Tickets t
    LEFT JOIN dbo.Users u ON t.AssignedTo = u.UserID
    WHERE t.AssignedTo IS NOT NULL;
    PRINT '✓ Added and populated AssignedToGUID to Tickets table';
END
GO

-- SubTickets table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SubTickets') AND name = 'ParentTicketGUID')
BEGIN
    ALTER TABLE dbo.SubTickets ADD ParentTicketGUID UNIQUEIDENTIFIER NULL;
    UPDATE st SET st.ParentTicketGUID = t.TicketGUID
    FROM dbo.SubTickets st
    INNER JOIN dbo.Tickets t ON st.ParentTicketID = t.TicketID;
    PRINT '✓ Added and populated ParentTicketGUID to SubTickets table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SubTickets') AND name = 'StatusGUID_FK')
BEGIN
    ALTER TABLE dbo.SubTickets ADD StatusGUID_FK UNIQUEIDENTIFIER NULL;
    UPDATE st SET st.StatusGUID_FK = s.StatusGUID
    FROM dbo.SubTickets st
    INNER JOIN dbo.Status s ON st.StatusID = s.StatusID;
    PRINT '✓ Added and populated StatusGUID_FK to SubTickets table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SubTickets') AND name = 'PriorityGUID_FK')
BEGIN
    ALTER TABLE dbo.SubTickets ADD PriorityGUID_FK UNIQUEIDENTIFIER NULL;
    UPDATE st SET st.PriorityGUID_FK = p.PriorityGUID
    FROM dbo.SubTickets st
    INNER JOIN dbo.Priority p ON st.PriorityID = p.PriorityID;
    PRINT '✓ Added and populated PriorityGUID_FK to SubTickets table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SubTickets') AND name = 'CreatedByGUID')
BEGIN
    ALTER TABLE dbo.SubTickets ADD CreatedByGUID UNIQUEIDENTIFIER NULL;
    UPDATE st SET st.CreatedByGUID = u.UserGUID
    FROM dbo.SubTickets st
    INNER JOIN dbo.Users u ON st.CreatedBy = u.UserID;
    PRINT '✓ Added and populated CreatedByGUID to SubTickets table';
END
GO

-- TicketFiles table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketFiles') AND name = 'TicketGUID_FK')
BEGIN
    ALTER TABLE dbo.TicketFiles ADD TicketGUID_FK UNIQUEIDENTIFIER NULL;
    UPDATE tf SET tf.TicketGUID_FK = t.TicketGUID
    FROM dbo.TicketFiles tf
    INNER JOIN dbo.Tickets t ON tf.TicketID = t.TicketID;
    PRINT '✓ Added and populated TicketGUID_FK to TicketFiles table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketFiles') AND name = 'UploadedByGUID')
BEGIN
    ALTER TABLE dbo.TicketFiles ADD UploadedByGUID UNIQUEIDENTIFIER NULL;
    UPDATE tf SET tf.UploadedByGUID = u.UserGUID
    FROM dbo.TicketFiles tf
    INNER JOIN dbo.Users u ON tf.UploadedBy = u.UserID;
    PRINT '✓ Added and populated UploadedByGUID to TicketFiles table';
END
GO

-- TicketComments table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketComments') AND name = 'TicketGUID_FK')
BEGIN
    ALTER TABLE dbo.TicketComments ADD TicketGUID_FK UNIQUEIDENTIFIER NULL;
    UPDATE tc SET tc.TicketGUID_FK = t.TicketGUID
    FROM dbo.TicketComments tc
    INNER JOIN dbo.Tickets t ON tc.TicketID = t.TicketID;
    PRINT '✓ Added and populated TicketGUID_FK to TicketComments table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketComments') AND name = 'CreatedByGUID')
BEGIN
    ALTER TABLE dbo.TicketComments ADD CreatedByGUID UNIQUEIDENTIFIER NULL;
    UPDATE tc SET tc.CreatedByGUID = u.UserGUID
    FROM dbo.TicketComments tc
    INNER JOIN dbo.Users u ON tc.CreatedBy = u.UserID;
    PRINT '✓ Added and populated CreatedByGUID to TicketComments table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketComments') AND name = 'RecipientUserGUID')
BEGIN
    ALTER TABLE dbo.TicketComments ADD RecipientUserGUID UNIQUEIDENTIFIER NULL;
    UPDATE tc SET tc.RecipientUserGUID = u.UserGUID
    FROM dbo.TicketComments tc
    LEFT JOIN dbo.Users u ON tc.RecipientUserID = u.UserID
    WHERE tc.RecipientUserID IS NOT NULL;
    PRINT '✓ Added and populated RecipientUserGUID to TicketComments table';
END
GO

-- Collaborators table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Collaborators') AND name = 'TicketGUID_FK')
BEGIN
    ALTER TABLE dbo.Collaborators ADD TicketGUID_FK UNIQUEIDENTIFIER NULL;
    UPDATE c SET c.TicketGUID_FK = t.TicketGUID
    FROM dbo.Collaborators c
    INNER JOIN dbo.Tickets t ON c.TicketID = t.TicketID;
    PRINT '✓ Added and populated TicketGUID_FK to Collaborators table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Collaborators') AND name = 'UserGUID_FK')
BEGIN
    ALTER TABLE dbo.Collaborators ADD UserGUID_FK UNIQUEIDENTIFIER NULL;
    UPDATE c SET c.UserGUID_FK = u.UserGUID
    FROM dbo.Collaborators c
    INNER JOIN dbo.Users u ON c.UserID = u.UserID;
    PRINT '✓ Added and populated UserGUID_FK to Collaborators table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Collaborators') AND name = 'AddedByGUID')
BEGIN
    ALTER TABLE dbo.Collaborators ADD AddedByGUID UNIQUEIDENTIFIER NULL;
    UPDATE c SET c.AddedByGUID = u.UserGUID
    FROM dbo.Collaborators c
    INNER JOIN dbo.Users u ON c.AddedBy = u.UserID;
    PRINT '✓ Added and populated AddedByGUID to Collaborators table';
END
GO

-- TimePunch table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TimePunch') AND name = 'TicketGUID_FK')
BEGIN
    ALTER TABLE dbo.TimePunch ADD TicketGUID_FK UNIQUEIDENTIFIER NULL;
    UPDATE tp SET tp.TicketGUID_FK = t.TicketGUID
    FROM dbo.TimePunch tp
    INNER JOIN dbo.Tickets t ON tp.TicketID = t.TicketID;
    PRINT '✓ Added and populated TicketGUID_FK to TimePunch table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TimePunch') AND name = 'UserGUID_FK')
BEGIN
    ALTER TABLE dbo.TimePunch ADD UserGUID_FK UNIQUEIDENTIFIER NULL;
    UPDATE tp SET tp.UserGUID_FK = u.UserGUID
    FROM dbo.TimePunch tp
    INNER JOIN dbo.Users u ON tp.UserID = u.UserID;
    PRINT '✓ Added and populated UserGUID_FK to TimePunch table';
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
        PRINT '✓ Added and populated TicketGUID_FK to TicketActivity table';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketActivity') AND name = 'UserGUID_FK')
    BEGIN
        ALTER TABLE dbo.TicketActivity ADD UserGUID_FK UNIQUEIDENTIFIER NULL;
        UPDATE ta SET ta.UserGUID_FK = u.UserGUID
        FROM dbo.TicketActivity ta
        INNER JOIN dbo.Users u ON ta.UserID = u.UserID;
        PRINT '✓ Added and populated UserGUID_FK to TicketActivity table';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketActivity') AND name = 'CommentGUID_FK')
    BEGIN
        ALTER TABLE dbo.TicketActivity ADD CommentGUID_FK UNIQUEIDENTIFIER NULL;
        UPDATE ta SET ta.CommentGUID_FK = tc.CommentGUID
        FROM dbo.TicketActivity ta
        LEFT JOIN dbo.TicketComments tc ON ta.CommentID = tc.CommentID
        WHERE ta.CommentID IS NOT NULL;
        PRINT '✓ Added and populated CommentGUID_FK to TicketActivity table';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketActivity') AND name = 'FileGUID_FK')
    BEGIN
        ALTER TABLE dbo.TicketActivity ADD FileGUID_FK UNIQUEIDENTIFIER NULL;
        UPDATE ta SET ta.FileGUID_FK = tf.FileGUID
        FROM dbo.TicketActivity ta
        LEFT JOIN dbo.TicketFiles tf ON ta.FileID = tf.FileID
        WHERE ta.FileID IS NOT NULL;
        PRINT '✓ Added and populated FileGUID_FK to TicketActivity table';
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
        PRINT '✓ Added and populated TicketGUID_FK to TicketReferences table';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TicketReferences') AND name = 'ReferencedTicketGUID')
    BEGIN
        ALTER TABLE dbo.TicketReferences ADD ReferencedTicketGUID UNIQUEIDENTIFIER NULL;
        UPDATE tr SET tr.ReferencedTicketGUID = t.TicketGUID
        FROM dbo.TicketReferences tr
        INNER JOIN dbo.Tickets t ON tr.ReferencedTicketID = t.TicketID;
        PRINT '✓ Added and populated ReferencedTicketGUID to TicketReferences table';
    END
END
GO

PRINT '';
PRINT '=================================================';
PRINT 'All missing foreign key GUID columns added!';
PRINT '=================================================';
PRINT 'You can now run MigrateToGUIDs_Part2.sql';
GO
