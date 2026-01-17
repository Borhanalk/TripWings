# PowerShell script to reset the database
# This will drop and recreate the TripWingsDB database

$connectionString = "Server=(localdb)\mssqllocaldb;Database=master;Trusted_Connection=True;"

Write-Host "Connecting to SQL Server..." -ForegroundColor Yellow

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    
    Write-Host "Dropping database TripWingsDB if it exists..." -ForegroundColor Yellow
    
    $dropCommand = $connection.CreateCommand()
    $dropCommand.CommandText = @"
IF EXISTS (SELECT name FROM sys.databases WHERE name = 'TripWingsDB')
BEGIN
    ALTER DATABASE TripWingsDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE TripWingsDB;
    PRINT 'Database TripWingsDB dropped successfully.';
END
ELSE
BEGIN
    PRINT 'Database TripWingsDB does not exist.';
END
"@
    
    $dropCommand.ExecuteNonQuery()
    
    Write-Host "Database reset completed successfully!" -ForegroundColor Green
    Write-Host "Now run 'dotnet run' to recreate the database with all tables." -ForegroundColor Cyan
    
    $connection.Close()
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host "Make sure SQL Server LocalDB is running." -ForegroundColor Yellow
}
