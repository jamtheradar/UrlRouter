<#
.SYNOPSIS
  Renders the application icon and the README/social artwork into src\Images\ and docs\.

.DESCRIPTION
  The mark is drawn in code rather than committed only as an opaque binary, so it can be
  re-rendered at any size and edited without a design tool. The outputs ARE committed — the build
  must not depend on this script running, and GDI+ rasterisation is not byte-identical across
  Windows versions, so rendering at build time would churn the binaries on every machine.

  The mark: one incoming link forking into two outgoing arrows, one cyan, one amber. That is
  literally what the application does, and it survives being shrunk to 16px, which is the size
  that actually decides an icon. No text and no thin strokes for the same reason.

  The palette is the sibling SQLExtended tool's, deliberately — same author, same family, and a
  dark rounded badge sits equally well on GitHub's light and dark themes.

  Outputs:
    src\Images\UrlRouter.ico    16/32/48/64/128/256, wired up via <ApplicationIcon>. This is what
                                Windows shows in Explorer and Settings, and what the tray icon
                                extracts from the running exe.
    docs\logo.png               512x512 mark alone.
    docs\wordmark.png           1024x256 lockup for the README header.
    docs\social-preview.png     1280x640 for GitHub's Settings > Social preview.

.PARAMETER Sheet
  Render a contact sheet of the mark at 256/64/32/16 to docs\icon-sheet.png instead of the
  production files. The 16px cell is the one to judge; everything else flatters the design.

.PARAMETER OutRoot
  Repository root. Defaults to the parent of this script's directory.
#>
[CmdletBinding()]
param(
    [switch] $Sheet,
    [string] $OutRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.Drawing

if (-not $OutRoot) { $OutRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path }

$ImagesDir = Join-Path $OutRoot 'src\Images'
$DocsDir   = Join-Path $OutRoot 'docs'
New-Item -ItemType Directory -Force -Path $ImagesDir, $DocsDir | Out-Null

$BgTop = [System.Drawing.ColorTranslator]::FromHtml('#1B2B36')
$BgBot = [System.Drawing.ColorTranslator]::FromHtml('#0C1720')
$Cyan  = [System.Drawing.ColorTranslator]::FromHtml('#4FC3F7')
$Amber = [System.Drawing.ColorTranslator]::FromHtml('#FFB74D')
$Ink   = [System.Drawing.ColorTranslator]::FromHtml('#EAF4FA')

# All geometry is normalised 0..1 and multiplied by the requested size, so every size is drawn
# natively rather than downscaled from one master bitmap - which is what keeps the 16px version
# crisp instead of muddy.
function P([double] $x, [double] $y, [double] $S) {
    New-Object System.Drawing.PointF -ArgumentList ([single]($x * $S)), ([single]($y * $S))
}

function New-RoundedPath([double] $x, [double] $y, [double] $w, [double] $h, [double] $r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function Draw-Backdrop($g, [double] $S) {
    $path = New-RoundedPath 0 0 $S $S ($S * 0.22)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF 0, 0),
        (New-Object System.Drawing.PointF ([single]$S), ([single]$S)),
        $BgTop, $BgBot)
    $g.FillPath($brush, $path)
    $brush.Dispose(); $path.Dispose()
}

# One arm: a stroke from the junction out to a filled arrowhead. The head is a separate triangle
# rather than a GDI+ arrow cap, because arrow caps scale with pen width and go ragged below about
# 24px - and 16px is where this icon has to survive.
#
# The head is rotated to sit square on the end of the arm, so a diagonal arm gets a diagonal head
# rather than the axis-aligned one that would read as sloppy at 256px.
function Draw-Arm($g, [double] $S, $colour, [double] $x0, [double] $y0, [double] $x1, [double] $y1, [double] $stroke, [bool] $withHead) {
    if (-not $withHead) {
        # Below about 24px an arrowhead is three or four pixels and reads as a smudge on the end
        # of the arm, so the arm is drawn as a plain round-capped stroke instead. Dropping detail
        # beats shrinking it: at this size the mark is a recognition cue, not an explanation.
        $pen = New-Object System.Drawing.Pen($colour, [single]($stroke * $S))
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $g.DrawLine($pen, (P $x0 $y0 $S), (P $x1 $y1 $S))
        $pen.Dispose()
        return
    }

    $head = $stroke * 1.30

    $dx = $x1 - $x0
    $dy = $y1 - $y0
    $len = [Math]::Sqrt($dx * $dx + $dy * $dy)
    $ux = $dx / $len
    $uy = $dy / $len

    # Stop the stroke inside the head so the two do not compound into a longer arrow than intended.
    $stopX = $x1 - $ux * $head * 0.72
    $stopY = $y1 - $uy * $head * 0.72

    $pen = New-Object System.Drawing.Pen($colour, [single]($stroke * $S))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($pen, (P $x0 $y0 $S), (P $stopX $stopY $S))
    $pen.Dispose()

    # Base of the head, and the perpendicular that gives it its width.
    $baseX = $x1 - $ux * $head
    $baseY = $y1 - $uy * $head
    $px = -$uy * $head * 0.88
    $py = $ux * $head * 0.88

    $brush = New-Object System.Drawing.SolidBrush($colour)
    $g.FillPolygon($brush, @(
        (P $x1 $y1 $S),
        (P ($baseX + $px) ($baseY + $py) $S),
        (P ($baseX - $px) ($baseY - $py) $S)
    ))
    $brush.Dispose()
}

