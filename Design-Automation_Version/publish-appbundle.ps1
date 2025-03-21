# PowerShell script to publish AppBundle for Design Automation
param(
    [Parameter(Mandatory=$true)]
    [string]$ClientId,
    
    [Parameter(Mandatory=$true)]
    [string]$ClientSecret,
    
    [Parameter(Mandatory=$true)]
    [string]$AppBundleId,
    
    [Parameter(Mandatory=$true)]
    [string]$AppBundleVersion
)

# Configuration
$baseUrl = "https://developer.api.autodesk.com"
$scope = "data:write code:all"

# Function to get access token
function Get-AccessToken {
    $body = @{
        client_id = $ClientId
        client_secret = $ClientSecret
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
    $releaseFolder = ".\ReportCreatorApp\bin\Release"
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
        "ReportCreatorApp.dll",
        "ReportCreatorApp.addin"
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
    
    $url = "$baseUrl/da/us-east/v3/appbundles/$AppBundleId/versions/$AppBundleVersion"
    $response = Invoke-RestMethod -Uri $url -Method Put -Headers $headers -InFile $zipPath
    
    Write-Host "AppBundle published successfully!"
    Write-Host "Response: $($response | ConvertTo-Json)"
    
    # Cleanup
    Remove-Item $zipPath -Force
    
} catch {
    Write-Host "Error: $_"
    exit 1
} 