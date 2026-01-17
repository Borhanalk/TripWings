-- Add TotalRooms column to TravelPackages table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TravelPackages]') AND name = 'TotalRooms')
BEGIN
    -- Add TotalRooms column as nullable first
    ALTER TABLE [TravelPackages]
    ADD [TotalRooms] INT NULL;
    
    -- Update existing records: set TotalRooms = AvailableRooms (or 1 if AvailableRooms is 0)
    UPDATE [TravelPackages]
    SET [TotalRooms] = CASE 
        WHEN [AvailableRooms] > 0 THEN [AvailableRooms]
        ELSE 1
    END;
    
    -- Make TotalRooms NOT NULL
    ALTER TABLE [TravelPackages]
    ALTER COLUMN [TotalRooms] INT NOT NULL;
    
    -- Add default constraint
    ALTER TABLE [TravelPackages]
    ADD CONSTRAINT DF_TravelPackages_TotalRooms DEFAULT 1 FOR [TotalRooms];
    
    -- Drop existing constraint if it exists
    IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_TravelPackage_AvailableRooms')
    BEGIN
        ALTER TABLE [TravelPackages]
        DROP CONSTRAINT CK_TravelPackage_AvailableRooms;
    END
    
    -- Add check constraint
    ALTER TABLE [TravelPackages]
    ADD CONSTRAINT CK_TravelPackage_AvailableRooms 
    CHECK ([AvailableRooms] >= 0 AND [AvailableRooms] <= [TotalRooms]);
    
    PRINT 'Successfully added TotalRooms column to TravelPackages table';
END
ELSE
BEGIN
    PRINT 'TotalRooms column already exists in TravelPackages table';
END
