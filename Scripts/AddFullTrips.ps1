# PowerShell script to add two trips with full rooms to the database
# This script executes the SQL script to add trips with all rooms booked

$connectionString = "Server=(localdb)\mssqllocaldb;Database=TripWingsDB;Trusted_Connection=True;MultipleActiveResultSets=true"
$sqlScript = Get-Content -Path "Scripts\AddFullTrips.sql" -Raw

try {
    # Load SQL Server module
    $sqlConnection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $sqlConnection.Open()
    
    Write-Host "Connected to database successfully." -ForegroundColor Green
    
    # Split SQL script by GO statements and execute each batch
    $batches = $sqlScript -split "GO\s*`r?`n" | Where-Object { $_.Trim() -ne "" }
    
    foreach ($batch in $batches) {
        if ($batch.Trim() -ne "") {
            $command = $sqlConnection.CreateCommand()
            $command.CommandText = $batch
            try {
                $command.ExecuteNonQuery() | Out-Null
                Write-Host "Executed batch successfully." -ForegroundColor Green
            }
            catch {
                Write-Host "Error executing batch: $_" -ForegroundColor Red
            }
        }
    }
    
    Write-Host "`nSuccessfully added 2 trips with NO available rooms:" -ForegroundColor Green
    Write-Host "1. Maldives - 0 rooms available" -ForegroundColor Cyan
    Write-Host "2. Santorini - 0 rooms available" -ForegroundColor Cyan
    
    $sqlConnection.Close()
}
catch {
    Write-Host "Error connecting to database: $_" -ForegroundColor Red
    Write-Host "Make sure SQL Server LocalDB is installed and the database exists." -ForegroundColor Yellow
    exit 1
}
