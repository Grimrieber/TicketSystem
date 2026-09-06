-- =============================================
-- Migration Script: Add Companies Table to Existing Database
-- Run this if you have existing data to preserve
-- =============================================

USE TicketSystem;
GO

PRINT 'Starting migration to add Companies table...';
PRINT '';

-- Step 1: Create Companies table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Companies')
BEGIN
    PRINT '1. Creating Companies table...';

    CREATE TABLE dbo.Companies (
        CompanyID INT IDENTITY(1,1) PRIMARY KEY,
        CompanyName NVARCHAR(200) NOT NULL,
        ShortName NVARCHAR(10) NULL,
        ContactEmail NVARCHAR(255) NULL,
        ContactPhone NVARCHAR(50) NULL,
        Address NVARCHAR(500) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedOn DATETIME NOT NULL DEFAULT GETDATE()
    );

    -- Insert default company
    INSERT INTO Companies (CompanyName, ShortName, ContactEmail)
    VALUES ('Default Company', 'DEF', 'contact@defaultcompany.com');

    PRINT '   ✓ Companies table created';
    PRINT '   ✓ Default company inserted (CompanyID = 1)';
    PRINT '';
END
ELSE
BEGIN
    PRINT '1. Companies table already exists - skipping';
    PRINT '';
END
GO

-- Step 2: Add CompanyID to Users table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'CompanyID')
BEGIN
    PRINT '2. Adding CompanyID column to Users table...';

    ALTER TABLE Users
    ADD CompanyID INT NULL;

    -- Set all existing users to CompanyID = 1
    UPDATE Users SET CompanyID = 1;

    -- Make it NOT NULL after setting values
    ALTER TABLE Users
    ALTER COLUMN CompanyID INT NOT NULL;

    -- Add foreign key
    ALTER TABLE Users
    ADD CONSTRAINT FK_Users_Companies FOREIGN KEY (CompanyID) REFERENCES Companies(CompanyID);

    -- Add index
    CREATE NONCLUSTERED INDEX IX_Users_CompanyID ON Users(CompanyID);

    PRINT '   ✓ CompanyID column added to Users';
    PRINT '   ✓ All existing users set to CompanyID = 1';
    PRINT '   ✓ Foreign key constraint added';
    PRINT '';
END
ELSE
BEGIN
    PRINT '2. Users.CompanyID already exists - skipping';
    PRINT '';
END
GO

-- Step 3: Add CompanyID to Clients table and rename CompanyName to ClientName
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Clients') AND name = 'CompanyID')
BEGIN
    PRINT '3. Adding CompanyID column to Clients table...';

    ALTER TABLE Clients
    ADD CompanyID INT NULL;

    -- Set all existing clients to CompanyID = 1
    UPDATE Clients SET CompanyID = 1;

    -- Make it NOT NULL after setting values
    ALTER TABLE Clients
    ALTER COLUMN CompanyID INT NOT NULL;

    -- Add foreign key
    ALTER TABLE Clients
    ADD CONSTRAINT FK_Clients_Companies FOREIGN KEY (CompanyID) REFERENCES Companies(CompanyID);

    -- Add index
    CREATE NONCLUSTERED INDEX IX_Clients_CompanyID ON Clients(CompanyID);

    PRINT '   ✓ CompanyID column added to Clients';
    PRINT '   ✓ All existing clients set to CompanyID = 1';
    PRINT '   ✓ Foreign key constraint added';
    PRINT '';
END
ELSE
BEGIN
    PRINT '3. Clients.CompanyID already exists - skipping';
    PRINT '';
END
GO

-- Step 4: Rename CompanyName to ClientName in Clients table
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Clients') AND name = 'CompanyName')
   AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Clients') AND name = 'ClientName')
BEGIN
    PRINT '4. Renaming CompanyName to ClientName in Clients table...';

    EXEC sp_rename 'Clients.CompanyName', 'ClientName', 'COLUMN';

    PRINT '   ✓ Column renamed: CompanyName → ClientName';
    PRINT '';
END
ELSE IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Clients') AND name = 'ClientName')
BEGIN
    PRINT '4. Clients.ClientName already exists - skipping rename';
    PRINT '';
END
ELSE
BEGIN
    PRINT '4. WARNING: Clients table structure unexpected';
    PRINT '';
END
GO

-- Verification
PRINT '========================================';
PRINT 'MIGRATION COMPLETE';
PRINT '========================================';
PRINT '';
PRINT 'Verification:';

-- Check Companies
SELECT COUNT(*) AS CompanyCount FROM Companies;
PRINT 'Companies in database: ' + CAST((SELECT COUNT(*) FROM Companies) AS NVARCHAR(10));

-- Check Users with CompanyID
SELECT COUNT(*) AS UserCount FROM Users WHERE CompanyID = 1;
PRINT 'Users with CompanyID = 1: ' + CAST((SELECT COUNT(*) FROM Users WHERE CompanyID = 1) AS NVARCHAR(10));

-- Check Clients with CompanyID
SELECT COUNT(*) AS ClientCount FROM Clients WHERE CompanyID = 1;
PRINT 'Clients with CompanyID = 1: ' + CAST((SELECT COUNT(*) FROM Clients WHERE CompanyID = 1) AS NVARCHAR(10));

PRINT '';
PRINT 'Next steps:';
PRINT '1. Verify admin user can log in';
PRINT '2. All users and clients now belong to "Default Company"';
PRINT '3. You can create additional companies as needed';
PRINT '';
GO
