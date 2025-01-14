param(
    [string]$ComPort,
    [int]$BaudRate,
    [ValidateSet("Physical", "Simulated")]
    [string]$HardwareType
)

$allParamsProvided = $ComPort -and $BaudRate -and $HardwareType

if (-not $allParamsProvided) {
    Write-Host "Warning: Missing required parameters. Please provide all the following parameters to upload sketch and test handshake:"
    Write-Host "  -ComPort <COM port>"
    Write-Host "  -BaudRate <Baud rate>"
    Write-Host "  -HardwareType <Physical | Simulated>"
}

Write-Host "Parameters received:"
Write-Host "  COM Port: $ComPort"
Write-Host "  Baud Rate: $BaudRate"
Write-Host "  Hardware Type: $HardwareType"

$IsGitHubActions = $env:GITHUB_ACTIONS -eq "true"

# Папки для білду та деплойменту
$ClientOutputDir = "deploy/client-output"
$ServerOutputDir = "deploy/server-output"
$TestResultsDir = "deploy/test-results"

# Створення папок для артефактів
Write-Host "Creating deployment directories..."
New-Item -ItemType Directory -Force -Path $ClientOutputDir, $ServerOutputDir, $TestResultsDir

# 1. Build Client
Write-Host "Building client..."
dotnet build ./client/client.sln -c Release -o $ClientOutputDir

# 2. Build Server
Write-Host "Building server..."
arduino-cli compile --fqbn arduino:avr:uno ./server/server.ino --output-dir $ServerOutputDir

# 3. Upload to hardware (only for physical board)
if (-not $IsGitHubActions -and $allParamsProvided) {
    if ($HardwareType -eq "Physical") {
        Write-Host "Uploading firmware to physical hardware..."
        arduino-cli upload -p $ComPort --fqbn arduino:avr:uno ./server/server.ino
    } else {
        Write-Host "Simulated hardware detected. Skipping upload step."
        Write-Host "HEX file path: $ServerOutputDir/server.ino.hex"
    }
}

function Send-Message {
    param (
        [string]$Message
    )
    # Відкриваємо серійний порт для зв'язку з Arduino
    $serialPort = new-Object System.IO.Ports.SerialPort $ComPort, $BaudRate
    $serialPort.Open()

    # Надсилаємо повідомлення
    $serialPort.WriteLine($Message)
    Write-Host "Sent: $Message"
    
    # Чекаємо, поки отримуємо відповідь
    $response = ""
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($stopwatch.Elapsed.TotalSeconds -lt 5) {
        if ($serialPort.BytesToRead -gt 0) {
            $response += $serialPort.ReadExisting()
        }
    }

    $serialPort.Close()
    return $response
}

if (-not $IsGitHubActions -and $allParamsProvided) {
    # Надсилаємо команду reset=1 і очікуємо "game_reset"
    $response = Send-Message "reset=1"
    Write-Host "Response: $response"
    if ($response -notmatch "game_reset") {
        Write-Host "Test failed. 'game_reset' not found in response."
        exit 1
    }

    # Надсилаємо команду choices=playerOne=0;playerTwo=2 і очікуємо одне з ключових слів (draw, one_won_round, two_won_round)
    $response = Send-Message "choices=playerOne=0;playerTwo=2"
    Write-Host "Response: $response"
    if ($response -match "draw|one_won_round|two_won_round") {
        Write-Host "Test passed. Valid round outcome received."
    } else {
        Write-Host "Test failed. Unexpected response."
        exit 1
    }
}

Write-Host "All operations completed. Artifacts are in 'deploy' directory."
