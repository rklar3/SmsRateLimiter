# Base URL for the API
$baseUrl = "http://localhost:5263"

# Function to send a request to check if a message can be sent
function Check-Message {
    param (
        [string]$phoneNumber
    )
    
    $body = @{
        phoneNumber = $phoneNumber
        message = "Test message"
        recipientNumber = "+19876543210"
    } | ConvertTo-Json

    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/api/sms/check" `
            -Method Post `
            -Body $body `
            -ContentType "application/json"
        return $response
    }
    catch {
        Write-Host "Error checking message: $_"
        return $null
    }
}

# Function to print stats
function Print-Stats {
    try {
        $stats = Invoke-RestMethod -Uri "$baseUrl/api/monitoring/stats" `
            -Method Get
        Write-Host "Current Stats:"
        $stats | Format-List
        Write-Host "----------------------------------------"
    }
    catch {
        Write-Host "Error getting stats: $_"
    }
}

# Array of phone numbers to simulate
$phoneNumbers = @(
    "+12345678901"
    "+12345678902"
    "+12345678903"
    "+12345678904"
    "+12345678905"
    "+12345678906"
    "+12345678907"
    "+12345678908"
    "+12345678909"
    "+12345678910"
)

Write-Host "Starting SMS traffic simulation..."
Write-Host "Press Ctrl+C to stop"

# Initial stats
Print-Stats

# Main loop
while ($true) {
    # Send multiple requests in parallel
    1..5 | ForEach-Object {
        $phoneNumber = $phoneNumbers | Get-Random
        $response = Check-Message -phoneNumber $phoneNumber
        Write-Host "Checking $phoneNumber : $($response | ConvertTo-Json)"
    }
    
    # Print stats every 2 seconds (more frequently)
    if ((Get-Date).Second % 2 -eq 0) {
        Print-Stats
    }
    
    # Very short delay between batches
    Start-Sleep -Milliseconds (Get-Random -Minimum 10 -Maximum 50)
} 