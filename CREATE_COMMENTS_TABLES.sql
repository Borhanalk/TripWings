-- SQL Script to create SiteComments and CommentRatings tables
-- Run this script if Entity Framework migrations are not available

-- Create SiteComments table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SiteComments]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[SiteComments] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [Rating] int NOT NULL,
        [CommentText] nvarchar(2000) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsApproved] bit NOT NULL,
        CONSTRAINT [PK_SiteComments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_SiteComment_Rating] CHECK ([Rating] >= 1 AND [Rating] <= 5),
        CONSTRAINT [FK_SiteComments_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
    
    CREATE INDEX [IX_SiteComments_UserId] ON [dbo].[SiteComments] ([UserId]);
    CREATE INDEX [IX_SiteComments_CreatedAt] ON [dbo].[SiteComments] ([CreatedAt]);
    CREATE INDEX [IX_SiteComments_IsApproved] ON [dbo].[SiteComments] ([IsApproved]);
END
GO

-- Create CommentRatings table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CommentRatings]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CommentRatings] (
        [Id] int NOT NULL IDENTITY,
        [SiteCommentId] int NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [IsHelpful] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CommentRatings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CommentRatings_SiteComments_SiteCommentId] FOREIGN KEY ([SiteCommentId]) REFERENCES [dbo].[SiteComments] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CommentRatings_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [IX_CommentRatings_SiteCommentId_UserId] UNIQUE ([SiteCommentId], [UserId])
    );
    
    CREATE INDEX [IX_CommentRatings_SiteCommentId] ON [dbo].[CommentRatings] ([SiteCommentId]);
    CREATE INDEX [IX_CommentRatings_UserId] ON [dbo].[CommentRatings] ([UserId]);
    CREATE UNIQUE INDEX [IX_CommentRatings_SiteCommentId_UserId] ON [dbo].[CommentRatings] ([SiteCommentId], [UserId]);
END
GO
