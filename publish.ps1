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
    '-o', $outDir,
    '-p:DebugType=none',
    '-p:SatelliteResourceLanguages=en'
)

if ($FrameworkDependent) {
    # KHONG dung PublishSingleFile o day: khi co ca -r win-x64 lan PublishSingleFile,
    # SDK van copy toan bo runtime (265 file) vao build roi goi het vao exe ->
    # ra file 140 MB ma VAN doi may dich cai .NET Desktop Runtime.
    # Bo -r va bo single-file thi duoc 4 file / ~1.2 MB dung nghia framework-dependent.
    $publishArgs += '--self-contained'
    $publishArgs += 'false'
} else {
    $publishArgs += '-r'
    $publishArgs += 'win-x64'
    $publishArgs += '--self-contained'
    $publishArgs += 'true'
    $publishArgs += '-p:PublishSingleFile=true'
    $publishArgs += '-p:IncludeNativeLibrariesForSelfExtract=true'
    $publishArgs += '-p:EnableCompressionInSingleFile=true'
}

# Xoa NOI DUNG chu khong xoa ca thu muc: neu co tien trinh nao dang giu handle
# vao thu muc (Explorer, terminal dang cd vao day) thi Remove-Item ca thu muc se loi.
if (Test-Path $outDir) {
    Get-ChildItem $outDir -Force | Remove-Item -Recurse -Force
} else {
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
}

Write-Host "==> dotnet $($publishArgs -join ' ')" -ForegroundColor Cyan
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish that bai (exit $LASTEXITCODE)" }

$exePath = Join-Path $outDir $exeName
if (-not (Test-Path $exePath)) { throw "Khong tim thay $exePath" }

$totalMb = [Math]::Round(((Get-ChildItem $outDir -Recurse -File | Measure-Object Length -Sum).Sum) / 1MB, 2)
$fileCount = (Get-ChildItem $outDir -Recurse -File | Measure-Object).Count
Write-Host ""
Write-Host "OK: $outDir  ($fileCount file, tong $totalMb MB)" -ForegroundColor Green
if ($FrameworkDependent) {
    Write-Host "Luu y: ban nay YEU CAU may dich da cai .NET 10 Desktop Runtime." -ForegroundColor Yellow
    Write-Host "       Phai chep CA THU MUC, khong chi rieng file .exe." -ForegroundColor Yellow
}
Get-ChildItem $outDir | Select-Object Name, @{N='MB';E={[Math]::Round($_.Length/1MB,2)}} | Format-Table

if ($Install) {
    $installDir = Join-Path $env:LOCALAPPDATA 'Programs\IDE_Touch_Window'
    $installedExe = Join-Path $installDir $exeName

    Get-Process -Name 'IDE_Touch_Window' -ErrorAction SilentlyContinue | ForEach-Object {
        $_.Kill(); $_.WaitForExit(5000)
    }

    New-Item -ItemType Directory -Force -Path $installDir | Out-Null
    if ($FrameworkDependent) {
        Copy-Item (Join-Path $outDir '*') $installDir -Recurse -Force
    } else {
        Copy-Item $exePath $installedExe -Force
    }

    Write-Host "Da cai vao: $installedExe" -ForegroundColor Green
    Start-Process -FilePath $installedExe -WorkingDirectory $installDir
    Write-Host "Da chay. Tim icon co gai o khay he thong (goc phai duoi)." -ForegroundColor Green
}
