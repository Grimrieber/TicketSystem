-- =============================================
-- GUID Migration Script - Part 2
-- Completes the conversion by dropping INT columns and renaming GUID columns
-- =============================================
-- WARNING: This is destructive! Ensure Part 1 ran successfully first!
-- WARNING: Ensure all application code has been updated to use GUIDs!
-- =============================================

USE TicketSystem;
GO

PRINT 'Starting GUID migration Part 2...';
PRINT 'WARNING: This will drop all INT identity columns!';
GO

-- =============================================
-- STEP 3: Drop all foreign key constraints
-- =============================================

PRINT 'Step 3: Dropping foreign key constraints...';
GO

DECLARE @SQL NVARCHAR(MAX) = '';

SELECT @SQL = @SQL + 'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + '.' +
              QUOTENAME(OBJECT_NAME(parent_object_id)) +
              ' DROP CONSTRAINT ' + QUOTENAME(name) + ';' + CHAR(13)
FROM sys.foreign_keys
WHERE referenced_object_id IN (
    OBJECT_ID('dbo.Users'),
    OBJECT_ID('dbo.Clients'),
    OBJECT_ID('dbo.Status'),
    OBJECT_ID('dbo.Priority'),
    OBJECT_ID('dbo.Projects'),
    OBJECT_ID('dbo.Tickets'),
    OBJECT_ID('dbo.SubTickets'),
    OBJECT_ID('dbo.TicketFiles'),
    OBJECT_ID('dbo.TicketComments')
) OR parent_object_id IN (
    OBJECT_ID('dbo.Projects'),
    OBJECT_ID('dbo.Tickets'),
    OBJECT_ID('dbo.SubTickets'),
    OBJECT_ID('dbo.TicketFiles'),
    OBJECT_ID('dbo.TicketComments'),
    OBJECT_ID('dbo.Collaborators'),
    OBJECT_ID('dbo.TimePunch'),
    OBJECT_ID('dbo.TicketActivity'),
    OBJECT_ID('dbo.TicketReferences')
);

IF LEN(@SQL) > 0
BEGIN
    EXEC sp_executesql @SQL;
    PRINT '✓ Dropped all foreign key constraints';
END
GO

-- =============================================
-- STEP 4: Drop primary key constraints
-- =============================================

PRINT 'Step 4: Dropping primary key constraints...';
GO

IF EXISTS (SELECT * FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('dbo.Users'))
    ALTER TABLE dbo.Users DROP CONSTRAINT PK__Users__1788CCAC0C4EDA5F;
PRINT '✓ Dropped Users PK';

IF EXISTS (SELECT * FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('dbo.Clients'))
    ALTER TABLE dbo.Clients DROP CONSTRAINT PK__Clients__E67E1A045FDEE06C;
PRINT '✓ Dropped Clients PK';

IF EXISTS (SELECT * FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('dbo.Status'))
BEGIN
    DECLARE @StatusPK NVARCHAR(255);
    SELECT @StatusPK = name FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('dbo.Status');
    EXEC('ALTER TABLE dbo.Status DROP CONSTRAINT ' + @StatusPK);
    PRINT '✓ Dropped Status PK';
END

IF EXISTS (SELECT * FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('dbo.Priority'))
BEGIN
    DECLARE @PriorityPK NVARCHAR(255);
    SELECT @PriorityPK = name FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('dbo.Priority');
    EXEC('ALTER TABLE dbo.Priority DROP CONSTRAINT ' + @PriorityPK);
    PRINT '✓ Dropped Priority PK';
END

IF EXISTS (SELECT * FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('dbo.Projects'))
BEGIN
    DECLARE @ProjectsPK NVARCHAR(255);
    SELECT @ProjectsPK = name FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('dbo.Projects');
    EXEC('ALTER TABLE dbo.Projects DROP CONSTRAINT ' + @ProjectsPK);
    PRINT '✓ Dropped Projects PK';
END

