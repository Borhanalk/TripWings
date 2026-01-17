-- Add TotalRooms column to TravelPackages table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TravelPackages]') AND name = 'TotalRooms')
BEGIN
    -- Step 1: Add TotalRooms column as nullable first
    ALTER TABLE [TravelPackages]
    ADD [TotalRooms] INT NULL;
    
    PRINT 'Step 1: Added TotalRooms column as nullable';
    
    -- Step 2: Update existing records: set TotalRooms = AvailableRooms (or 1 if AvailableRooms is 0)
    UPDATE [TravelPackages]
    SET [TotalRooms] = CASE 
        WHEN [AvailableRooms] > 0 THEN [AvailableRooms]
        ELSE 1
    END;
    
    PRINT 'Step 2: Updated existing records';
    
    -- Step 3: Make TotalRooms NOT NULL
    ALTER TABLE [TravelPackages]
    ALTER COLUMN [TotalRooms] INT NOT NULL;
    
    PRINT 'Step 3: Made TotalRooms NOT NULL';
    
    -- Step 4: Add default constraint
    ALTER TABLE [TravelPackages]
    ADD CONSTRAINT DF_TravelPackages_TotalRooms DEFAULT 1 FOR [TotalRooms];
    
    PRINT 'Step 4: Added default constraint';
    
    -- Step 5: Drop existing constraint if it exists (in case it references TotalRooms)
    IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_TravelPackage_AvailableRooms')
    BEGIN
        ALTER TABLE [TravelPackages]
        DROP CONSTRAINT CK_TravelPackage_AvailableRooms;
        PRINT 'Step 5: Dropped existing constraint';
    END
    
    -- Step 6: Add check constraint
    ALTER TABLE [TravelPackages]
    ADD CONSTRAINT CK_TravelPackage_AvailableRooms 
    CHECK ([AvailableRooms] >= 0 AND [AvailableRooms] <= [TotalRooms]);
    
    PRINT 'Step 6: Added check constraint';
    PRINT 'Successfully added TotalRooms column to TravelPackages table';
END
ELSE
BEGIN
    PRINT 'TotalRooms column already exists in TravelPackages table';
END
