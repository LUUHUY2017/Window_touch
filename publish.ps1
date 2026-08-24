<#
.SYNOPSIS
    Dong goi IDE_Touch_Window thanh mot file .exe duy nhat.

.PARAMETER FrameworkDependent
    Xuat ban nhe (~vai MB) nhung may dich phai cai .NET 10 Desktop Runtime.
    Mac dinh la self-contained: exe chay duoc tren may Windows x64 sach.

.PARAMETER Install
    Sau khi publish thi chep exe vao %LOCALAPPDATA%\Programs\IDE_Touch_Window
    va chay len. Lan chay dau tien ung dung tu dang ky khoi dong cung Windows.

.EXAMPLE
    .\publish.ps1
    .\publish.ps1 -Install
    .\publish.ps1 -FrameworkDependent -Install
#>
[CmdletBinding()]
param(
    [switch]$FrameworkDependent,
    [switch]$Install
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$outDir = Join-Path $root 'publish'
$exeName = 'IDE_Touch_Window.exe'

$publishArgs = @(
    'publish', (Join-Path $root 'IDE_Touch_Window.csproj'),
    '-c', 'Release',
    '-r', 'win-x64',
    '-o', $outDir,
    '-p:PublishSingleFile=true',
    '-p:DebugType=none',
    '-p:SatelliteResourceLanguages=en'
)

if ($FrameworkDependent) {
    $publishArgs += '--self-contained'
    $publishArgs += 'false'
} else {
    $publishArgs += '--self-contained'
    $publishArgs += 'true'
    $publishArgs += '-p:IncludeNativeLibrariesForSelfExtract=true'
    $publishArgs += '-p:EnableCompressionInSingleFile=true'
}

if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }

Write-Host "==> dotnet $($publishArgs -join ' ')" -ForegroundColor Cyan
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish that bai (exit $LASTEXITCODE)" }

$exePath = Join-Path $outDir $exeName
if (-not (Test-Path $exePath)) { throw "Khong tim thay $exePath" }

$sizeMb = [Math]::Round((Get-Item $exePath).Length / 1MB, 1)
Write-Host ""
Write-Host "OK: $exePath  ($sizeMb MB)" -ForegroundColor Green
Get-ChildItem $outDir | Select-Object Name, @{N='MB';E={[Math]::Round($_.Length/1MB,2)}} | Format-Table

if ($Install) {
    $installDir = Join-Path $env:LOCALAPPDATA 'Programs\IDE_Touch_Window'
    $installedExe = Join-Path $installDir $exeName

    Get-Process -Name 'IDE_Touch_Window' -ErrorAction SilentlyContinue | ForEach-Object {
        $_.Kill(); $_.WaitForExit(5000)
    }

    New-Item -ItemType Directory -Force -Path $installDir | Out-Null
    Copy-Item $exePath $installedExe -Force

    Write-Host "Da cai vao: $installedExe" -ForegroundColor Green
    Start-Process -FilePath $installedExe -WorkingDirectory $installDir
    Write-Host "Da chay. Tim icon co gai o khay he thong (goc phai duoi)." -ForegroundColor Green
}
