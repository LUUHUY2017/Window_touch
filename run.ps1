$env:DOTNET_ROOT = "E:\LUUHUY\IDEVS\dotnet\net10.0\runtime"
$env:PATH = "E:\LUUHUY\IDEVS\dotnet\net10.0\runtime;" + $env:PATH
Start-Process -FilePath "$PSScriptRoot\bin\Debug\net10.0-windows\IDE_Touch_Window.exe" -WorkingDirectory "$PSScriptRoot\bin\Debug\net10.0-windows"