IF EXISTS (SELECT * FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('dbo.Tickets'))
BEGIN
    DECLARE @TicketsPK NVARCHAR(255);
    SELECT @TicketsPK = name FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('dbo.Tickets');
    EXEC('ALTER TABLE dbo.Tickets DROP CONSTRAINT ' + @TicketsPK);
    PRINT '✓ Dropped Tickets PK';
END

IF EXISTS (SELECT * FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('dbo.SubTickets'))
BEGIN
    DECLARE @SubTicketsPK NVARCHAR(255);
    SELECT @SubTicketsPK = name FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('dbo.SubTickets');
    EXEC('ALTER TABLE dbo.SubTickets DROP CONSTRAINT ' + @SubTicketsPK);
    PRINT '✓ Dropped SubTickets PK';
END

IF EXISTS (SELECT * FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('dbo.TicketFiles'))
BEGIN
    DECLARE @FilesPK NVARCHAR(255);
    SELECT @FilesPK = name FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('dbo.TicketFiles');
    EXEC('ALTER TABLE dbo.TicketFiles DROP CONSTRAINT ' + @FilesPK);
    PRINT '✓ Dropped TicketFiles PK';
END

IF EXISTS (SELECT * FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('dbo.TicketComments'))
BEGIN
    DECLARE @CommentsPK NVARCHAR(255);
    SELECT @CommentsPK = name FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('dbo.TicketComments');
    EXEC('ALTER TABLE dbo.TicketComments DROP CONSTRAINT ' + @CommentsPK);
    PRINT '✓ Dropped TicketComments PK';
END

IF EXISTS (SELECT * FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('dbo.Collaborators'))
BEGIN
    DECLARE @CollabPK NVARCHAR(255);
    SELECT @CollabPK = name FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('dbo.Collaborators');
    EXEC('ALTER TABLE dbo.Collaborators DROP CONSTRAINT ' + @CollabPK);
    PRINT '✓ Dropped Collaborators PK';
END

IF EXISTS (SELECT * FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('dbo.TimePunch'))
BEGIN
    DECLARE @PunchPK NVARCHAR(255);
    SELECT @PunchPK = name FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('dbo.TimePunch');
    EXEC('ALTER TABLE dbo.TimePunch DROP CONSTRAINT ' + @PunchPK);
    PRINT '✓ Dropped TimePunch PK';
END

IF OBJECT_ID('dbo.TicketActivity', 'U') IS NOT NULL AND EXISTS (SELECT * FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('dbo.TicketActivity'))
BEGIN
    DECLARE @ActivityPK NVARCHAR(255);
    SELECT @ActivityPK = name FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('dbo.TicketActivity');
    EXEC('ALTER TABLE dbo.TicketActivity DROP CONSTRAINT ' + @ActivityPK);
    PRINT '✓ Dropped TicketActivity PK';
END

IF OBJECT_ID('dbo.TicketReferences', 'U') IS NOT NULL AND EXISTS (SELECT * FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('dbo.TicketReferences'))
BEGIN
    DECLARE @RefPK NVARCHAR(255);
    SELECT @RefPK = name FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID('dbo.TicketReferences');
    EXEC('ALTER TABLE dbo.TicketReferences DROP CONSTRAINT ' + @RefPK);
    PRINT '✓ Dropped TicketReferences PK';
END
GO

-- =============================================
-- STEP 5: Drop old INT identity columns
-- =============================================

PRINT 'Step 5: Dropping old INT identity columns...';
GO

-- Drop old INT columns from all tables
ALTER TABLE dbo.Users DROP COLUMN UserID;
PRINT '✓ Dropped UserID column';

ALTER TABLE dbo.Clients DROP COLUMN ClientID;
PRINT '✓ Dropped ClientID column';

ALTER TABLE dbo.Status DROP COLUMN StatusID;
PRINT '✓ Dropped StatusID column';

ALTER TABLE dbo.Priority DROP COLUMN PriorityID;
PRINT '✓ Dropped PriorityID column';

ALTER TABLE dbo.Projects DROP COLUMN ProjectID, ClientID;
PRINT '✓ Dropped ProjectID and ClientID columns';

