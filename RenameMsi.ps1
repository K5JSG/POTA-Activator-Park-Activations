$exePath = "C:\Users\jsgay\Documents\Ham Radio\POTA Activator Park Activations\bin\Release\net10.0-windows\POTA Activator Park Activations.exe"
$msiFolder = "C:\Users\jsgay\Documents\Ham Radio\POTA Activator Park Activations\Installer\POTA Check Installer\Release" 
$projectName = "POTA Activator Park Activations"

if (-not (Test-Path $exePath)) {
    Write-Host "Executable not found - skipping rename."
    Write-Host "Looked for: $exePath"
    exit 0
}

# Fetch the version from the actual compiled .exe application file
$fullVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exePath).FileVersion

if (-not $fullVersion) {
    Write-Host "Warning: Version string returned blank from file properties."
    $version = "1.0.0" # Fallback version if properties are blank
} else {
    # Splits 1.0.0.0 down to 1.0.0
    $version = ($fullVersion -split '\.')[0..2] -join '.'
}

$sourceMsi = Join-Path $msiFolder "$projectName.msi"
$targetMsi = Join-Path $msiFolder "$projectName Setup $version.msi"

if (Test-Path $targetMsi) {
    Remove-Item $targetMsi -Force
}

if (Test-Path $sourceMsi) {
    Rename-Item -Path $sourceMsi -NewName (Split-Path $targetMsi -Leaf) -Force
    Write-Host "Renamed MSI to: $(Split-Path $targetMsi -Leaf)"
} else {
    Write-Host "$sourceMsi not found - already renamed or build name doesn't match."
}
