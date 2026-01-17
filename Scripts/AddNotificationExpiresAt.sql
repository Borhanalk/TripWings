-- Add NotificationExpiresAt column to WaitingListEntries table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[WaitingListEntries]') AND name = 'NotificationExpiresAt')
BEGIN
    ALTER TABLE [WaitingListEntries]
    ADD [NotificationExpiresAt] DATETIME2 NULL;
    
    PRINT 'Added NotificationExpiresAt column to WaitingListEntries table';
END
ELSE
BEGIN
    PRINT 'NotificationExpiresAt column already exists in WaitingListEntries table';
END