ALTER TABLE dbo.Tickets DROP COLUMN TicketID, ProjectID, StatusID, PriorityID, CreatedBy, AssignedTo;
PRINT '✓ Dropped Tickets INT columns';

ALTER TABLE dbo.SubTickets DROP COLUMN SubTicketID, ParentTicketID, StatusID, PriorityID, CreatedBy;
PRINT '✓ Dropped SubTickets INT columns';

ALTER TABLE dbo.TicketFiles DROP COLUMN FileID, TicketID, UploadedBy;
PRINT '✓ Dropped TicketFiles INT columns';

ALTER TABLE dbo.TicketComments DROP COLUMN CommentID, TicketID, CreatedBy, RecipientUserID;
PRINT '✓ Dropped TicketComments INT columns';

ALTER TABLE dbo.Collaborators DROP COLUMN CollaboratorID, TicketID, UserID, AddedBy;
PRINT '✓ Dropped Collaborators INT columns';

ALTER TABLE dbo.TimePunch DROP COLUMN PunchID, TicketID, UserID;
PRINT '✓ Dropped TimePunch INT columns';

IF OBJECT_ID('dbo.TicketActivity', 'U') IS NOT NULL
BEGIN
    ALTER TABLE dbo.TicketActivity DROP COLUMN ActivityID, TicketID, UserID, CommentID, FileID;
    PRINT '✓ Dropped TicketActivity INT columns';
END

IF OBJECT_ID('dbo.TicketReferences', 'U') IS NOT NULL
BEGIN
    ALTER TABLE dbo.TicketReferences DROP COLUMN ReferenceID, TicketID, ReferencedTicketID;
    PRINT '✓ Dropped TicketReferences INT columns';
END
GO

-- =============================================
-- STEP 6: Rename GUID columns to original names
-- =============================================

PRINT 'Step 6: Renaming GUID columns...';
GO

-- Rename primary key columns
EXEC sp_rename 'dbo.Users.UserGUID', 'UserID', 'COLUMN';
EXEC sp_rename 'dbo.Clients.ClientGUID', 'ClientID', 'COLUMN';
EXEC sp_rename 'dbo.Status.StatusGUID', 'StatusID', 'COLUMN';
EXEC sp_rename 'dbo.Priority.PriorityGUID', 'PriorityID', 'COLUMN';
EXEC sp_rename 'dbo.Projects.ProjectGUID', 'ProjectID', 'COLUMN';
EXEC sp_rename 'dbo.Tickets.TicketGUID', 'TicketID', 'COLUMN';
EXEC sp_rename 'dbo.SubTickets.SubTicketGUID', 'SubTicketID', 'COLUMN';
EXEC sp_rename 'dbo.TicketFiles.FileGUID', 'FileID', 'COLUMN';
EXEC sp_rename 'dbo.TicketComments.CommentGUID', 'CommentID', 'COLUMN';
EXEC sp_rename 'dbo.Collaborators.CollaboratorGUID', 'CollaboratorID', 'COLUMN';
EXEC sp_rename 'dbo.TimePunch.PunchGUID', 'PunchID', 'COLUMN';
PRINT '✓ Renamed primary key columns';