function Draw-Mark($g, [double] $S) {
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    Draw-Backdrop $g $S

    # Thicker strokes at small sizes: a weight that reads as confident at 256px turns into grey
    # mush at 16px, so it is raised as the canvas shrinks.
    $detailed = $S -ge 24
    $stroke = if ($detailed) { if ($S -le 40) { 0.118 } else { 0.10 } } else { 0.155 }

    $jx = 0.33
    $jy = 0.5

    # Straight diagonal arms rather than stepped ones: fewer features means fewer things to
    # smear together at small sizes, and the Y reads as a fork at a glance.
    $tipX = if ($detailed) { 0.79 } else { 0.76 }
    $spread = if ($detailed) { 0.245 } else { 0.225 }

    Draw-Arm $g $S $Cyan  $jx $jy $tipX ($jy - $spread) $stroke $detailed
    Draw-Arm $g $S $Amber $jx $jy $tipX ($jy + $spread) $stroke $detailed

    # The incoming link, drawn last so it sits cleanly over the junction.
    $pen = New-Object System.Drawing.Pen($Ink, [single]($stroke * $S))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($pen, (P 0.16 $jy $S), (P $jx $jy $S))
    $pen.Dispose()

    # The junction node. Only slightly proud of the stroke - large enough to read as a decision
    # point, small enough not to become the whole icon at 16px.
    $brush = New-Object System.Drawing.SolidBrush($Ink)
    $r = $stroke * 0.72 * $S
    $g.FillEllipse($brush, [single]($jx * $S - $r), [single]($jy * $S - $r), [single]($r * 2), [single]($r * 2))
    $brush.Dispose()
}

function New-MarkBitmap([int] $size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    Draw-Mark $g $size
    $g.Dispose()
    return $bmp
}

# ---------------------------------------------------------------------------------------------
# ICO container.
#
# System.Drawing cannot write a multi-size .ico, so the container is assembled by hand: an
# ICONDIR, one 16-byte ICONDIRENTRY per size, then the images. Every frame is stored PNG-encoded,
# which Windows has supported since Vista and which avoids hand-rolling a DIB with its doubled
# height and AND mask. A width byte of 0 means 256 - the field is one byte.
function Write-Ico([int[]] $sizes, [string] $path) {
    $frames = foreach ($size in $sizes) {
        $bmp = New-MarkBitmap $size
        $stream = New-Object System.IO.MemoryStream
        $bmp.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        [pscustomobject]@{ Size = $size; Bytes = $stream.ToArray() }
        $stream.Dispose()
    }

    $out = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter($out)

    $w.Write([uint16]0)                  # reserved
    $w.Write([uint16]1)                  # type: icon
    $w.Write([uint16]$frames.Count)

    $offset = 6 + (16 * $frames.Count)
    foreach ($frame in $frames) {
        $dim = if ($frame.Size -ge 256) { 0 } else { $frame.Size }
        $w.Write([byte]$dim)             # width
        $w.Write([byte]$dim)             # height
        $w.Write([byte]0)                # palette size (0 = no palette)
        $w.Write([byte]0)                # reserved
        $w.Write([uint16]1)              # colour planes
        $w.Write([uint16]32)             # bits per pixel
        $w.Write([uint32]$frame.Bytes.Length)
        $w.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) { $w.Write($frame.Bytes) }

    $w.Flush()
    [System.IO.File]::WriteAllBytes($path, $out.ToArray())
    $w.Dispose(); $out.Dispose()

    Write-Host ("  {0}  ({1} frames: {2})" -f $path, $frames.Count, ($sizes -join ', '))
}

# ---------------------------------------------------------------------------------------------
# Wordmark and social artwork.

