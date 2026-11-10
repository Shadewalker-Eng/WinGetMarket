# Convert the approved PNG artwork into a multi-resolution Windows icon.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$source = [Drawing.Image]::FromFile((Join-Path $PSScriptRoot 'Assets/WinGetMarketIcon.png'))
$sizes = @(16,20,24,32,40,48,64,128,256)
$frames = @()
try {
    foreach ($size in $sizes) {
        $bitmap = [Drawing.Bitmap]::new($size, $size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        $stream = [IO.MemoryStream]::new()
        try {
            $graphics.Clear([Drawing.Color]::Transparent)
            $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($source, 0, 0, $size, $size)
            $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
            $frames += ,$stream.ToArray()
        } finally { $graphics.Dispose(); $bitmap.Dispose(); $stream.Dispose() }
    }
    $file = [IO.File]::Create((Join-Path $PSScriptRoot 'Assets/WinGetMarket.ico'))
    $writer = [IO.BinaryWriter]::new($file)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($i=0; $i -lt $sizes.Count; $i++) {
            $dimension = if ($sizes[$i] -eq 256) {0} else {$sizes[$i]}
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
            $offset += $frames[$i].Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
    } finally { $writer.Dispose() }
} finally { $source.Dispose() }
Write-Output 'Created WinGetMarket.ico: 16, 20, 24, 32, 40, 48, 64, 128, 256 pixels.'
