$path = "d:\ELEMENT\VERIFLOW\src\Veriflow.Desktop\Assets\veriflow.ico"

if (-not (Test-Path $path)) {
    Write-Host "File not found: $path"
    exit
}

$bytes = [System.IO.File]::ReadAllBytes($path)
$count = [BitConverter]::ToInt16($bytes, 4)

Write-Host "File: $(Split-Path $path -Leaf)"
Write-Host "Image Count: $count"

for ($i = 0; $i -lt $count; $i++) {
    $offset = 6 + ($i * 16)
    $w = $bytes[$offset]
    $h = $bytes[$offset + 1]
    $bpp = [BitConverter]::ToInt16($bytes, $offset + 6)
    $size = [BitConverter]::ToInt32($bytes, $offset + 8)
    
    if ($w -eq 0) { $w = 256 }
    if ($h -eq 0) { $h = 256 }
    
    Write-Host "  #$($i+1): ${w}x${h} - $bpp bits - $size bytes"
}
