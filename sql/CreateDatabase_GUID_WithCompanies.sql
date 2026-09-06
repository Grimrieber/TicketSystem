-- =============================================
-- AI-Asana Replacement - Database Schema (GUID VERSION WITH COMPANIES)
-- VB.NET WebForms Ticket System
-- SQL Server Standard/Enterprise
-- .NET Framework 4.8
-- ALL IDs ARE UNIQUEIDENTIFIER (GUID) EXCEPT CompanyID (INT)
-- =============================================

USE master;
GO

-- Drop and recreate database
IF EXISTS (SELECT name FROM sys.databases WHERE name = N'TicketSystem')
BEGIN
    ALTER DATABASE TicketSystem SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE TicketSystem;
END
GO

CREATE DATABASE TicketSystem;
GO

USE TicketSystem;
GO

-- =============================================
-- Create Tables
-- =============================================

-- Companies Table (Parent organization - uses INT for PK)
CREATE TABLE dbo.Companies (
    CompanyID INT IDENTITY(1,1) PRIMARY KEY,
    CompanyName NVARCHAR(200) NOT NULL,
    ShortName NVARCHAR(10) NULL, -- For ticket numbering prefix (e.g., "ACME")
    ContactEmail NVARCHAR(255) NULL,
    ContactPhone NVARCHAR(50) NULL,
    Address NVARCHAR(500) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedOn DATETIME NOT NULL DEFAULT GETDATE()
);
GO

-- Users Table
CREATE TABLE dbo.Users (
    UserID UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyID INT NOT NULL, -- Link to parent company
    Username NVARCHAR(100) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    FirstName NVARCHAR(100) NULL,
    LastName NVARCHAR(100) NULL,
    Role NVARCHAR(50) NOT NULL CHECK (Role IN ('Admin', 'Worker', 'Client', 'Collaborator')),
    Email NVARCHAR(255) NOT NULL,
    ClientID UNIQUEIDENTIFIER NULL, -- Link to client record if user is a client
    CreatedOn DATETIME NOT NULL DEFAULT GETDATE(),
    IsActive BIT NOT NULL DEFAULT 1,
    LastLoginOn DATETIME NULL,
    MustResetPassword BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_Users_Companies FOREIGN KEY (CompanyID) REFERENCES dbo.Companies(CompanyID)
);
GO

-- Clients Table (Client records within a company)
CREATE TABLE dbo.Clients (
    ClientID UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyID INT NOT NULL, -- Link to parent company
    ClientName NVARCHAR(200) NOT NULL, -- Changed from CompanyName to ClientName for clarity
    ShortName NVARCHAR(10) NULL, -- For ticket numbering (e.g., "DEPT-A")
    Billable BIT NOT NULL DEFAULT 1,
    ContactEmail NVARCHAR(255) NULL,
    ContactPhone NVARCHAR(50) NULL,
    CreatedOn DATETIME NOT NULL DEFAULT GETDATE(),
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_Clients_Companies FOREIGN KEY (CompanyID) REFERENCES dbo.Companies(CompanyID)
);
GO

-- Status Lookup Table
CREATE TABLE dbo.Status (
    StatusID UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(50) NOT NULL UNIQUE,
    DisplayOrder INT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1
);
GO

-- Priority Lookup Table
CREATE TABLE dbo.Priority (
    PriorityID UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(50) NOT NULL UNIQUE,
    DisplayOrder INT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1
);
GO

-- Projects Table
CREATE TABLE dbo.Projects (
    ProjectID UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(200) NOT NULL,
    ClientID UNIQUEIDENTIFIER NOT NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Active',
    Description NVARCHAR(MAX) NULL,
    CreatedOn DATETIME NOT NULL DEFAULT GETDATE(),
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_Projects_Clients FOREIGN KEY (ClientID) REFERENCES dbo.Clients(ClientID)
);
GO

