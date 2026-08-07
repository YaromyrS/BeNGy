# Renders the BeNGy branch glyph into a multi-size PNG-compressed .ico for the
# system tray. Same geometry as Resources/AppIcon/appiconfg.svg, drawn white
# with a dark halo so it stays visible on both dark and light taskbars.
param(
    [string]$Out = (Join-Path (git rev-parse --show-toplevel) "src\BeNGy\Resources\Raw\trayicon.ico")
)

Add-Type -AssemblyName System.Drawing

function Draw-Glyph([System.Drawing.Graphics]$g, [float]$size, [System.Drawing.Color]$color, [float]$widthScale) {
    # Geometry in the 456-unit space of appiconfg.svg, scaled to $size.
    $s = $size / 456.0
    $lineW = 30 * $s * $widthScale
    $nodeW = 26 * $s * $widthScale
    $r = 38 * $s

    $pen = New-Object System.Drawing.Pen($color, $lineW)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    # trunk
    $g.DrawLine($pen, [float](180*$s), [float](160*$s), [float](180*$s), [float](296*$s))
    # branch curve
    $g.DrawBezier($pen, [float](180*$s), [float](248*$s), [float](180*$s), [float](198*$s), [float](210*$s), [float](168*$s), [float](252*$s), [float](168*$s))
    $pen.Dispose()

    $nodePen = New-Object System.Drawing.Pen($color, $nodeW)
    foreach ($c in @(@(180,118), @(180,338), @(296,168))) {
        $cx = $c[0]*$s; $cy = $c[1]*$s
        $g.DrawEllipse($nodePen, [float]($cx-$r), [float]($cy-$r), [float](2*$r), [float](2*$r))
    }
    $nodePen.Dispose()
}

function Render-Png([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # Dark halo first (slightly wider), then the white glyph on top.
    Draw-Glyph $g $size ([System.Drawing.Color]::FromArgb(160, 20, 20, 20)) 1.7
    Draw-Glyph $g $size ([System.Drawing.Color]::White) 1.0

    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    # The leading comma stops PowerShell from unrolling the byte[] into the pipeline.
    return ,$ms.ToArray()
}

$sizes = 16, 20, 24, 32, 48
$pngs = @{}
foreach ($sz in $sizes) { $pngs[$sz] = [byte[]](Render-Png $sz) }

# ICO container: ICONDIR + one ICONDIRENTRY per image + raw PNG blobs.
$ms = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter($ms)
$w.Write([uint16]0)                 # reserved
$w.Write([uint16]1)                 # type: icon
$w.Write([uint16]$sizes.Count)      # image count

$offset = 6 + 16 * $sizes.Count
foreach ($sz in $sizes) {
    $data = $pngs[$sz]
    $w.Write([byte]($(if ($sz -ge 256) { 0 } else { $sz })))  # width
    $w.Write([byte]($(if ($sz -ge 256) { 0 } else { $sz })))  # height
    $w.Write([byte]0)               # palette
    $w.Write([byte]0)               # reserved
    $w.Write([uint16]1)             # planes
    $w.Write([uint16]32)            # bpp
    $w.Write([uint32]$data.Length)  # size
    $w.Write([uint32]$offset)       # offset
    $offset += $data.Length
}
foreach ($sz in $sizes) { $w.Write([byte[]]$pngs[$sz]) }
$w.Flush()

[System.IO.File]::WriteAllBytes($Out, $ms.ToArray())
$w.Dispose(); $ms.Dispose()
"wrote $Out ($((Get-Item $Out).Length) bytes, sizes: $($sizes -join ','))"
