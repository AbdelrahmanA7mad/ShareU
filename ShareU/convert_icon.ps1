Add-Type -AssemblyName System.Drawing
$pngPath = "D:\shareu\ShareU\Resources\app_icon.png"
$icoPath = "D:\shareu\ShareU\share.ico"

$bmp = [System.Drawing.Bitmap]::FromFile($pngPath)
$hIcon = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hIcon)

$stream = [System.IO.File]::Create($icoPath)
$icon.Save($stream)
$stream.Close()

$icon.Dispose()
$bmp.Dispose()
Write-Host "Converted $pngPath to $icoPath"