-- Tickets Table
CREATE TABLE dbo.Tickets (
    TicketID UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TicketNumber NVARCHAR(50) NULL, -- Company-scoped number (e.g., "ACME-001")
    Subject NVARCHAR(255) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    ProjectID UNIQUEIDENTIFIER NOT NULL,
    StatusID UNIQUEIDENTIFIER NOT NULL,
    PriorityID UNIQUEIDENTIFIER NOT NULL,
    DueDate DATETIME NULL,
    ProjectedHours DECIMAL(10,2) NULL,
    CreatedBy UNIQUEIDENTIFIER NOT NULL,
    AssignedTo UNIQUEIDENTIFIER NULL,
    CreatedOn DATETIME NOT NULL DEFAULT GETDATE(),
    UpdatedOn DATETIME NOT NULL DEFAULT GETDATE(),
    CompletedOn DATETIME NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_Tickets_Projects FOREIGN KEY (ProjectID) REFERENCES dbo.Projects(ProjectID),
    CONSTRAINT FK_Tickets_Status FOREIGN KEY (StatusID) REFERENCES dbo.Status(StatusID),
    CONSTRAINT FK_Tickets_Priority FOREIGN KEY (PriorityID) REFERENCES dbo.Priority(PriorityID),
    CONSTRAINT FK_Tickets_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(UserID),
    CONSTRAINT FK_Tickets_AssignedTo FOREIGN KEY (AssignedTo) REFERENCES dbo.Users(UserID)
);
GO

-- SubTickets Table
CREATE TABLE dbo.SubTickets (
    SubTicketID UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ParentTicketID UNIQUEIDENTIFIER NOT NULL,
    Subject NVARCHAR(255) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    StatusID UNIQUEIDENTIFIER NOT NULL,
    PriorityID UNIQUEIDENTIFIER NOT NULL,
    CreatedBy UNIQUEIDENTIFIER NOT NULL,
    CreatedOn DATETIME NOT NULL DEFAULT GETDATE(),
    CompletedOn DATETIME NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_SubTickets_Tickets FOREIGN KEY (ParentTicketID) REFERENCES dbo.Tickets(TicketID) ON DELETE CASCADE,
    CONSTRAINT FK_SubTickets_Status FOREIGN KEY (StatusID) REFERENCES dbo.Status(StatusID),
    CONSTRAINT FK_SubTickets_Priority FOREIGN KEY (PriorityID) REFERENCES dbo.Priority(PriorityID),
    CONSTRAINT FK_SubTickets_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(UserID)
);
GO

-- TicketFiles Table
CREATE TABLE dbo.TicketFiles (
    FileID UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TicketID UNIQUEIDENTIFIER NOT NULL,
    FileType NVARCHAR(50) NOT NULL CHECK (FileType IN ('Screenshot', 'Video', 'Audio', 'Document', 'Other')),
    FilePath NVARCHAR(500) NOT NULL,
    OriginalFileName NVARCHAR(255) NOT NULL,
    FileSize BIGINT NOT NULL,
    UploadedBy UNIQUEIDENTIFIER NOT NULL,
    Timestamp DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_TicketFiles_Tickets FOREIGN KEY (TicketID) REFERENCES dbo.Tickets(TicketID) ON DELETE CASCADE,
    CONSTRAINT FK_TicketFiles_UploadedBy FOREIGN KEY (UploadedBy) REFERENCES dbo.Users(UserID)
);
GO

-- TicketComments Table
CREATE TABLE dbo.TicketComments (
    CommentID UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TicketID UNIQUEIDENTIFIER NOT NULL,
    CommentText NVARCHAR(MAX) NOT NULL,
    CreatedBy UNIQUEIDENTIFIER NOT NULL,
    RecipientUserID UNIQUEIDENTIFIER NULL,
    Timestamp DATETIME NOT NULL DEFAULT GETDATE(),
    Private BIT NOT NULL DEFAULT 0,
    IsEdited BIT NOT NULL DEFAULT 0,
    EditedOn DATETIME NULL,
    CONSTRAINT FK_TicketComments_Tickets FOREIGN KEY (TicketID) REFERENCES dbo.Tickets(TicketID) ON DELETE CASCADE,
    CONSTRAINT FK_TicketComments_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(UserID),
    CONSTRAINT FK_TicketComments_RecipientUserID FOREIGN KEY (RecipientUserID) REFERENCES dbo.Users(UserID)
);
GO

-- Collaborators Table (Junction table - NO CollaboratorID, just UserID)
CREATE TABLE dbo.Collaborators (
    TicketID UNIQUEIDENTIFIER NOT NULL,
    UserID UNIQUEIDENTIFIER NOT NULL, -- Links to Users.UserID
    AddedBy UNIQUEIDENTIFIER NOT NULL,
    AddedOn DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT PK_Collaborators PRIMARY KEY (TicketID, UserID),
    CONSTRAINT FK_Collaborators_Tickets FOREIGN KEY (TicketID) REFERENCES dbo.Tickets(TicketID) ON DELETE CASCADE,
    CONSTRAINT FK_Collaborators_UserID FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID),
    CONSTRAINT FK_Collaborators_AddedBy FOREIGN KEY (AddedBy) REFERENCES dbo.Users(UserID)
);
GO

