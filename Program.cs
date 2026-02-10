using System;
using Tesseract;

// Ruta a tus datos de idioma (la que verificamos antes)
string tessDataPath = @"C:\Program Files\Tesseract-OCR\tessdata";
string imagePath = args[0]; // El archivo que le pases por clic derecho

using (var engine = new TesseractEngine(tessDataPath, "spa", EngineMode.Default))
{
    using (var img = Pix.LoadFromFile(imagePath))
    {
        using (var page = engine.Process(img))
        {
            string text = page.GetText();
            Console.WriteLine("Texto detectado:");
            Console.WriteLine(text);
            // Guarda el resultado en un archivo de texto
            System.IO.File.WriteAllText(imagePath + "_OCR.txt", text);
        }
    }
}
