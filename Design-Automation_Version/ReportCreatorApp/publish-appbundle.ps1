# PowerShell script to publish AppBundle for Design Automation

# Read .env file
$envPath = Join-Path $PSScriptRoot ".env"
if (Test-Path $envPath) {
    Get-Content $envPath | ForEach-Object {
        if ($_ -match '(.+)=(.+)') {
            $key = $matches[1]
            $value = $matches[2]
            Set-Variable -Name $key -Value $value
        }
    }
} else {
    Write-Error ".env file not found at $envPath"
    exit 1
}

# Configuration
$baseUrl = "https://developer.api.autodesk.com"
$scope = "data:write code:all"

# Function to get access token
function Get-AccessToken {
    $body = @{
        client_id = $FORGE_CLIENT_ID
        client_secret = $FORGE_CLIENT_SECRET
        grant_type = "client_credentials"
        scope = $scope
    }
    
    $response = Invoke-RestMethod -Uri "$baseUrl/authentication/v2/token" -Method Post -Body $body
    return $response.access_token
}

# Function to create PackageContents.xml
function Create-PackageContentsXml {
    param (
        [string]$OutputPath
    )
    
    $xmlContent = @"
<?xml version="1.0" encoding="utf-8"?>
<ApplicationPackage SchemaVersion="1.0">
  <Id>ReportCreator</Id>
  <Version>1.0</Version>
  <VendorId>ADSK</VendorId>
  <VendorDescription>Autodesk, www.autodesk.com</VendorDescription>
  <SupportedOS>Win64</SupportedOS>
  <Description>Report Creator Application</Description>
  <Components>
    <RuntimeRequirements OS="Win64" Platform="Revit" SeriesMin="R2024" SeriesMax="R2024" />
    <EntryPoint>
      <Assembly>ReportCreatorApp.dll</Assembly>
      <FullClassName>ReportCreatorApp.CreateReportsApp</FullClassName>
      <ClientId>ReportCreator</ClientId>
      <VendorId>ADSK</VendorId>
      <VendorDescription>Autodesk, www.autodesk.com</VendorDescription>
      <Description>Report Creator Application</Description>
    </EntryPoint>
  </Components>
</ApplicationPackage>
"@
    
    $xmlContent | Out-File -FilePath $OutputPath -Encoding UTF8
}

# Function to create ZIP file
function Create-AppBundleZip {
    $releaseFolder = Join-Path $PSScriptRoot "ReportCreatorApp\bin\Release"
    $bundleFolder = Join-Path $releaseFolder "ReportCreator.bundle"
    $contentsFolder = Join-Path $bundleFolder "Contents"
    $zipPath = Join-Path $releaseFolder "ReportCreator.bundle.zip"
    
    # Clean up existing files
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }
    if (Test-Path $bundleFolder) {
        Remove-Item $bundleFolder -Recurse -Force
    }
    
    # Create bundle structure
    New-Item -ItemType Directory -Path $bundleFolder -Force
    New-Item -ItemType Directory -Path $contentsFolder -Force
    
    # Create PackageContents.xml
    Create-PackageContentsXml -OutputPath (Join-Path $bundleFolder "PackageContents.xml")
    
    # Copy necessary files to Contents folder
    $sourceFiles = @(
        "CreateReportsApp.dll",
        "CreateReportsApp.addin"
    )
    
    foreach ($file in $sourceFiles) {
        $sourcePath = Join-Path $releaseFolder $file
        if (Test-Path $sourcePath) {
            Copy-Item $sourcePath -Destination $contentsFolder -Force
        } else {
            Write-Warning "File not found: $sourcePath"
        }
    }
    
    # Create ZIP file
    Compress-Archive -Path "$bundleFolder\*" -DestinationPath $zipPath -Force
    
    # Clean up bundle folder
    Remove-Item $bundleFolder -Recurse -Force
    
    return $zipPath
}

# Main process
try {
    Write-Host "Getting access token..."
    $accessToken = Get-AccessToken
    
    Write-Host "Creating ZIP file..."
    $zipPath = Create-AppBundleZip
    
    Write-Host "Publishing AppBundle..."
    $headers = @{
        "Authorization" = "Bearer $accessToken"
        "Content-Type" = "application/zip"
    }
    
    $url = "$baseUrl/da/us-east/v3/appbundles/$APP_BUNDLE_ID/versions/$APP_BUNDLE_VERSION"
    $response = Invoke-RestMethod -Uri $url -Method Put -Headers $headers -InFile $zipPath
    
    Write-Host "AppBundle published successfully!"
    Write-Host "Response: $($response | ConvertTo-Json)"
    
    # Cleanup
    Remove-Item $zipPath -Force
    
} catch {
    Write-Host "Error: $_"
    exit 1
} 