-- Rename foreign key columns
EXEC sp_rename 'dbo.Projects.ClientGUID', 'ClientID', 'COLUMN';
EXEC sp_rename 'dbo.Tickets.ProjectGUID_FK', 'ProjectID', 'COLUMN';
EXEC sp_rename 'dbo.Tickets.StatusGUID_FK', 'StatusID', 'COLUMN';
EXEC sp_rename 'dbo.Tickets.PriorityGUID_FK', 'PriorityID', 'COLUMN';
EXEC sp_rename 'dbo.Tickets.CreatedByGUID', 'CreatedBy', 'COLUMN';
EXEC sp_rename 'dbo.Tickets.AssignedToGUID', 'AssignedTo', 'COLUMN';
EXEC sp_rename 'dbo.SubTickets.ParentTicketGUID', 'ParentTicketID', 'COLUMN';
EXEC sp_rename 'dbo.SubTickets.StatusGUID_FK', 'StatusID', 'COLUMN';
EXEC sp_rename 'dbo.SubTickets.PriorityGUID_FK', 'PriorityID', 'COLUMN';
EXEC sp_rename 'dbo.SubTickets.CreatedByGUID', 'CreatedBy', 'COLUMN';
EXEC sp_rename 'dbo.TicketFiles.TicketGUID_FK', 'TicketID', 'COLUMN';
EXEC sp_rename 'dbo.TicketFiles.UploadedByGUID', 'UploadedBy', 'COLUMN';
EXEC sp_rename 'dbo.TicketComments.TicketGUID_FK', 'TicketID', 'COLUMN';
EXEC sp_rename 'dbo.TicketComments.CreatedByGUID', 'CreatedBy', 'COLUMN';
EXEC sp_rename 'dbo.TicketComments.RecipientUserGUID', 'RecipientUserID', 'COLUMN';
EXEC sp_rename 'dbo.Collaborators.TicketGUID_FK', 'TicketID', 'COLUMN';
EXEC sp_rename 'dbo.Collaborators.UserGUID_FK', 'UserID', 'COLUMN';
EXEC sp_rename 'dbo.Collaborators.AddedByGUID', 'AddedBy', 'COLUMN';
EXEC sp_rename 'dbo.TimePunch.TicketGUID_FK', 'TicketID', 'COLUMN';
EXEC sp_rename 'dbo.TimePunch.UserGUID_FK', 'UserID', 'COLUMN';
PRINT '✓ Renamed foreign key columns';

IF OBJECT_ID('dbo.TicketActivity', 'U') IS NOT NULL
BEGIN
    EXEC sp_rename 'dbo.TicketActivity.ActivityGUID', 'ActivityID', 'COLUMN';
    EXEC sp_rename 'dbo.TicketActivity.TicketGUID_FK', 'TicketID', 'COLUMN';
    EXEC sp_rename 'dbo.TicketActivity.UserGUID_FK', 'UserID', 'COLUMN';
    EXEC sp_rename 'dbo.TicketActivity.CommentGUID_FK', 'CommentID', 'COLUMN';
    EXEC sp_rename 'dbo.TicketActivity.FileGUID_FK', 'FileID', 'COLUMN';
    PRINT '✓ Renamed TicketActivity columns';
END

IF OBJECT_ID('dbo.TicketReferences', 'U') IS NOT NULL
BEGIN
    EXEC sp_rename 'dbo.TicketReferences.ReferenceGUID', 'ReferenceID', 'COLUMN';
    EXEC sp_rename 'dbo.TicketReferences.TicketGUID_FK', 'TicketID', 'COLUMN';
    EXEC sp_rename 'dbo.TicketReferences.ReferencedTicketGUID', 'ReferencedTicketID', 'COLUMN';
    PRINT '✓ Renamed TicketReferences columns';
END
GO

-- =============================================
-- STEP 7: Create new primary keys
-- =============================================

PRINT 'Step 7: Creating new primary keys...';
GO

ALTER TABLE dbo.Users ADD CONSTRAINT PK_Users PRIMARY KEY (UserID);
ALTER TABLE dbo.Clients ADD CONSTRAINT PK_Clients PRIMARY KEY (ClientID);
ALTER TABLE dbo.Status ADD CONSTRAINT PK_Status PRIMARY KEY (StatusID);
ALTER TABLE dbo.Priority ADD CONSTRAINT PK_Priority PRIMARY KEY (PriorityID);
ALTER TABLE dbo.Projects ADD CONSTRAINT PK_Projects PRIMARY KEY (ProjectID);
ALTER TABLE dbo.Tickets ADD CONSTRAINT PK_Tickets PRIMARY KEY (TicketID);
ALTER TABLE dbo.SubTickets ADD CONSTRAINT PK_SubTickets PRIMARY KEY (SubTicketID);
ALTER TABLE dbo.TicketFiles ADD CONSTRAINT PK_TicketFiles PRIMARY KEY (FileID);
ALTER TABLE dbo.TicketComments ADD CONSTRAINT PK_TicketComments PRIMARY KEY (CommentID);
ALTER TABLE dbo.Collaborators ADD CONSTRAINT PK_Collaborators PRIMARY KEY (CollaboratorID);
ALTER TABLE dbo.TimePunch ADD CONSTRAINT PK_TimePunch PRIMARY KEY (PunchID);
PRINT '✓ Created primary keys';

