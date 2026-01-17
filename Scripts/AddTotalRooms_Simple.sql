-- Simple script to add TotalRooms column
-- Run this script directly in SQL Server Management Studio or using sqlcmd

-- Step 1: Add TotalRooms column as nullable
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TravelPackages]') AND name = 'TotalRooms')
BEGIN
    ALTER TABLE [TravelPackages]
    ADD [TotalRooms] INT NULL;
    
    -- Step 2: Set default value for existing records
    UPDATE [TravelPackages]
    SET [TotalRooms] = CASE 
        WHEN [AvailableRooms] > 0 THEN [AvailableRooms]
        ELSE 1
    END;
    
    -- Step 3: Make it NOT NULL
    ALTER TABLE [TravelPackages]
    ALTER COLUMN [TotalRooms] INT NOT NULL;
    
    -- Step 4: Add default constraint
    ALTER TABLE [TravelPackages]
    ADD CONSTRAINT DF_TravelPackages_TotalRooms DEFAULT 1 FOR [TotalRooms];
    
    PRINT 'TotalRooms column added successfully';
END
ELSE
BEGIN
    PRINT 'TotalRooms column already exists';
END

-- Step 5: Add check constraint (drop if exists first)
IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_TravelPackage_AvailableRooms')
BEGIN
    ALTER TABLE [TravelPackages]
    DROP CONSTRAINT CK_TravelPackage_AvailableRooms;
END

ALTER TABLE [TravelPackages]
ADD CONSTRAINT CK_TravelPackage_AvailableRooms 
CHECK ([AvailableRooms] >= 0 AND [AvailableRooms] <= [TotalRooms]);

PRINT 'Check constraint added successfully';
