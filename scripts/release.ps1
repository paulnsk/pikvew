$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $repository
try {
    if (git status --porcelain) { throw 'Commit pending changes before creating a release.' }
    & python (Join-Path $PSScriptRoot 'github-api.py') preflight
    if ($LASTEXITCODE -ne 0) { throw 'GitHub preflight failed.' }
    [xml]$config = Get-Content -LiteralPath 'Version.props'
    $major = [int]$config.Project.PropertyGroup.VersionMajor
    $minor = if ([int]$config.Project.PropertyGroup.MinorVersionMajor -ne $major) { 1 } else { [int]$config.Project.PropertyGroup.VersionMinor + 1 }
    $config.Project.PropertyGroup.VersionMinor = [string]$minor
    $config.Project.PropertyGroup.MinorVersionMajor = [string]$major
    $config.Project.PropertyGroup.BuildFloor = '0'
    $config.Save((Join-Path $repository 'Version.props'))
    & (Join-Path $PSScriptRoot 'publish.ps1') -NoToolsCopy
    $executable = Join-Path $repository 'artifacts/release/win-x64/Pikvew.exe'
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($executable).ProductVersion
    if ($version -notmatch "^$major\.$minor\.\d+$") { throw 'Published executable has an unexpected version.' }
    & python (Join-Path $PSScriptRoot 'audit-bundle.py') $executable | Set-Content -LiteralPath 'artifacts/release-bundle.json' -Encoding UTF8
    if ($LASTEXITCODE -ne 0) { throw 'Published bundle contains unexpected files.' }
    $config.Project.PropertyGroup.BuildFloor = ([Version]$version).Build.ToString()
    $config.Save((Join-Path $repository 'Version.props'))
    & git add Version.props
    & git commit -m "Release Pikvew $version"
    if ($LASTEXITCODE -ne 0) { throw 'Release commit failed.' }
    & python (Join-Path $PSScriptRoot 'github-api.py') push
    if ($LASTEXITCODE -ne 0) { throw 'Push failed.' }
    $sha = (git rev-parse HEAD).Trim()
    & python (Join-Path $PSScriptRoot 'github-api.py') release --version $version --commit $sha --executable $executable
    if ($LASTEXITCODE -ne 0) { throw 'GitHub release/upload failed.' }
} finally { Pop-Location }
