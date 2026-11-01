param([Parameter(Mandatory=$true)][string]$Repository, [Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference = 'Stop'
[xml]$config = Get-Content -LiteralPath (Join-Path $Repository 'Version.props')
$major = [int]$config.Project.PropertyGroup.VersionMajor
$sameMajor = [int]$config.Project.PropertyGroup.MinorVersionMajor -eq $major
$minor = if ($sameMajor) { [int]$config.Project.PropertyGroup.VersionMinor } else { 1 }
$floor = if ($sameMajor) { [int]$config.Project.PropertyGroup.BuildFloor } else { 0 }
$counterFolder = Join-Path $Repository 'artifacts'
[IO.Directory]::CreateDirectory($counterFolder) | Out-Null
$counterPath = Join-Path $counterFolder 'version-counter.txt'
$lockPath = Join-Path $counterFolder 'version-counter.lock'
$deadline = [DateTime]::UtcNow.AddSeconds(20)
$counterLock = $null
while ($null -eq $counterLock) {
    try { $counterLock = [IO.File]::Open($lockPath, 'OpenOrCreate', 'ReadWrite', 'None') }
    catch [IO.IOException] { if ([DateTime]::UtcNow -gt $deadline) { throw }; Start-Sleep -Milliseconds 50 }
}
try {
    $build = $floor + 1
    if (Test-Path -LiteralPath $counterPath) {
        $previous = [Version]([IO.File]::ReadAllText($counterPath).Trim())
        if ($previous.Major -eq $major -and $previous.Minor -eq $minor) { $build = [Math]::Max($floor, $previous.Build) + 1 }
    }
    $version = "$major.$minor.$build"
    [IO.File]::WriteAllText($counterPath, $version)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Output))) | Out-Null
    [IO.File]::WriteAllText($Output, $version)
} finally { $counterLock.Dispose() }
