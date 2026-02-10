@echo off
setlocal enabledelayedexpansion

:: 1. Rutas y Temporales
set "original=%~1"
set "nombre=%~n1"
set "salida_pdf=%~dp1%nombre%_OCR.pdf"
set "temp_txt=%temp%\ocr_raw_%random%.txt"
set "meta_data=%temp%\ocr_meta_%random%.txt"
set "tipo_check=%temp%\file_type_%random%.txt"

if "%~1"=="" (
    echo [ERROR] Arrastra un PDF, Imagen o Documento de Office aqui.
    pause & exit /b 1
)

echo [SISTEMA] Analizando estructura binaria del archivo...

:: 2. DETECCIÓN REAL POR FIRMA (A prueba de errores de extension)
powershell -NoProfile -Command ^
    "$bytes = Get-Content '%original%' -Encoding Byte -TotalCount 8 -ErrorAction SilentlyContinue; ^
    if ($bytes) { ^
        $sig = [System.BitConverter]::ToString($bytes); ^
        if ($sig -match '^25-50-44-46') { 'PDF' } ^
        elseif ($sig -match '^FF-D8-FF|^89-50-4E-47|^47-49-46|^42-4D') { 'IMAGEN' } ^
        elseif ($sig -match '^50-4B-03-04') { 'OFFICE' } ^
        else { 'TEXTO' } ^
    } else { 'TEXTO' }" > "%tipo_check%"

set /p TIPO_REAL=<"%tipo_check%"
del "%tipo_check%"

echo >>> NATURALEZA DETECTADA: %TIPO_REAL%

:: 3. PROCESAMIENTO SEGÚN TIPO
set "archivo_a_procesar=%original%"

if "%TIPO_REAL%"=="OFFICE" (
    echo [CONVERSIÓN] Convirtiendo Word/Excel a PDF...
    powershell -NoProfile -Command ^
        "$obj = New-Object -ComObject Word.Application -ErrorAction SilentlyContinue; ^
        if ($obj) { ^
            $doc = $obj.Documents.Open('%original%'); ^
            $path = '%temp%\temp_conv.pdf'; ^
            $doc.SaveAs($path, 17); ^
            $doc.Close(); $obj.Quit(); 'OK' ^
        } else { 'FAIL' }" > "%tipo_check%"
    set /p status=<"%tipo_check%"
    if "!status!"=="OK" (set "archivo_a_procesar=%temp%\temp_conv.pdf")
)

:: 4. MOTOR OCR Y STREAMING (Idiomas: Indonesio, Inglés, Francés, Español, Catalán)
echo [OCR] Iniciando reconocimiento multi-idioma...
start "TESS-PDF" /b tesseract "%archivo_a_procesar%" "%~dp1%nombre%_OCR" -l ind+eng+fra+spa+cat pdf >nul 2>&1
start "TESS-TXT" /b tesseract "%archivo_a_procesar%" "%temp_txt:~0,-4%" -l ind+eng+fra+spa+cat >nul 2>&1

:stream_loop
    powershell -NoProfile -Command "Clear-Host; Write-Host 'LECTURA EN TIEMPO REAL: %nombre%' -ForegroundColor Yellow; Write-Host '---------------------------------------------------' -ForegroundColor Gray;"
    if exist "%temp_txt%" (
        powershell -NoProfile -Command "Get-Content '%temp_txt%' -Tail 15 | Write-Host -ForegroundColor Cyan"
    )
    timeout /t 1 >nul
    tasklist /FI "IMAGENAME eq tesseract.exe" | findstr /I "tesseract.exe" >nul
    if %ERRORLEVEL%==0 goto stream_loop

:: 5. EXTRACCIÓN INTELIGENTE DE METADATOS
echo.
echo [INTELIGENCIA] Analizando texto para metadatos...
powershell -NoProfile -Command ^
    "$txt = Get-Content '%temp_txt%' -Raw; ^
    $docType = 'Documento'; ^
    if ($txt -match 'Factura|Invoice|Facture') { $docType = 'Factura' } ^
    elseif ($txt -match 'Acte Notarié|Acta Notarial|Notarié') { $docType = 'Acta Notarial' } ^
    elseif ($txt -match 'Recibo|Receipt|Reçu') { $docType = 'Recibo' }; ^
    $regex = '\b[A-ZÁÉÍÓÚ][a-zñáéíóú]+ (?:[A-ZÁÉÍÓÚ][a-zñáéíóú]+\b\s?){1,2}'; ^
    $nombres = [regex]::matches($txt, $regex) | foreach {$_.Value.Trim()} | select -Unique; ^
    $lista = ($nombres -join ', '); ^
    if ($lista.Length -gt 150) { $lista = $lista.Substring(0,150) + '...' }; ^
    $docType; $lista" > "%meta_data%"

set /p final_type=<"%meta_data%"
for /f "skip=1 delims=" %%b in ('type "%meta_data%"') do set "final_names=%%b"

:: 6. ESCRITURA FINAL DE METADATOS
if exist "%salida_pdf%" (
    echo [METADATOS] Guardando informacion en el archivo final...
    exiftool -charset utf8 -overwrite_original ^
        "-Title=%final_type% - %nombre%" ^
        "-Subject=%final_names%" ^
        "-Description=Tipo: %final_type%. Personas detectadas: %final_names%" ^
        "%salida_pdf%" >nul 2>&1
)

echo.
echo ===================================================
echo  TIPO: %final_type%
echo  NOMBRES: %final_names%
echo ===================================================
echo PROCESO COMPLETADO. El archivo _OCR.pdf esta listo.
pause
del "%temp_txt%" "%meta_data%" "%tipo_check%" 2>nul