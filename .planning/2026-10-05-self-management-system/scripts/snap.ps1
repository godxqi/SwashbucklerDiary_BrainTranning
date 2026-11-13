param([int]$Count = 6, [int]$IntervalSeconds = 9)
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
$OutDir = "J:\Git\src\SwashbucklerDiary\.planning\2026-10-05-self-management-system\shots"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
Write-Output ("virtual desktop: {0}x{1}" -f $bounds.Width, $bounds.Height)
for ($i = 1; $i -le $Count; $i++) {
    $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $gfx.CopyFromScreen($bounds.X, $bounds.Y, 0, 0, $bmp.Size)
    $path = Join-Path $OutDir ("shot{0:d2}.png" -f $i)
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $gfx.Dispose(); $bmp.Dispose()
    Write-Output ("saved " + $path)
    if ($i -lt $Count) { Start-Sleep -Seconds $IntervalSeconds }
}
