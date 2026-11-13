param([int]$Count = 6, [int]$IntervalSeconds = 9, [string]$ProcName = "SwashbucklerDiary.Maui", [int]$WinWidth = 1300, [int]$WinHeight = 900)
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
$src = @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class W {
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    public delegate bool EnumProc(IntPtr h, IntPtr p);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public static List<IntPtr> WindowsOf(uint pid) {
        var found = new List<IntPtr>();
        EnumWindows((h, p) => { uint w; GetWindowThreadProcessId(h, out w); if (w == pid && IsWindowVisible(h)) found.Add(h); return true; }, IntPtr.Zero);
        return found;
    }
    public static IntPtr Biggest(uint pid) {
        IntPtr best = IntPtr.Zero; long bestArea = 0;
        foreach (var h in WindowsOf(pid)) { RECT r; if (!GetWindowRect(h, out r)) continue;
            long a = (long)(r.Right - r.Left) * (r.Bottom - r.Top); if (a > bestArea) { bestArea = a; best = h; } }
        return best;
    }
}
"@
Add-Type -TypeDefinition $src

$OutDir = "J:\Git\src\SwashbucklerDiary\.planning\2026-10-05-self-management-system\shots"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds

for ($i = 1; $i -le $Count; $i++) {
    $proc = Get-Process -Name $ProcName -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $proc) { Write-Output "app not running at shot $i"; break }
    $hwnd = [W]::Biggest([uint32]$proc.Id)
    if ($hwnd -eq [IntPtr]::Zero) { Write-Output "no window at shot $i"; break }
    # 不要最大化：本进程是 DPI-unaware，看到的虚拟桌面只有物理屏幕的 1/1.5，
    # 最大化会让窗口比"能截到的范围"宽 —— 截图只剩窗口左边 2/3，右边内容全丢。
    # 改成一个确定装得下的尺寸（0,0 起，1300x900 虚拟 px），整窗才都在画面里。
    [W]::SetWindowPos($hwnd, [IntPtr](-1), 0, 0, $WinWidth, $WinHeight, 0x0040) | Out-Null
    Start-Sleep -Milliseconds 1500
    $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $gfx.CopyFromScreen($bounds.X, $bounds.Y, 0, 0, $bmp.Size)
    $path = Join-Path $OutDir ("shot{0:d2}.png" -f $i)
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $gfx.Dispose(); $bmp.Dispose()
    [W]::SetWindowPos($hwnd, [IntPtr](-2), 0, 0, 0, 0, 0x0043) | Out-Null
    Write-Output ("saved " + $path)
    if ($i -lt $Count) { Start-Sleep -Seconds $IntervalSeconds }
}
