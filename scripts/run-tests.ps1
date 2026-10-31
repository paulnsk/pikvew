param([switch]$Sandbox)
$ErrorActionPreference = 'Continue'
$repoPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$resultsPath = Join-Path $repoPath 'artifacts/test-results'
New-Item -ItemType Directory -Force -Path $resultsPath | Out-Null
$logPath = Join-Path $resultsPath 'test.log'
"Pikvew tests: $(Get-Date -Format o)`r`nRepository: $repoPath" | Set-Content -LiteralPath $logPath -Encoding UTF8
if (!$Sandbox) {
    $env:PIKVEW_REGISTRY_TESTS = '1'
    $env:PIKVEW_PIPE_TESTS = '1'
} else {
    $env:PIKVEW_REGISTRY_TESTS = '0'
    $env:PIKVEW_PIPE_TESTS = '0'
}
try {
    $dotnetPath = (Get-Command dotnet.exe -ErrorAction Stop).Source
    & $dotnetPath test (Join-Path $repoPath 'Pikvew.slnx') -m:1 --logger 'console;verbosity=normal' --logger 'trx;LogFileName=tests.trx' --results-directory $resultsPath 2>&1 |
        ForEach-Object {
            $line = $_.ToString()
            Write-Host $line
            Add-Content -LiteralPath $logPath -Value $line -Encoding UTF8
        }
    $resultCode = $LASTEXITCODE
} catch {
    $errorText = $_ | Out-String
    Write-Host $errorText
    Add-Content -LiteralPath $logPath -Value $errorText -Encoding UTF8
    $resultCode = 1
}
Write-Host "`nLogs: $resultsPath"
exit $resultCode
