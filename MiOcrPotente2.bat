# Carpeta donde están tus 120 archivos .md
$inputFolder = "C:\Users\Ken\Iconos_en_.ico\3dicons-develop\3dicons-develop\content\3dicons-meta"
# Carpeta base para los iconos
$baseOutput = "C:\Users\Ken\Iconos_en_.ico\.ico"

# Crear subcarpetas para mantener el orden
$colorPath = Join-Path $baseOutput "Version_Color"
$premiumPath = Join-Path $baseOutput "Version_Premium"

if (!(Test-Path $colorPath)) { New-Item -ItemType Directory -Path $colorPath -Force }
if (!(Test-Path $premiumPath)) { New-Item -ItemType Directory -Path $premiumPath -Force }

Get-ChildItem -Path $inputFolder -Filter *.md | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    $name = $_.BaseName

    # 1. Buscar y descargar versión COLOR
    if ($content -match 'color: (https://\S+\.png)') {
        $urlColor = $matches[1]
        Write-Host "Descargando Color: $name" -ForegroundColor Cyan
        Invoke-WebRequest -Uri $urlColor -OutFile (Join-Path $colorPath "$name-color.png")
    }

    # 2. Buscar y descargar versión PREMIUM
    if ($content -match 'premium: (https://\S+\.png)') {
        $urlPremium = $matches[1]
        Write-Host "Descargando Premium: $name" -ForegroundColor Yellow
        Invoke-WebRequest -Uri $urlPremium -OutFile (Join-Path $premiumPath "$name-premium.png")
    }
}