function Draw-Wordmark($g, [int] $width, [int] $height, [double] $markSize, [double] $markX, [double] $markY, [double] $fontSize, [bool] $withTagline, $inkColour) {
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    $state = $g.Save()
    $g.TranslateTransform([single]$markX, [single]$markY)
    Draw-Mark $g $markSize
    $g.Restore($state)

    $textX = $markX + $markSize + ($markSize * 0.30)

    # Segoe UI Semibold is on every supported Windows; the fallback keeps the script honest on a
    # machine where it is not.
    $family = try { New-Object System.Drawing.FontFamily('Segoe UI') } catch { [System.Drawing.FontFamily]::GenericSansSerif }
    $font = New-Object System.Drawing.Font($family, [single]$fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $brush = New-Object System.Drawing.SolidBrush($inkColour)

    $textY = if ($withTagline) { $markY + $markSize * 0.10 } else { $markY + ($markSize - $font.Height) / 2 }
    $g.DrawString('URL Router', $font, $brush, [single]$textX, [single]$textY)

    if ($withTagline) {
        $sub = New-Object System.Drawing.Font($family, [single]($fontSize * 0.36), [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
        $subBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(190, $Cyan))
        $g.DrawString('Every link to the right browser profile', $sub, $subBrush,
            [single]$textX, [single]($textY + $font.Height * 0.98))
        $sub.Dispose(); $subBrush.Dispose()
    }

    $font.Dispose(); $brush.Dispose()
}

# ---------------------------------------------------------------------------------------------

if ($Sheet) {
    $sizes = 256, 48, 32, 24, 16
    $pad = 24
    $width = [int]((($sizes | Measure-Object -Sum).Sum) + $pad * ($sizes.Count + 1))
    $sheetBmp = New-Object System.Drawing.Bitmap([int]$width, [int](256 + $pad * 2))
    $g = [System.Drawing.Graphics]::FromImage($sheetBmp)
    $g.Clear([System.Drawing.Color]::FromArgb(255, 245, 246, 248))

    $x = $pad
    foreach ($size in $sizes) {
        $bmp = New-MarkBitmap $size
        $g.DrawImage($bmp, [int]$x, [int]($pad + (256 - $size) / 2), [int]$size, [int]$size)
        $bmp.Dispose()
        $x += $size + $pad
    }
    $g.Dispose()

    $path = Join-Path $DocsDir 'icon-sheet.png'
    $sheetBmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $sheetBmp.Dispose()
    Write-Host "Contact sheet: $path"
    return
}

Write-Host "`n==> Icon"
Write-Ico @(256, 128, 64, 48, 32, 16) (Join-Path $ImagesDir 'UrlRouter.ico')

Write-Host "`n==> Artwork"

$logo = New-MarkBitmap 512
$logoPath = Join-Path $DocsDir 'logo.png'
$logo.Save($logoPath, [System.Drawing.Imaging.ImageFormat]::Png)
$logo.Dispose()
Write-Host "  $logoPath"

# Transparent background so the README header sits on GitHub's light and dark themes alike; the
# mark carries its own dark badge, so it needs no plate behind it.
# Two variants, because the wordmark sits on a transparent background and GitHub renders READMEs
# in both themes: near-white text would be invisible on the light one. The README picks between
# them with <picture media="(prefers-color-scheme: dark)">. The mark itself needs no variant - it
# carries its own dark badge.
$Slate = [System.Drawing.ColorTranslator]::FromHtml('#132430')
foreach ($variant in @(
    @{ File = 'wordmark.png';      Ink = $Slate },   # for light backgrounds
    @{ File = 'wordmark-dark.png'; Ink = $Ink }      # for dark backgrounds
)) {
    $word = New-Object System.Drawing.Bitmap(1024, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($word)
    Draw-Wordmark $g 1024 256 176 24 40 104 $false $variant.Ink
    $g.Dispose()
    $wordPath = Join-Path $DocsDir $variant.File
    $word.Save($wordPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $word.Dispose()
    Write-Host "  $wordPath"
}

# GitHub renders the social preview at 1280x640 and crops nothing, but it is shown as small as a
# link card, so the lockup is large and centred with generous margins.
$social = New-Object System.Drawing.Bitmap(1280, 640, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($social)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$plate = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.PointF 0, 0),
    (New-Object System.Drawing.PointF 1280, 640),
    [System.Drawing.ColorTranslator]::FromHtml('#132430'),
    [System.Drawing.ColorTranslator]::FromHtml('#080F16'))
$g.FillRectangle($plate, 0, 0, 1280, 640)
$plate.Dispose()
Draw-Wordmark $g 1280 640 232 232 204 96 $true $Ink
$g.Dispose()
$socialPath = Join-Path $DocsDir 'social-preview.png'
$social.Save($socialPath, [System.Drawing.Imaging.ImageFormat]::Png)
$social.Dispose()
Write-Host "  $socialPath"

Write-Host "`nDone. These outputs are committed; re-run only when the design changes."
