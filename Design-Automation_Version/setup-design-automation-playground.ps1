# Load .env variables from the solution folder
$envPath = Join-Path $PSScriptRoot ".env"
if (-Not (Test-Path $envPath)) {
    Write-Error "'.env' file not found at: $envPath"
    exit 1
}

# Parse .env file
Get-Content $envPath | ForEach-Object {
    if ($_ -match "^\s*([^#][^=]+)=(.+)$") {
        [System.Environment]::SetEnvironmentVariable($matches[1].Trim(), $matches[2].Trim())
    }
}

$clientId = $env:FORGE_CLIENT_ID
$clientSecret = $env:FORGE_CLIENT_SECRET
$bucketKey = $env:FORGE_BUCKET_KEY
$projectId = $env:FORGE_PROJECT_ID
$revitFileName = $env:FORGE_REVIT_FILENAME
$combinedFileName = "${projectId}_${revitFileName}"

# Target folder on Desktop
$desktopPath = [System.Environment]::GetFolderPath("Desktop")
$targetDir = Join-Path $desktopPath "design_automation_playground"
if (-Not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir | Out-Null
}

# Step 1: Get Access Token (OAuth v2)
$tokenResponse = Invoke-RestMethod -Uri "https://developer.api.autodesk.com/authentication/v1/authenticate" `
    -Method POST `
    -Body @{
        client_id     = $clientId
        client_secret = $clientSecret
        grant_type    = "client_credentials"
        scope         = "data:read"
    } `
    -ContentType "application/x-www-form-urlencoded"

$accessToken = $tokenResponse.access_token

# Step 2: Download Revit file if not already there
$revitPath = Join-Path $targetDir $combinedFileName
if (-Not (Test-Path $revitPath)) {
    $downloadUrl = "https://developer.api.autodesk.com/oss/v2/buckets/$bucketKey/objects/$combinedFileName"
    Invoke-RestMethod -Uri $downloadUrl `
        -Headers @{ Authorization = "Bearer $accessToken" } `
        -OutFile $revitPath
    Write-Host " Downloaded: $combinedFileName"
} else {
    Write-Host " Revit file already exists, skipping download."
}

# Step 3: Copy params.json
$paramsSource = Get-ChildItem -Path $PSScriptRoot -Filter "params.json" -Recurse | Select-Object -First 1
$paramsTarget = Join-Path $targetDir "params.json"
if (-Not (Test-Path $paramsTarget) -and $paramsSource) {
    Copy-Item $paramsSource.FullName -Destination $paramsTarget
    Write-Host " Copied params.json to $targetDir"
} else {
    Write-Host " params.json already exists or not found, skipping copy."
}

# Step 4: Create assets.zip if it doesn't already exist
$zipPath = Join-Path $targetDir "assets.zip"
if (-Not (Test-Path $zipPath)) {
    $pngFiles = Get-ChildItem -Path $PSScriptRoot -Recurse -Filter *.png | Select-Object -First 3
    if ($pngFiles.Count -eq 0) {
        Write-Warning " No .png files found to zip."
    } else {
        Compress-Archive -Path $pngFiles.FullName -DestinationPath $zipPath -Force
        Write-Host " Created assets.zip with $($pngFiles.Count) PNGs"
    }
} else {
    Write-Host " assets.zip already exists, skipping."
}
