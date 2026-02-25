$dllPath = "bin\Debug\net8.0-windows\ShareU.dll"
if (Test-Path $dllPath) {
    $asm = [Reflection.Assembly]::LoadFrom((Resolve-Path $dllPath).Path)
    $stream = $asm.GetManifestResourceStream("ShareU.g.resources")
    if ($stream) {
        $reader = New-Object System.Resources.ResourceReader $stream
        $enumerator = $reader.GetEnumerator()
        while ($enumerator.MoveNext()) {
            Write-Host $enumerator.Key
        }
    } else {
        Write-Host "No g.resources found in $dllPath"
    }
} else {
    Write-Host "DLL not found at $dllPath"
}
