-- Script to add two trips with NO available rooms
-- This script adds two travel packages with AvailableRooms = 0

-- Delete existing trips if they exist (to avoid duplicates)
DELETE FROM PackageImages WHERE TravelPackageId IN (SELECT Id FROM TravelPackages WHERE Destination = 'Maldives' AND Country = 'Maldives');
DELETE FROM Bookings WHERE TravelPackageId IN (SELECT Id FROM TravelPackages WHERE Destination = 'Maldives' AND Country = 'Maldives');
DELETE FROM TravelPackages WHERE Destination = 'Maldives' AND Country = 'Maldives';

DELETE FROM PackageImages WHERE TravelPackageId IN (SELECT Id FROM TravelPackages WHERE Destination = 'Santorini' AND Country = 'Greece');
DELETE FROM Bookings WHERE TravelPackageId IN (SELECT Id FROM TravelPackages WHERE Destination = 'Santorini' AND Country = 'Greece');
DELETE FROM TravelPackages WHERE Destination = 'Santorini' AND Country = 'Greece';

-- Add first trip: Maldives (0 available rooms)
INSERT INTO TravelPackages (Destination, Country, StartDate, EndDate, Price, AvailableRooms, PackageType, AgeLimit, Description, IsVisible, CreatedAt)
VALUES ('Maldives', 'Maldives', DATEADD(day, 35, GETUTCDATE()), DATEADD(day, 42, GETUTCDATE()), 3500.00, 0, 'Luxury', NULL, 'Paradise islands with crystal clear waters', 1, GETUTCDATE());

DECLARE @Trip1Id INT = SCOPE_IDENTITY();

-- Add second trip: Santorini (0 available rooms)
INSERT INTO TravelPackages (Destination, Country, StartDate, EndDate, Price, AvailableRooms, PackageType, AgeLimit, Description, IsVisible, CreatedAt)
VALUES ('Santorini', 'Greece', DATEADD(day, 50, GETUTCDATE()), DATEADD(day, 57, GETUTCDATE()), 2800.00, 0, 'Luxury', NULL, 'Stunning Greek island with white buildings and blue domes', 1, GETUTCDATE());

DECLARE @Trip2Id INT = SCOPE_IDENTITY();

-- Add images for the trips
INSERT INTO PackageImages (TravelPackageId, ImageUrl)
VALUES 
    (@Trip1Id, 'https://images.unsplash.com/photo-1512343879784-a960bf40e7f2?w=800'),
    (@Trip2Id, 'https://images.unsplash.com/photo-1570077188670-e3a8d69ac5ff?w=800');

PRINT 'Successfully added 2 trips with NO available rooms:';
PRINT '1. Maldives - 0 rooms available';
PRINT '2. Santorini - 0 rooms available';
