param([Parameter(Mandatory=$true)][string]$Image)
$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class StartupWindows {
  public delegate bool Callback(IntPtr window, IntPtr state);
  [DllImport("user32.dll")] public static extern bool EnumWindows(Callback callback, IntPtr state);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr window, StringBuilder text, int count);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr window, uint message, IntPtr w, IntPtr l);
}
'@
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$imagePath = [IO.Path]::GetFullPath($Image)
$results = @()
$previousExtraction = $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR
try {
    foreach ($variant in @('plain', 'compressed')) {
        $executable = Join-Path $repository "artifacts/bench/$variant/Pikvew.exe"
        $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = Join-Path $repository ('artifacts/bench/extraction-' + $variant + '-' + [Guid]::NewGuid().ToString('N'))
        for ($run = 0; $run -lt 4; $run++) {
            $timer = [Diagnostics.Stopwatch]::StartNew()
            $process = Start-Process -FilePath $executable -ArgumentList ('"' + $imagePath + '"') -WindowStyle Hidden -PassThru
            $found = @{ Window = [IntPtr]::Zero; Title = '' }
            try {
                if (!$process.WaitForInputIdle(20000)) { throw 'Application did not become input-idle.' }
                do {
                [StartupWindows]::EnumWindows({
                    param($window, $state)
                    [uint32]$ownerId = 0
                    $null = [StartupWindows]::GetWindowThreadProcessId($window, [ref]$ownerId)
                    if ($ownerId -eq $process.Id) {
                        $title = New-Object Text.StringBuilder 1024
                        $null = [StartupWindows]::GetWindowText($window, $title, $title.Capacity)
                        if ($title.ToString().Contains('Pikvew')) { $found.Window = $window; $found.Title = $title.ToString() }
                    }
                    return $true
                }, [IntPtr]::Zero) | Out-Null
                if ($found.Window -eq [IntPtr]::Zero) { Start-Sleep -Milliseconds 10 }
                } while ($found.Window -eq [IntPtr]::Zero -and $timer.Elapsed.TotalSeconds -lt 20 -and !$process.HasExited)
                $timer.Stop()
                if ($found.Window -eq [IntPtr]::Zero -or $found.Title -like '*could not start*') { throw "No usable main window: $($found.Title)" }
                $results += [pscustomobject]@{ Variant=$variant; Run=$run; FirstExtraction=($run -eq 0); Milliseconds=[Math]::Round($timer.Elapsed.TotalMilliseconds,1); Title=$found.Title }
            } finally {
                if ($found.Window -ne [IntPtr]::Zero) { $null = [StartupWindows]::PostMessage($found.Window, 0x10, [IntPtr]::Zero, [IntPtr]::Zero) }
                if (!$process.WaitForExit(10000)) { $process.Kill(); $process.WaitForExit() }
                $process.Dispose()
            }
        }
    }
} finally { $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = $previousExtraction }
$json = $results | ConvertTo-Json
[IO.File]::WriteAllText((Join-Path $repository 'artifacts/bench/startup.json'), $json)
$results | Format-Table
Write-Host 'These are warm filesystem-cache measurements; FirstExtraction only means an empty bundle extraction cache.'