IF OBJECT_ID('dbo.TicketActivity', 'U') IS NOT NULL
BEGIN
    ALTER TABLE dbo.TicketActivity ADD CONSTRAINT PK_TicketActivity PRIMARY KEY (ActivityID);
    PRINT '✓ Created TicketActivity primary key';
END

IF OBJECT_ID('dbo.TicketReferences', 'U') IS NOT NULL
BEGIN
    ALTER TABLE dbo.TicketReferences ADD CONSTRAINT PK_TicketReferences PRIMARY KEY (ReferenceID);
    PRINT '✓ Created TicketReferences primary key';
END
GO

-- =============================================
-- STEP 8: Create new foreign keys
-- =============================================

PRINT 'Step 8: Creating new foreign key constraints...';
GO

-- Projects
ALTER TABLE dbo.Projects ADD CONSTRAINT FK_Projects_Clients FOREIGN KEY (ClientID) REFERENCES dbo.Clients(ClientID);

-- Tickets
ALTER TABLE dbo.Tickets ADD CONSTRAINT FK_Tickets_Projects FOREIGN KEY (ProjectID) REFERENCES dbo.Projects(ProjectID);
ALTER TABLE dbo.Tickets ADD CONSTRAINT FK_Tickets_Status FOREIGN KEY (StatusID) REFERENCES dbo.Status(StatusID);
ALTER TABLE dbo.Tickets ADD CONSTRAINT FK_Tickets_Priority FOREIGN KEY (PriorityID) REFERENCES dbo.Priority(PriorityID);
ALTER TABLE dbo.Tickets ADD CONSTRAINT FK_Tickets_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(UserID);
ALTER TABLE dbo.Tickets ADD CONSTRAINT FK_Tickets_AssignedTo FOREIGN KEY (AssignedTo) REFERENCES dbo.Users(UserID);

-- SubTickets
ALTER TABLE dbo.SubTickets ADD CONSTRAINT FK_SubTickets_Tickets FOREIGN KEY (ParentTicketID) REFERENCES dbo.Tickets(TicketID);
ALTER TABLE dbo.SubTickets ADD CONSTRAINT FK_SubTickets_Status FOREIGN KEY (StatusID) REFERENCES dbo.Status(StatusID);
ALTER TABLE dbo.SubTickets ADD CONSTRAINT FK_SubTickets_Priority FOREIGN KEY (PriorityID) REFERENCES dbo.Priority(PriorityID);
ALTER TABLE dbo.SubTickets ADD CONSTRAINT FK_SubTickets_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(UserID);

-- TicketFiles
ALTER TABLE dbo.TicketFiles ADD CONSTRAINT FK_TicketFiles_Tickets FOREIGN KEY (TicketID) REFERENCES dbo.Tickets(TicketID);
ALTER TABLE dbo.TicketFiles ADD CONSTRAINT FK_TicketFiles_UploadedBy FOREIGN KEY (UploadedBy) REFERENCES dbo.Users(UserID);

-- TicketComments
ALTER TABLE dbo.TicketComments ADD CONSTRAINT FK_TicketComments_Tickets FOREIGN KEY (TicketID) REFERENCES dbo.Tickets(TicketID);
ALTER TABLE dbo.TicketComments ADD CONSTRAINT FK_TicketComments_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(UserID);
ALTER TABLE dbo.TicketComments ADD CONSTRAINT FK_TicketComments_RecipientUserID FOREIGN KEY (RecipientUserID) REFERENCES dbo.Users(UserID);

