@echo off
setlocal
if "%~1"=="" (
  echo Arrastra una imagen PNG, JPG, TIFF o BMP sobre este archivo.
  echo Uso: MiOcrPotente.bat "C:\ruta\documento.jpg"
  pause
  exit /b 2
)

set "APP=%~dp0MiOcrPotente.exe"
if not exist "%APP%" (
  echo No encuentro MiOcrPotente.exe junto a este script.
  echo Compila el proyecto o copia aqui el ejecutable publicado.
  pause
  exit /b 3
)

"%APP%" "%~1" %2 %3 %4 %5 %6 %7 %8 %9
if errorlevel 1 pause
exit /b %ERRORLEVEL%
