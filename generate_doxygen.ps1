# PowerShell Script to Install Doxygen and Generate Documentation
# Define paths
$doxygenInstallerUrl = "https://doxygen.nl/files/doxygen-1.12.0-setup.exe"
$doxygenInstallerPath = "$env:TEMP\doxygen-setup.exe"
$projectDir = Join-Path -Path $PSScriptRoot -ChildPath ""
$outputDir = "$projectDir\docs"

# Step 1: Check if Doxygen is installed
Write-Output "Checking if Doxygen is installed..."
$doxygenPath = (Get-Command "doxygen" -ErrorAction SilentlyContinue).Source
if (-not $doxygenPath) {
    Write-Output "Doxygen not found. Downloading and installing Doxygen..."
    Invoke-WebRequest -Uri $doxygenInstallerUrl -OutFile $doxygenInstallerPath -UseBasicParsing
    Start-Process -FilePath $doxygenInstallerPath -ArgumentList "/S" -Wait
    $doxygenPath = (Get-Command "doxygen" -ErrorAction SilentlyContinue).Source
    if (-not $doxygenPath) {
        Write-Output "Doxygen installation failed. Please install it manually."
        exit 1
    }
    $doxygenPath = "doxygen\bin"
    [System.Environment]::SetEnvironmentVariable("Path", $env:Path + ";$doxygenPath", [System.EnvironmentVariableTarget]::Machine)
    Write-Output "Doxygen installed successfully."
} else {
    Write-Output "Doxygen is already installed at $doxygenPath."
}

# Step 2: Create Doxygen configuration file if not exists
$doxyfilePath = "$projectDir\Doxyfile"
if (-not (Test-Path $doxyfilePath)) {
    Write-Output "Generating Doxygen configuration file..."
    Start-Process -FilePath "doxygen" -ArgumentList "-g $doxyfilePath" -Wait
}

# Step 3: Update configuration file for your project settings
$serverPath = "$projectDir\server"
$clientPath = "$projectDir\client\client\MainWindow.xaml.cs"

# Update multiple configuration parameters
$configUpdates = @{
    "OUTPUT_DIRECTORY.*" = "OUTPUT_DIRECTORY = $outputDir"
    "INPUT.*" = "INPUT = $serverPath $clientPath"
    "RECURSIVE.*" = "RECURSIVE = YES"
    "EXTRACT_ALL.*" = "EXTRACT_ALL = YES"
    "EXTRACT_PRIVATE.*" = "EXTRACT_PRIVATE = YES"
    "FILE_PATTERNS.*" = "FILE_PATTERNS = *.cpp *.h *.cs *.ino"
    "EXTENSION_MAPPING.*" = "EXTENSION_MAPPING = ino=C++"
    "PROJECT_NAME.*" = "PROJECT_NAME = Rock Papper Scissors"
}

foreach ($key in $configUpdates.Keys) {
    (Get-Content $doxyfilePath) -replace $key, $configUpdates[$key] | Set-Content $doxyfilePath
}

# Step 4: Run Doxygen to generate documentation
Write-Output "Generating documentation..."
Start-Process -FilePath "doxygen" -ArgumentList "$doxyfilePath" -Wait
Write-Output "Documentation generation complete. Output available at $outputDir."