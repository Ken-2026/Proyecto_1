using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Tesseract;

internal static class Program
{
    private static readonly string[] SupportedExtensions =
        [".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp"];

    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            PrintUsage();
            return args.Length == 0 ? 2 : 0;
        }

        var inputPath = Path.GetFullPath(args[0]);
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"No se encuentra el archivo: {inputPath}");
            return 2;
        }

        var extension = Path.GetExtension(inputPath);
        if (!SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Formato no compatible. Esta versión admite imágenes PNG, JPG, TIFF y BMP.");
            Console.Error.WriteLine("Los PDF escaneados se añadirán cuando se incorpore un renderizador de páginas PDF.");
            return 2;
        }

        var tessdata = FindTessdata(args.Skip(1).ToArray());
        if (tessdata is null)
        {
            Console.Error.WriteLine("No encuentro tessdata/spa.traineddata.");
            Console.Error.WriteLine("Coloca spa.traineddata en la carpeta tessdata junto al programa o indica --tessdata <carpeta>.");
            Console.Error.WriteLine("Puedes descargar el idioma español desde https://github.com/tesseract-ocr/tessdata_fast");
            return 3;
        }

        try
        {
            using var engine = new TesseractEngine(tessdata, "spa+eng", EngineMode.Default);
            using var image = Pix.LoadFromFile(inputPath);
            using var page = engine.Process(image, PageSegMode.Auto);
            var text = page.GetText()?.Trim() ?? string.Empty;
            if (text.Length < 8)
            {
                Console.Error.WriteLine("El OCR ha extraído muy poco texto. Revisa la calidad de la imagen y que el idioma esté instalado.");
                return 4;
            }

            var classification = Classify(text);
            var safeName = BuildFileName(classification, text);
            var directory = Path.GetDirectoryName(inputPath)!;
            var outputPath = GetUniquePath(Path.Combine(directory, safeName + extension));
            var textPath = Path.ChangeExtension(outputPath, ".txt");

            // Conserva el original y crea la copia OCR con el nombre propuesto.
            File.Copy(inputPath, outputPath);
            File.WriteAllText(textPath, text + Environment.NewLine, new UTF8Encoding(false));

            Console.WriteLine($"Tipo detectado: {classification.Type}");
            if (classification.Entity is not null) Console.WriteLine($"Entidad detectada: {classification.Entity}");
            if (classification.Date is not null) Console.WriteLine($"Fecha detectada: {classification.Date}");
            Console.WriteLine($"Archivo: {outputPath}");
            Console.WriteLine($"Texto OCR: {textPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"No se pudo procesar el archivo: {ex.Message}");
            return 5;
        }
    }

    private static string? FindTessdata(string[] args)
    {
        string? requested = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--tessdata" && i + 1 < args.Length) requested = args[++i];
        }

        var candidates = new[]
        {
            requested,
            Environment.GetEnvironmentVariable("TESSDATA_PREFIX"),
            Path.Combine(AppContext.BaseDirectory, "tessdata"),
            Path.Combine(Environment.CurrentDirectory, "tessdata"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR", "tessdata"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tesseract-OCR", "tessdata")
        };
        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) &&
            File.Exists(Path.Combine(path, "spa.traineddata")));
    }

    private static (string Type, string? Entity, string? Date) Classify(string text)
    {
        var normalized = RemoveDiacritics(text).ToLowerInvariant();
        var rules = new (string Type, string[] Terms)[]
        {
            ("Factura", ["factura", "invoice", "importe total", "base imponible", "iva"]),
            ("Recibo", ["recibo", "justificante de pago", "pago recibido", "receipt"]),
            ("Contrato", ["contrato", "acuerdo entre", "las partes", "contract"]),
            ("Documento_Identidad", ["documento nacional de identidad", "dni", "passport", "pasaporte"]),
            ("Extracto_Bancario", ["extracto bancario", "saldo disponible", "movimientos de cuenta", "bank statement"]),
            ("Nomina", ["nomina", "salario bruto", "devengos", "retenciones"]),
            ("Informe_Medico", ["informe medico", "diagnostico", "historia clinica", "prescripcion"]),
            ("Carta", ["estimado/a", "dear ", "a quien corresponda", "muy senor mio"]),
            ("Presupuesto", ["presupuesto", "oferta economica", "quotation", "presupuesto valido"]),
            ("Reserva", ["reserva", "booking confirmation", "localizador", "check-in"])
        };

        var scores = rules.Select(rule => (rule.Type, Score: rule.Terms.Sum(term => CountOccurrences(normalized, term))))
            .OrderByDescending(item => item.Score).ToArray();
        var type = scores.Length > 0 && scores[0].Score > 0 ? scores[0].Type : "Documento";

        var dateMatch = Regex.Match(text,
            @"\b(?<date>(?:\d{1,2}[./-]\d{1,2}[./-](?:\d{2,4})|(?:19|20)\d{2}[./-]\d{1,2}[./-]\d{1,2}))\b");
        string? date = null;
        if (dateMatch.Success && DateTime.TryParse(dateMatch.Groups["date"].Value, CultureInfo.GetCultureInfo("es-ES"), DateTimeStyles.None, out var parsed))
            date = parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var entity = FindEntity(text, type);
        return (type, entity, date);
    }

    private static string? FindEntity(string text, string type)
    {
        if (type == "Factura")
        {
            var labeled = Regex.Match(text, @"(?im)^\s*(?:proveedor|empresa|emisor)\s*[:\-]\s*(?<value>[^\r\n]{2,60})");
            if (labeled.Success) return CleanEntity(labeled.Groups["value"].Value);
        }

        var taxId = Regex.Match(text, @"(?im)^\s*(?<value>[A-ZÁÉÍÓÚÑ&][A-ZÁÉÍÓÚÑ& .'-]{2,45}\s+(?:S\.?L\.?|S\.?A\.?|S\.?L\.?U\.?))\b");
        return taxId.Success ? CleanEntity(taxId.Groups["value"].Value) : null;
    }

    private static string BuildFileName((string Type, string? Entity, string? Date) classification, string text)
    {
        var parts = new List<string> { classification.Type };
        if (!string.IsNullOrWhiteSpace(classification.Entity)) parts.Add(classification.Entity);
        if (classification.Date is not null) parts.Add(classification.Date);
        if (parts.Count == 1)
        {
            var title = Regex.Match(text, @"(?m)^\s*(?<title>[^\r\n]{5,70})");
            if (title.Success) parts.Add(title.Groups["title"].Value);
        }
        var name = string.Join("_", parts.Select(SanitizeFilePart).Where(part => part.Length > 0));
        return name.Length > 120 ? name[..120].TrimEnd('_', '-', ' ') : name;
    }

    private static string SanitizeFilePart(string value)
    {
        var normalized = RemoveDiacritics(value).Normalize(NormalizationForm.FormKC);
        normalized = Regex.Replace(normalized, @"[^A-Za-z0-9_-]+", "_");
        return Regex.Replace(normalized.Trim('_', '-', ' '), "_+", "_");
    }

    private static string CleanEntity(string value) => Regex.Replace(value.Trim(), @"\s+", " ").Trim(' ', '.', ',', ';', ':');

    private static string RemoveDiacritics(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark).ToArray());
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0) { count++; offset += value.Length; }
        return count;
    }

    private static string GetUniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        var directory = Path.GetDirectoryName(path)!;
        var extension = Path.GetExtension(path);
        var stem = Path.GetFileNameWithoutExtension(path);
        for (var index = 2; ; index++)
        {
            var candidate = Path.Combine(directory, $"{stem}_{index}{extension}");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("MiOCRPotente: extrae texto de una imagen y crea una copia con nombre descriptivo.");
        Console.WriteLine("Uso: MiOcrPotente <imagen> [--tessdata <carpeta>]");
        Console.WriteLine("Formatos: PNG, JPG, TIFF y BMP. Mantiene intacto el archivo original.");
    }
}
