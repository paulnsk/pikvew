param([string]$Output = 'artifacts/release/win-x64', [bool]$Compress = $true, [switch]$NoToolsCopy)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $repository
try {
    $compression = $Compress.ToString().ToLowerInvariant()
    & dotnet.exe publish 'src/Pikvew.App/Pikvew.App.csproj' -m:1 -c Release -r win-x64 --self-contained true '-p:PublishSingleFile=true' '-p:IncludeNativeLibrariesForSelfExtract=true' '-p:IncludeAllContentForSelfExtract=true' '-p:PublishTrimmed=false' "-p:EnableCompressionInSingleFile=$compression" '-p:DebugType=None' '-p:DebugSymbols=false' '-p:SatelliteResourceLanguages=en' -o $Output
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
    $executable = Join-Path $Output 'Pikvew.exe'
    if (!(Test-Path -LiteralPath $executable)) { throw 'Pikvew.exe was not published.' }
    if (!$NoToolsCopy -and (Test-Path -LiteralPath 'C:\tools' -PathType Container)) {
        Copy-Item -LiteralPath $executable -Destination 'C:\tools\Pikvew.exe' -Force
    }
    Write-Host "Published: $executable"
} finally { Pop-Location }
