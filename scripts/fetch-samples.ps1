$ErrorActionPreference = 'Stop'
$sampleRoot = Join-Path $PSScriptRoot '../artifacts/samples'
New-Item -ItemType Directory -Force -Path $sampleRoot | Out-Null
$revision = 'fb3714b6cfdbdae734e33b523e80c303bdfdae52'
$base = "https://raw.githubusercontent.com/whistyun/AnimatedImage/$revision"
$names = @(
    'step_all_previous.gif', 'step_all_none.gif', 'step_all_background.gif',
    'step_firstnone_laterprev.gif', 'step_firstnone_laterback.gif', 'step_jagging_back_prev.gif',
    'earth_89a_intl.gif', 'BouncingBeachBall.png', 'BouncingBeachBallWebP.webp'
)
foreach ($name in $names) {
    Invoke-WebRequest -UseBasicParsing -Uri "$base/AnimatedImageTest/Inputs/$name" -OutFile (Join-Path $sampleRoot $name)
}
Invoke-WebRequest -UseBasicParsing -Uri "$base/DemoForWpf/Images/stickman.webp" -OutFile (Join-Path $sampleRoot 'stickman.webp')
foreach ($reference in (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'sample-references.json') -Raw | ConvertFrom-Json)) {
    $destination = Join-Path (Join-Path $sampleRoot 'reference') $reference
    New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
    $escapedReference = ($reference.Split('/') | ForEach-Object { [Uri]::EscapeDataString($_) }) -join '/'
    Invoke-WebRequest -UseBasicParsing -Uri "$base/AnimatedImageTest/Outputs/$escapedReference" -OutFile $destination
}
Write-Output "Downloaded animation verification corpus to $sampleRoot"