-- Collaborators
ALTER TABLE dbo.Collaborators ADD CONSTRAINT FK_Collaborators_Tickets FOREIGN KEY (TicketID) REFERENCES dbo.Tickets(TicketID);
ALTER TABLE dbo.Collaborators ADD CONSTRAINT FK_Collaborators_UserID FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID);
ALTER TABLE dbo.Collaborators ADD CONSTRAINT FK_Collaborators_AddedBy FOREIGN KEY (AddedBy) REFERENCES dbo.Users(UserID);

-- TimePunch
ALTER TABLE dbo.TimePunch ADD CONSTRAINT FK_TimePunch_Tickets FOREIGN KEY (TicketID) REFERENCES dbo.Tickets(TicketID);
ALTER TABLE dbo.TimePunch ADD CONSTRAINT FK_TimePunch_UserID FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID);

PRINT '✓ Created foreign key constraints';

IF OBJECT_ID('dbo.TicketActivity', 'U') IS NOT NULL
BEGIN
    ALTER TABLE dbo.TicketActivity ADD CONSTRAINT FK_TicketActivity_Tickets FOREIGN KEY (TicketID) REFERENCES dbo.Tickets(TicketID);
    ALTER TABLE dbo.TicketActivity ADD CONSTRAINT FK_TicketActivity_UserID FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID);
    ALTER TABLE dbo.TicketActivity ADD CONSTRAINT FK_TicketActivity_CommentID FOREIGN KEY (CommentID) REFERENCES dbo.TicketComments(CommentID);
    ALTER TABLE dbo.TicketActivity ADD CONSTRAINT FK_TicketActivity_FileID FOREIGN KEY (FileID) REFERENCES dbo.TicketFiles(FileID);
    PRINT '✓ Created TicketActivity foreign keys';
END

IF OBJECT_ID('dbo.TicketReferences', 'U') IS NOT NULL
BEGIN
    ALTER TABLE dbo.TicketReferences ADD CONSTRAINT FK_TicketReferences_Tickets FOREIGN KEY (TicketID) REFERENCES dbo.Tickets(TicketID);
    ALTER TABLE dbo.TicketReferences ADD CONSTRAINT FK_TicketReferences_Referenced FOREIGN KEY (ReferencedTicketID) REFERENCES dbo.Tickets(TicketID);
    PRINT '✓ Created TicketReferences foreign keys';
END
GO

-- =============================================
-- STEP 9: Create indexes for performance
-- =============================================

PRINT 'Step 9: Creating indexes...';
GO

CREATE NONCLUSTERED INDEX IX_Tickets_StatusID ON dbo.Tickets(StatusID);
CREATE NONCLUSTERED INDEX IX_Tickets_AssignedTo ON dbo.Tickets(AssignedTo);
CREATE NONCLUSTERED INDEX IX_Tickets_CreatedBy ON dbo.Tickets(CreatedBy);
CREATE NONCLUSTERED INDEX IX_TicketComments_TicketID ON dbo.TicketComments(TicketID);
CREATE NONCLUSTERED INDEX IX_TicketFiles_TicketID ON dbo.TicketFiles(TicketID);
CREATE NONCLUSTERED INDEX IX_Collaborators_TicketID ON dbo.Collaborators(TicketID);
CREATE NONCLUSTERED INDEX IX_Collaborators_UserID ON dbo.Collaborators(UserID);
CREATE NONCLUSTERED INDEX IX_TimePunch_TicketID ON dbo.TimePunch(TicketID);
CREATE NONCLUSTERED INDEX IX_TimePunch_UserID ON dbo.TimePunch(UserID);
PRINT '✓ Created performance indexes';
GO

PRINT '';
PRINT '=================================================';
PRINT 'GUID migration completed successfully!';
PRINT '=================================================';
PRINT '';
PRINT 'All tables now use UNIQUEIDENTIFIER (GUID) primary keys.';
PRINT '';
PRINT 'IMPORTANT: Update your application code to use GUIDs:';
PRINT '- Change Integer to Guid in VB.NET code';
PRINT '- Update AuthHelper session storage for UserID';
PRINT '- Update all database helper methods';
PRINT '- Update all ASPX pages and code-behind files';
PRINT '';
GO
