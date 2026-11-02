# MiOCRPotente

Herramienta local para Windows que lee texto de imágenes y crea una copia con un nombre descriptivo. No envía documentos a Internet ni necesita una clave de IA.

## Funciones

- OCR en español e inglés para PNG, JPG/JPEG, TIFF y BMP.
- Clasificación local por reglas: factura, recibo, contrato, documento de identidad, extracto bancario, nómina, informe médico, carta, presupuesto o reserva.
- Añade una fecha encontrada y, en facturas, intenta obtener el proveedor.
- Crea una copia de la imagen con el nombre sugerido y un `.txt` con el texto OCR. El original no se modifica.

La clasificación es heurística. Si el texto no da señales claras, el programa usa `Documento`; no promete reconocer siempre nombres de personas o empresas.

## Requisitos

- Windows 10/11 x64.
- .NET 8 SDK para compilar; .NET 8 Runtime para ejecutar la publicación dependiente del runtime.
- Runtime de Microsoft Visual C++ 2019 x64, necesario para los componentes nativos del wrapper Tesseract.
- Modelos de idioma `spa.traineddata` y `eng.traineddata`.

## Abrir y preparar en Visual Studio Code

1. Instala Visual Studio Code y el SDK .NET 8.
2. Abre la carpeta del repositorio con **Archivo > Abrir carpeta**.
3. En la terminal integrada ejecuta:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\scripts\Install-Tessdata.ps1
   ```

4. Pulsa `Ctrl+Shift+B` y selecciona **MiOCRPotente: compilar**.

El script descarga `spa` y `eng` desde el repositorio oficial `tessdata_fast`. Los modelos no se guardan en Git; se copian junto al ejecutable al compilar/publicar.

También puedes compilar desde un terminal:

```powershell
dotnet restore MiOcrPotente.sln
dotnet build MiOcrPotente.sln -c Release
dotnet publish MiOcrPotente.csproj -c Release -r win-x64 --self-contained false
```

## Usar

```powershell
MiOcrPotente.exe "C:\Documentos\imagen.jpg"
MiOcrPotente.exe "C:\Documentos\imagen.jpg" --tessdata "C:\Herramientas\tessdata"
```

Se crearán, por ejemplo, `Factura_Empresa_Ejemplo_2026-11-02.jpg` y `Factura_Empresa_Ejemplo_2026-11-02.txt` junto a la imagen. Si ya existe ese nombre, añade `_2`, `_3`, etc.

## Límites actuales

PDF, escaneos multipágina, documentos Office, interfaz gráfica y revisión interactiva del nombre todavía no están implementados. Los scripts `.bat` y `.reg` antiguos incluían rutas personales; el `.bat` actual solo delega en el ejecutable.
