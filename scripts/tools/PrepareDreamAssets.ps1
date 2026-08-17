param(
    [string]$GeneratedRoot = 'C:\Users\duwei\.codex\generated_images\01a005da-091c-7e32-bc7a-bd4b12723852',
    [string]$OutputRoot = (Join-Path $PSScriptRoot '..\..\assets\generated\dream_escape')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputRoot)
[System.IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null

$icons = [ordered]@{
    'item-pin.png' = 'exec-511bb6a9-3950-4439-8985-fd9a2f25d1b8.png'
    'item-chalk.png' = 'exec-ad50a755-263e-4268-b7ea-54e9206debf6.png'
    'item-room-two-key.png' = 'exec-06f0122a-9ff8-4681-a61d-ef7fba41c921.png'
    'item-maintenance-key.png' = 'exec-afb62572-035f-48d9-81e3-b03adb36e8f5.png'
    'item-red-filter.png' = 'exec-8b647d93-ec19-4de7-9055-38afdc0440fd.png'
    'item-class-photo.png' = 'exec-71f9c5f8-7f38-4671-b6a9-455ef1870633.png'
    'item-tape-reel.png' = 'exec-bf64aeb2-655a-4aa0-a0c0-9f55c0aa2c7c.png'
    'item-recorder-crank.png' = 'exec-9544d5db-58b0-40bb-a6f9-bf01dc32765f.png'
}

function Convert-Icon {
    param([string]$Source, [string]$Destination, [bool]$RemoveGreen)
    $sourceBitmap = [System.Drawing.Bitmap]::new($Source)
    try {
        $working = [System.Drawing.Bitmap]::new($sourceBitmap.Width, $sourceBitmap.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($working)
            $graphics.DrawImageUnscaled($sourceBitmap, 0, 0)
            $graphics.Dispose()
            if ($RemoveGreen) {
                for ($y = 0; $y -lt $working.Height; $y++) {
                    for ($x = 0; $x -lt $working.Width; $x++) {
                        $color = $working.GetPixel($x, $y)
                        if ($color.G -gt 100 -and $color.G -gt ($color.R * 1.2) -and $color.G -gt ($color.B * 1.08)) {
                            $working.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
                        }
                    }
                }
            }

            $left = $working.Width
            $top = $working.Height
            $right = -1
            $bottom = -1
            for ($y = 0; $y -lt $working.Height; $y++) {
                for ($x = 0; $x -lt $working.Width; $x++) {
                    if ($working.GetPixel($x, $y).A -gt 8) {
                        if ($x -lt $left) { $left = $x }
                        if ($x -gt $right) { $right = $x }
                        if ($y -lt $top) { $top = $y }
                        if ($y -gt $bottom) { $bottom = $y }
                    }
                }
            }
            if ($right -lt $left -or $bottom -lt $top) { throw "No visible pixels in $Source" }
            $crop = [System.Drawing.Rectangle]::new($left, $top, $right - $left + 1, $bottom - $top + 1)
            $scale = [Math]::Min(56.0 / $crop.Width, 56.0 / $crop.Height)
            $width = [Math]::Max(1, [int][Math]::Round($crop.Width * $scale))
            $height = [Math]::Max(1, [int][Math]::Round($crop.Height * $scale))
            $target = [System.Drawing.Bitmap]::new(64, 64, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            try {
                $targetGraphics = [System.Drawing.Graphics]::FromImage($target)
                $targetGraphics.Clear([System.Drawing.Color]::Transparent)
                $targetGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $targetGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $destinationRect = [System.Drawing.Rectangle]::new([int]((64 - $width) / 2), [int]((64 - $height) / 2), $width, $height)
                $targetGraphics.DrawImage($working, $destinationRect, $crop, [System.Drawing.GraphicsUnit]::Pixel)
                $targetGraphics.Dispose()
                $target.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
            }
            finally { $target.Dispose() }
        }
        finally { $working.Dispose() }
    }
    finally { $sourceBitmap.Dispose() }
}

foreach ($entry in $icons.GetEnumerator()) {
    $source = Join-Path $GeneratedRoot $entry.Value
    if (-not [System.IO.File]::Exists($source)) { throw "Missing generated source: $source" }
    $removeGreen = $entry.Key -in @('item-pin.png', 'item-maintenance-key.png')
    Convert-Icon -Source $source -Destination (Join-Path $resolvedOutput $entry.Key) -RemoveGreen $removeGreen
}

$portraitSource = Join-Path $GeneratedRoot 'exec-ae141914-01ac-405d-9467-96344a506e29.png'
$portrait = [System.Drawing.Bitmap]::new($portraitSource)
try {
    $portraitTarget = [System.Drawing.Bitmap]::new(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($portraitTarget)
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $scale = [Math]::Min(244.0 / $portrait.Width, 244.0 / $portrait.Height)
        $width = [int][Math]::Round($portrait.Width * $scale)
        $height = [int][Math]::Round($portrait.Height * $scale)
        $graphics.DrawImage($portrait, [int]((256 - $width) / 2), 256 - $height, $width, $height)
        $graphics.Dispose()
        $portraitTarget.Save((Join-Path $resolvedOutput 'lin-xiaoyu-neutral.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $portraitTarget.Dispose() }
}
finally { $portrait.Dispose() }

function Resize-Image {
    param([string]$Source, [string]$Destination, [int]$Width, [int]$Height, [bool]$RemoveGreen)
    $sourceBitmap = [System.Drawing.Bitmap]::new($Source)
    try {
        $working = [System.Drawing.Bitmap]::new($sourceBitmap.Width, $sourceBitmap.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $workingGraphics = [System.Drawing.Graphics]::FromImage($working)
            $workingGraphics.DrawImageUnscaled($sourceBitmap, 0, 0)
            $workingGraphics.Dispose()
            if ($RemoveGreen) {
                for ($y = 0; $y -lt $working.Height; $y++) {
                    for ($x = 0; $x -lt $working.Width; $x++) {
                        $color = $working.GetPixel($x, $y)
                        if ($color.G -gt 100 -and $color.G -gt ($color.R * 1.2) -and $color.G -gt ($color.B * 1.08)) {
                            $working.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
                        }
                    }
                }
            }
            $target = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            try {
                $graphics = [System.Drawing.Graphics]::FromImage($target)
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.DrawImage($working, 0, 0, $Width, $Height)
                $graphics.Dispose()
                if ($RemoveGreen) {
                    for ($y = 0; $y -lt $target.Height; $y++) {
                        for ($x = 0; $x -lt $target.Width; $x++) {
                            $color = $target.GetPixel($x, $y)
                            if ($color.G -gt 70 -and $color.G -gt ($color.R * 1.18) -and $color.G -gt ($color.B * 1.06)) {
                                $target.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
                            }
                        }
                    }
                }
                $target.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
            }
            finally { $target.Dispose() }
        }
        finally { $working.Dispose() }
    }
    finally { $sourceBitmap.Dispose() }
}

Resize-Image -Source (Join-Path $GeneratedRoot 'exec-1c24aa51-5a65-4c1f-a86c-7a81eee89b78.png') `
    -Destination (Join-Path $resolvedOutput 'lin-xiaoyu-walk-4x3.png') -Width 128 -Height 144 -RemoveGreen $true
Resize-Image -Source (Join-Path $GeneratedRoot 'exec-cd63ee2e-0908-4ad6-9e5a-cc47867988bc.png') `
    -Destination (Join-Path $resolvedOutput 'class-photo-filter-closeup.png') -Width 640 -Height 360 -RemoveGreen $false
Resize-Image -Source (Join-Path $GeneratedRoot 'exec-e13ca812-9b8b-466a-b413-be4da4add4a4.png') `
    -Destination (Join-Path $resolvedOutput 'recorder-closeup.png') -Width 640 -Height 360 -RemoveGreen $false
Resize-Image -Source (Join-Path $GeneratedRoot 'exec-73dc3a22-6cd7-4d99-ae87-7907b42416d7.png') `
    -Destination (Join-Path $resolvedOutput 'auditorium-control-board.png') -Width 640 -Height 360 -RemoveGreen $false

Write-Output "DREAM_ASSETS_PREPARED icons=$($icons.Count) portrait=1 spriteSheets=1 closeups=3 output=$resolvedOutput"
