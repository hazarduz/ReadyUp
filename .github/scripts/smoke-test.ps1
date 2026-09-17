# Launches the published ReadyUp.exe and verifies it actually shows its
# main window instead of crashing or getting stuck on an error dialog.
#
# Process.MainWindowTitle alone isn't reliable here: when a startup
# exception fires an error MessageBox, the underlying library window can
# still be the one .NET reports as "main" while the dialog sits on top of
# it - an earlier version of this check reported success while the app was
# actually stuck on a fatal error dialog. Instead, enumerate every
# top-level window belonging to the process and check all of their titles.

param(
    [string]$ExePath = "publish/win-x64/ReadyUp.exe"
)

$ErrorActionPreference = "Stop"

Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;

public static class WinEnum
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public static List<string> GetTitlesForProcess(int pid)
    {
        var titles = new List<string>();
        EnumWindows((hWnd, lParam) =>
        {
            GetWindowThreadProcessId(hWnd, out uint windowPid);
            if (windowPid == (uint)pid && IsWindowVisible(hWnd))
            {
                var sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, 256);
                if (sb.Length > 0) titles.Add(sb.ToString());
            }
            return true;
        }, IntPtr.Zero);
        return titles;
    }
}
'@

# Seed one discoverable "game" so the scan actually populates the library
# grid before we check anything. Without this, CI has zero games to scan,
# so ItemsControl never materializes a single GameTileControl - which is
# exactly how a previous version of this smoke test missed a XAML binding
# crash that only fired once a real game tile got laid out.
$gameDir = "C:\Games\SmokeTestGame"
New-Item -ItemType Directory -Path $gameDir -Force | Out-Null
Copy-Item -Path "$env:WINDIR\System32\notepad.exe" -Destination "$gameDir\SmokeTestGame.exe" -Force

$proc = Start-Process -FilePath $ExePath -PassThru
Start-Sleep -Seconds 10
$proc.Refresh()

if ($proc.HasExited) {
    Write-Error "ReadyUp exited within 10 seconds of starting (exit code $($proc.ExitCode)) - it likely crashed on startup before showing any window."
    exit 1
}

$titles = [WinEnum]::GetTitlesForProcess($proc.Id)
Write-Host "Visible windows: $($titles -join ', ')"
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue

$errorWindow = $titles | Where-Object { $_ -match "Error" -or $_ -match "Exception" }
if ($errorWindow) {
    Write-Error "ReadyUp showed an error dialog on startup: '$errorWindow'"
    exit 1
}

if (-not ($titles -contains "ReadyUp")) {
    Write-Error "No window titled exactly 'ReadyUp' was found among: $($titles -join ', ')"
    exit 1
}

Write-Host "Smoke test passed: 'ReadyUp' window is showing, no error dialogs."
