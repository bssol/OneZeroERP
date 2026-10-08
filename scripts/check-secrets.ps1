$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    $violations = @()
    foreach ($path in @(git ls-files '*appsettings*.json')) {
        $config = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        if ($config.Authentication.SigningKey) { $violations += "$path contains a signing key." }
        foreach ($property in $config.DevelopmentSeed.PSObject.Properties) {
            if ($property.Name -match 'Password' -and $property.Value) { $violations += "$path contains a seed password." }
        }
        foreach ($property in $config.ConnectionStrings.PSObject.Properties) {
            if ([string]$property.Value -match '(?i)(password|pwd)\s*=') { $violations += "$path contains database credentials." }
        }
    }
    $sensitiveFiles = @(git ls-files '*.pfx' '*.p12' '*.pem' '*.key' '*.mdf' '*.ldf' '.env')
    if ($sensitiveFiles.Count -gt 0) { $violations += 'Sensitive artifact types are tracked.' }
    if ($violations.Count -gt 0) { throw ($violations -join [Environment]::NewLine) }
    Write-Host 'Tracked credential configuration check passed.'
} finally { Pop-Location }
