-- Add ReminderSent column to Bookings table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Bookings]') AND name = 'ReminderSent')
BEGIN
    ALTER TABLE [dbo].[Bookings]
    ADD [ReminderSent] BIT NOT NULL DEFAULT 0;
    
    PRINT 'ReminderSent column added successfully to Bookings table.';
END
ELSE
BEGIN
    PRINT 'ReminderSent column already exists in Bookings table.';
END
GO
