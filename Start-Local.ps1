$ErrorActionPreference = "Stop"

Set-Location $PSScriptRoot
$projectFile = Join-Path $PSScriptRoot "SkfProductAssistant.Functions.csproj"

$env:AzureWebJobsStorage = "UseDevelopmentStorage=true"
$env:FUNCTIONS_WORKER_RUNTIME = "dotnet-isolated"

$storageLocation = Join-Path $env:TEMP "skf-product-assistant-azurite"
$storagePorts = 10000, 10001, 10002
$azuriteRunning = @(Get-NetTCPConnection -LocalPort $storagePorts -State Listen -ErrorAction SilentlyContinue).Count -ge 3
if (-not $azuriteRunning) {
    New-Item -ItemType Directory -Force -Path $storageLocation | Out-Null
    Start-Process -FilePath "npx.cmd" -ArgumentList @(
        "--yes", "azurite", "--location", $storageLocation,
        "--blobPort", "10000", "--queuePort", "10001", "--tablePort", "10002"
    ) -WorkingDirectory $PSScriptRoot
}

dotnet build $projectFile --nologo
$outputDirectory = Join-Path $PSScriptRoot "bin\Debug\net8.0"
Push-Location $outputDirectory
try {
    func start --dotnet-isolated --no-build --verbose
}
finally {
    Pop-Location
}