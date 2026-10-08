param([string]$BaseRef)
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    if ($BaseRef -and $BaseRef -notmatch '^0+$') {
        $files = @(git diff --name-only --diff-filter=ACMR "$BaseRef...HEAD" -- '*.cs')
    } else {
        $files = @(git diff --name-only --diff-filter=ACMR HEAD -- '*.cs') + @(git ls-files --others --exclude-standard -- '*.cs')
    }
    if ($LASTEXITCODE -ne 0) { throw 'Could not determine formatting scope.' }
    if ($files.Count -eq 0) { Write-Host 'No changed C# files.'; return }
    dotnet format OneZeroErp.slnx --no-restore --verify-no-changes --include @files
    if ($LASTEXITCODE -ne 0) { throw 'Formatting check failed.' }
} finally { Pop-Location }
