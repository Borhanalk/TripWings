-- Script to remove LastFourDigits column from Payments table
-- SECURITY: This ensures no card information (not even last 4 digits) is stored

-- Check if column exists before dropping
IF EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[Payments]') 
    AND name = 'LastFourDigits'
)
BEGIN
    ALTER TABLE [dbo].[Payments]
    DROP COLUMN [LastFourDigits];
    
    PRINT 'LastFourDigits column has been removed from Payments table.';
END
ELSE
BEGIN
    PRINT 'LastFourDigits column does not exist in Payments table.';
END
GO
