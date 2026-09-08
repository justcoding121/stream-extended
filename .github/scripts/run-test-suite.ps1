param(
    [Parameter(Mandatory = $true)][string]$Project,
    [string]$Filter = '',
    [string]$ResultsDirectory = '',
    [int]$Retries = 1,
    [switch]$HangBlame
)

$attempts = [Math]::Max(1, $Retries)
for ($i = 1; $i -le $attempts; $i++) {
    $label = if ($Filter) { $Filter } else { $Project }
    Write-Host "dotnet test $label attempt $i of $attempts"
    $args = @(
        'test', $Project,
        '--configuration', 'Release',
        '--no-build',
        '--no-restore',
        '--nologo'
    )
    if ($Filter) {
        $args += @('--filter', $Filter)
    }
    if ($HangBlame) {
        $args += @('--blame-hang', '--blame-hang-timeout', '2m')
    }
    if ($ResultsDirectory) {
        $args += @('--collect:Code Coverage', '--results-directory', $ResultsDirectory)
    }

    & dotnet @args
    if ($LASTEXITCODE -eq 0) {
        exit 0
    }

    if ($i -eq $attempts) {
        exit $LASTEXITCODE
    }

    Write-Host "Suite failed on attempt $i; retrying..."
    Start-Sleep -Seconds 5
}