-- TimePunch Table
CREATE TABLE dbo.TimePunch (
    PunchID UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TicketID UNIQUEIDENTIFIER NOT NULL,
    UserID UNIQUEIDENTIFIER NOT NULL,
    ClockIn DATETIME NOT NULL,
    ClockOut DATETIME NULL,
    CONSTRAINT FK_TimePunch_Tickets FOREIGN KEY (TicketID) REFERENCES dbo.Tickets(TicketID),
    CONSTRAINT FK_TimePunch_UserID FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID)
);
GO

-- ChatMessages Table (Optional - for SignalR chat)
CREATE TABLE dbo.ChatMessages (
    MessageID UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TicketID UNIQUEIDENTIFIER NOT NULL,
    UserID UNIQUEIDENTIFIER NOT NULL,
    Message NVARCHAR(MAX) NOT NULL,
    Timestamp DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_ChatMessages_Tickets FOREIGN KEY (TicketID) REFERENCES dbo.Tickets(TicketID),
    CONSTRAINT FK_ChatMessages_UserID FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID)
);
GO

-- APIRequests Table (For tracking external API submissions)
CREATE TABLE dbo.APIRequests (
    RequestID UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ClientID UNIQUEIDENTIFIER NOT NULL,
    RequestData NVARCHAR(MAX) NULL,
    CreatedTicketID UNIQUEIDENTIFIER NULL,
    Timestamp DATETIME NOT NULL DEFAULT GETDATE(),
    IPAddress NVARCHAR(50) NULL,
    CONSTRAINT FK_APIRequests_Clients FOREIGN KEY (ClientID) REFERENCES dbo.Clients(ClientID),
    CONSTRAINT FK_APIRequests_Tickets FOREIGN KEY (CreatedTicketID) REFERENCES dbo.Tickets(TicketID)
);
GO

-- TicketActivity Table (Unified activity stream)
CREATE TABLE dbo.TicketActivity (
    ActivityID UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TicketID UNIQUEIDENTIFIER NOT NULL,
    UserID UNIQUEIDENTIFIER NOT NULL,
    ActivityType NVARCHAR(50) NOT NULL, -- 'Comment', 'FileUpload', 'StatusChange', 'Assignment', etc.
    ActivityText NVARCHAR(MAX) NULL,
    CommentID UNIQUEIDENTIFIER NULL,
    FileID UNIQUEIDENTIFIER NULL,
    CreatedOn DATETIME NOT NULL DEFAULT GETDATE(),
    IsPrivate BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_TicketActivity_Tickets FOREIGN KEY (TicketID) REFERENCES dbo.Tickets(TicketID) ON DELETE CASCADE,
    CONSTRAINT FK_TicketActivity_UserID FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID),
    CONSTRAINT FK_TicketActivity_CommentID FOREIGN KEY (CommentID) REFERENCES dbo.TicketComments(CommentID),
    CONSTRAINT FK_TicketActivity_FileID FOREIGN KEY (FileID) REFERENCES dbo.TicketFiles(FileID)
);
GO

-- TicketReferences Table (Link tickets together)
CREATE TABLE dbo.TicketReferences (
    ReferenceID UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TicketID UNIQUEIDENTIFIER NOT NULL,
    ReferencedTicketID UNIQUEIDENTIFIER NOT NULL,
    CreatedBy UNIQUEIDENTIFIER NOT NULL,
    Notes NVARCHAR(500) NULL,
    CreatedOn DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_TicketReferences_Tickets FOREIGN KEY (TicketID) REFERENCES dbo.Tickets(TicketID),
    CONSTRAINT FK_TicketReferences_Referenced FOREIGN KEY (ReferencedTicketID) REFERENCES dbo.Tickets(TicketID),
    CONSTRAINT FK_TicketReferences_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(UserID),
    CONSTRAINT UQ_TicketReferences UNIQUE (TicketID, ReferencedTicketID)
);
GO

-- =============================================
-- Create Indexes for Performance
-- =============================================

