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
<ApplicationPackage 
    SchemaVersion="1.0"
    AutodeskProduct="Revit"
    ProductType="Application"
    ProductCode="ReportCreatorApp"
    UpgradeCode="{$(New-Guid)}"
    Name="ReportCreatorApp"
    Description="Report Creator Application"
    AppVersion="1.0.0"
    FriendlyVersion="1.0.0"
    Author="Your Company Name"
    Icon="./Contents/Resources/icon.png"
    HelpFile="./Contents/Resources/help.html"
    SupportedLocales="Enu"
    AppNameSpace="appstore.exchange.autodesk.com">
    
    <CompanyDetails
        Name="Your Company Name"
        Phone="12345678"
        Url="www.yourcompany.com"
        Email="your.email@example.com"/>
        
    <RuntimeRequirements
        OS="Win64"
        Platform="Revit"
        SeriesMin="R2024"
        SeriesMax="R2024"/>
        
    <Components Description="Report Creator Components">
        <RuntimeRequirements
            OS="Win64"
            Platform="Revit"
            SeriesMin="R2024"
            SeriesMax="R2024"/>
        <ComponentEntry
            AppName="ReportCreatorApp"
            Version="1.0.0"
            ModuleName="./Contents/CreateReportsApp.addin"
            AppDescription="Report Creator App"
            LoadOnCommandInvocation="False"
            LoadOnRevitStartup="True">
            <Commands Description="Report Creator Commands"/>
        </ComponentEntry>
    </Components>
</ApplicationPackage>
"@
    
    $xmlContent | Out-File -FilePath $OutputPath -Encoding UTF8
}

# Function to create ZIP file
function Create-AppBundleZip {
    $releaseFolder = Join-Path $PSScriptRoot "ReportCreatorApp\bin\Release\net48"
    $bundleFolder = Join-Path $releaseFolder "ReportCreator.bundle"
    $contentsFolder = Join-Path $bundleFolder "Contents"
    $resourcesFolder = Join-Path $contentsFolder "Resources"
    $zipPath = Join-Path $releaseFolder "ReportCreator.bundle.zip"
    
    Write-Host "Release folder: $releaseFolder"
    Write-Host "Bundle folder: $bundleFolder"
    Write-Host "Contents folder: $contentsFolder"
    Write-Host "Resources folder: $resourcesFolder"
    Write-Host "Zip path: $zipPath"
    
    # Clean up existing files
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }
    if (Test-Path $bundleFolder) {
        Remove-Item $bundleFolder -Recurse -Force
    }
    
    # Create bundle structure
    New-Item -ItemType Directory -Path $bundleFolder -Force | Out-Null
    New-Item -ItemType Directory -Path $contentsFolder -Force | Out-Null
    New-Item -ItemType Directory -Path $resourcesFolder -Force | Out-Null
    
    # Create PackageContents.xml in the bundle root
    Create-PackageContentsXml -OutputPath (Join-Path $bundleFolder "PackageContents.xml")
    
    # Create placeholder help and icon files
    @"
<!DOCTYPE html>
<html>
<head><title>Report Creator Help</title></head>
<body><h1>Report Creator Help</h1><p>Help content goes here.</p></body>
</html>
"@ | Out-File -FilePath (Join-Path $resourcesFolder "help.html") -Encoding UTF8

    # Create a simple 32x32 pixel icon (you should replace this with your actual icon)
    $iconPath = Join-Path $resourcesFolder "icon.png"
    if (-not (Test-Path $iconPath)) {
        # Create an empty icon file
        [byte[]]::new(0) | Set-Content $iconPath -Encoding Byte
    }
    
    # Copy necessary files to Contents folder
    $sourceFiles = @(
        @{Source = "ReportCreatorApp.dll"; Dest = "ReportCreatorApp.dll"},
        @{Source = "CreateReportsApp.addin"; Dest = "CreateReportsApp.addin"}
        @{Source = "DesignAutomationBridge.dll"; Dest = "DesignAutomationBridge.dll"},
        @{Source = "RevitAPI.dll"; Dest = "RevitAPI.dll"},
        @{Source = "Newtonsoft.Json.dll"; Dest = "Newtonsoft.Json.dll"}
    )
    
    foreach ($file in $sourceFiles) {
        $sourcePath = Join-Path $releaseFolder $file.Source
        $destPath = Join-Path $contentsFolder $file.Dest
        if (Test-Path $sourcePath) {
            Copy-Item $sourcePath -Destination $destPath -Force
            Write-Host "Copied $($file.Source) to Contents folder"
        } else {
            Write-Warning "File not found: $sourcePath"
        }
    }
    
    # Create ZIP file from the bundle folder
    Write-Host "Creating ZIP file from $bundleFolder to $zipPath"
    Compress-Archive -Path "$bundleFolder" -DestinationPath $zipPath -Force
    
    if (Test-Path $zipPath) {
        Write-Host "ZIP file created successfully"
    } else {
        Write-Error "Failed to create ZIP file"
        exit 1
    }
    
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
    
    ###
    #Write-Host "Publishing AppBundle..."
    #$headers = @{
        #"Authorization" = "Bearer $accessToken"
        #"Content-Type" = "application/zip"
    #}
    
    #$url = "$baseUrl/da/us-east/v3/appbundles/$APP_BUNDLE_ID/versions/$APP_BUNDLE_VERSION"
    
    # Read ZIP file as bytes
    #$zipBytes = [System.IO.File]::ReadAllBytes($zipPath)
    
    # Use Invoke-RestMethod with byte array
    #$response = Invoke-RestMethod -Uri $url -Method Put -Headers $headers -Body $zipBytes
    
    #Write-Host "AppBundle published successfully!"
    #Write-Host "Response: $($response | ConvertTo-Json)"
    
    # Cleanup
    #Remove-Item $zipPath -Force
    ###
    
} catch {
    Write-Host "Error: $_"
    Write-Host "Stack Trace: $($_.ScriptStackTrace)"
    exit 1
} 