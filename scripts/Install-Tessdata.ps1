[CmdletBinding()]
param(
    [string]$Destination,
    [string[]]$Languages = @("spa", "eng")
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    $PSScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
}
if ([string]::IsNullOrWhiteSpace($Destination)) {
    $Destination = Join-Path $PSScriptRoot "..\tessdata"
}
$destinationPath = [System.IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null

foreach ($language in $Languages) {
    if ($language -notmatch '^[a-z]{3,4}$') { throw "Codigo de idioma no valido: $language" }
    $fileName = "$language.traineddata"
    $target = Join-Path $destinationPath $fileName
    $uri = "https://github.com/tesseract-ocr/tessdata_fast/raw/main/$fileName"
    Write-Host "Descargando modelo oficial $language..."
    Invoke-WebRequest -Uri $uri -OutFile $target
    if ((Get-Item $target).Length -lt 100KB) { Remove-Item $target -Force; throw "El modelo $language descargado parece incompleto." }
    Write-Host "Instalado: $target"
}

Write-Host "Modelos listos. Ejecuta dotnet build o dotnet publish para copiarlos junto al programa."