CREATE NONCLUSTERED INDEX IX_Users_CompanyID ON dbo.Users(CompanyID);
CREATE NONCLUSTERED INDEX IX_Users_ClientID ON dbo.Users(ClientID);
CREATE NONCLUSTERED INDEX IX_Clients_CompanyID ON dbo.Clients(CompanyID);
CREATE NONCLUSTERED INDEX IX_Projects_ClientID ON dbo.Projects(ClientID);
CREATE NONCLUSTERED INDEX IX_Tickets_ProjectID ON dbo.Tickets(ProjectID);
CREATE NONCLUSTERED INDEX IX_Tickets_StatusID ON dbo.Tickets(StatusID);
CREATE NONCLUSTERED INDEX IX_Tickets_AssignedTo ON dbo.Tickets(AssignedTo);
CREATE NONCLUSTERED INDEX IX_Tickets_CreatedBy ON dbo.Tickets(CreatedBy);
CREATE NONCLUSTERED INDEX IX_TicketComments_TicketID ON dbo.TicketComments(TicketID);
CREATE NONCLUSTERED INDEX IX_TicketFiles_TicketID ON dbo.TicketFiles(TicketID);
CREATE NONCLUSTERED INDEX IX_Collaborators_UserID ON dbo.Collaborators(UserID);
CREATE NONCLUSTERED INDEX IX_TimePunch_TicketID ON dbo.TimePunch(TicketID);
CREATE NONCLUSTERED INDEX IX_TimePunch_UserID ON dbo.TimePunch(UserID);
CREATE NONCLUSTERED INDEX IX_TicketActivity_TicketID ON dbo.TicketActivity(TicketID);
CREATE NONCLUSTERED INDEX IX_TicketReferences_TicketID ON dbo.TicketReferences(TicketID);
GO

-- =============================================
-- Insert Default Data
-- =============================================

-- Insert default company
INSERT INTO dbo.Companies (CompanyName, ShortName, ContactEmail) VALUES
('Default Company', 'DEF', 'contact@defaultcompany.com');
GO

-- Insert default statuses
INSERT INTO dbo.Status (StatusID, Name, DisplayOrder) VALUES
(NEWID(), 'Open', 1),
(NEWID(), 'In Progress', 2),
(NEWID(), 'Pending Review', 3),
(NEWID(), 'On Hold', 4),
(NEWID(), 'Completed', 5),
(NEWID(), 'Closed', 6);
GO

-- Insert default priorities
INSERT INTO dbo.Priority (PriorityID, Name, DisplayOrder) VALUES
(NEWID(), 'Low', 1),
(NEWID(), 'Medium', 2),
(NEWID(), 'High', 3),
(NEWID(), 'Critical', 4);
GO

-- Create the first administrator.
--
-- No default account is seeded. Seeding one means shipping a known username and
-- password, and installations that never change it stay open on credentials that
-- are public knowledge. Generate a BCrypt hash (work factor 11) for a password you
-- choose and insert it here before first use.
--
-- INSERT INTO dbo.Users (UserID, CompanyID, Username, PasswordHash, Role, Email)
-- VALUES (NEWID(), 1, '<username>', '<bcrypt-hash>', 'Admin', '<email>');
GO

PRINT '✅ Database created successfully with Companies table!';
PRINT '';
PRINT 'Schema highlights:';
PRINT '- Companies table: INT IDENTITY primary key (CompanyID)';
PRINT '- Users table: CompanyID FK to Companies';
PRINT '- Clients table: CompanyID FK to Companies, renamed CompanyName to ClientName';
PRINT '- Collaborators table: Composite PK (TicketID, UserID) - NO CollaboratorID';
PRINT '- All other IDs are UNIQUEIDENTIFIER (GUID)';
PRINT '';
PRINT 'Default data inserted:';
PRINT '- Default Company (CompanyID = 1)';
PRINT '- 6 Statuses (Open, In Progress, Pending Review, On Hold, Completed, Closed)';
PRINT '- 4 Priorities (Low, Medium, High, Critical)';
PRINT '- Admin user with password "<your password>"';
PRINT '';
PRINT 'Login credentials:';
PRINT '  Username: admin';
PRINT '';
PRINT 'Next steps:';
PRINT '1. Update VB.NET code connection string in Web.config';
PRINT '2. Build and run the application';
PRINT '3. Test login at /Login.aspx';
PRINT '4. Verify company filtering works';
GO
