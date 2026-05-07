[CmdletBinding()]
param(
    [int]$Port = 52500
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$apiPath = Join-Path $root "NovaAPI"
$binPath = Join-Path $apiPath "bin"
$buildOut = Join-Path $root "buildcheck\NovaAPI"
$perfLog = Join-Path $apiPath "nova_perf.log"
$errorLog = Join-Path $apiPath "nova_error.log"
$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
$iisExpress = "C:\Program Files\IIS Express\iisexpress.exe"
$dllPath = Join-Path $buildOut "NovaAPI.dll"
$pdbPath = Join-Path $buildOut "NovaAPI.pdb"
$novaRetailApiUrl = "http://localhost:52500"

Write-Host "Cerrando IIS Express..."
Get-Process iisexpress -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host "Limpiando logs..."
Remove-Item $perfLog -Force -ErrorAction SilentlyContinue
Remove-Item $errorLog -Force -ErrorAction SilentlyContinue

Write-Host "Compilando NovaAPI..."
& $msbuild `
    (Join-Path $apiPath "NovaAPI.csproj") `
    /t:Build `
    /p:Configuration=Debug `
    /p:OutDir="$buildOut\" `
    /nologo `
    /verbosity:minimal

$buildExitCode = $LASTEXITCODE

if (!(Test-Path $dllPath)) {
    throw "No se genero $dllPath"
}

if ($buildExitCode -ne 0) {
    Write-Warning "MSBuild devolvio codigo $buildExitCode, pero NovaAPI.dll si fue generado. Continuando."
}

Write-Host "Copiando binarios nuevos a bin..."
Copy-Item -Force $dllPath (Join-Path $binPath "NovaAPI.dll")

if (Test-Path $pdbPath) {
    Copy-Item -Force $pdbPath (Join-Path $binPath "NovaAPI.pdb")
}

if ($Port -ne 52500) {
    Write-Warning "NovaRetail esta configurado para usar $novaRetailApiUrl en NovaRetail\\MauiProgram.cs. Si levantas NovaAPI en $Port, la app no va a conectar hasta que cambies esa URL y recompiles NovaRetail."
}

Write-Host "Iniciando IIS Express en http://localhost:$Port ..."
& $iisExpress /path:$apiPath /port:$Port
