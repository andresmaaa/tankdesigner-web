using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace TankDesigner.Web.Services;

// Servicio encargado de generar un PDF a partir de HTML usando Chromium en modo headless
public class PdfRenderService
{
    // Método principal: recibe HTML y devuelve el PDF en bytes
    public async Task<byte[]> GenerarPdfDesdeHtmlAsync(string html)
    {
        // Validación básica: no se puede generar PDF sin HTML
        if (string.IsNullOrWhiteSpace(html))
            throw new InvalidOperationException("No hay HTML para generar el PDF.");

        // Busca la ruta del navegador (Chrome/Chromium)
        var navegador = ResolverRutaNavegador();
        if (string.IsNullOrWhiteSpace(navegador))
            throw new InvalidOperationException(
                "No se ha encontrado Chromium/Chrome en el sistema. " +
                "En Railway debe quedar instalado desde el Dockerfile.");

        // Carpeta temporal única para evitar conflictos entre ejecuciones
        var tempRoot = Path.Combine(Path.GetTempPath(), "tankdesigner-pdf", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        // Rutas del HTML temporal y del PDF generado
        var htmlPath = Path.Combine(tempRoot, "informe.html");
        var pdfPath = Path.Combine(tempRoot, "informe.pdf");

        try
        {
            // Prepara el HTML añadiendo estilos específicos para impresión
            var htmlFinal = PrepararHtmlParaPdf(html);

            // Guarda el HTML en disco (sin BOM para evitar problemas de encoding)
            await File.WriteAllTextAsync(htmlPath, htmlFinal, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            // Argumentos que se pasan a Chromium en modo headless
            var argumentos = new[]
            {
                "--headless=new", // modo headless moderno
                "--disable-gpu",
                "--no-sandbox", // necesario en Railway
                "--disable-dev-shm-usage", // evita problemas de memoria en contenedores
                "--allow-file-access-from-files",
                "--enable-local-file-accesses",
                "--no-first-run",
                "--no-default-browser-check",
                "--hide-scrollbars",
                "--run-all-compositor-stages-before-draw",
                "--virtual-time-budget=3000", // tiempo para renderizar
                "--no-pdf-header-footer", // sin cabecera/pie automático
                $"--print-to-pdf=\"{pdfPath}\"", // ruta de salida del PDF
                $"\"file://{htmlPath.Replace("\\", "/")}\"" // HTML de entrada
            };

            // Configuración del proceso que ejecuta Chromium
            var psi = new ProcessStartInfo
            {
                FileName = navegador,
                Arguments = string.Join(" ", argumentos),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = tempRoot
            };

            using var process = new Process { StartInfo = psi };

            // Lanza Chromium
            process.Start();

            // Lee salida estándar y errores en paralelo
            var stdOutTask = process.StandardOutput.ReadToEndAsync();
            var stdErrTask = process.StandardError.ReadToEndAsync();

            // Espera a que termine (máximo 45 segundos)
            var exited = await Task.Run(() => process.WaitForExit(45000));
            var stdOut = await stdOutTask;
            var stdErr = await stdErrTask;

            // Si no ha terminado a tiempo → error
            if (!exited)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                throw new InvalidOperationException("Chromium tardó demasiado en generar el PDF.");
            }

            // Si Chromium devuelve error → se lanza con logs
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "Chromium no pudo generar el PDF.\n" +
                    $"ExitCode: {process.ExitCode}\n" +
                    $"STDOUT: {stdOut}\n" +
                    $"STDERR: {stdErr}");
            }

            // Si no existe el PDF → algo ha fallado internamente
            if (!File.Exists(pdfPath))
            {
                throw new InvalidOperationException(
                    "Chromium terminó, pero no dejó el archivo PDF esperado.\n" +
                    $"STDOUT: {stdOut}\nSTDERR: {stdErr}");
            }

            // Devuelve el PDF como array de bytes
            return await File.ReadAllBytesAsync(pdfPath);
        }
        finally
        {
            // Limpieza de archivos temporales (muy importante en servidor)
            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    // Inserta CSS específico para impresión en el HTML
    private static string PrepararHtmlParaPdf(string html)
    {
        var printCss = """
<style>
    @page {
        size: A4;
        margin: 12mm;
    }

    html, body {
        margin: 0 !important;
        padding: 0 !important;
        background: #ffffff !important;
        -webkit-print-color-adjust: exact !important;
        print-color-adjust: exact !important;
    }

    body {
        overflow: visible !important;
    }

    /* Elementos que no deben aparecer en PDF */
    .preview-shell,
    .preview-frame,
    .ia-fab,
    .td-sidebar,
    .topbar,
    nav,
    .no-print {
        box-shadow: none !important;
    }

    /* Tablas bien formateadas */
    table {
        width: 100% !important;
        border-collapse: collapse !important;
    }

    /* Repetir cabecera de tabla en cada página */
    thead {
        display: table-header-group;
    }

    /* Evitar cortes dentro de filas */
    tr, td, th {
        page-break-inside: avoid !important;
    }

    /* Forzar salto de página */
    .page-break {
        page-break-before: always !important;
        break-before: page !important;
    }
</style>
""";

        // Si el HTML ya tiene <head>, se inserta ahí
        if (html.Contains("</head>", StringComparison.OrdinalIgnoreCase))
        {
            return html.Replace("</head>", $"{printCss}</head>", StringComparison.OrdinalIgnoreCase);
        }

        // Si no, se construye un HTML completo
        return $"""
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8" />
{printCss}
</head>
<body>
{html}
</body>
</html>
""";
    }

    // Busca la ruta del navegador según sistema operativo
    private static string ResolverRutaNavegador()
    {
        // Primero intenta variable de entorno (ideal en Railway)
        var env = Environment.GetEnvironmentVariable("CHROME_BIN");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
            return env;

        // Linux (Railway)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var linuxCandidates = new[]
            {
                "/usr/bin/chromium",
                "/usr/bin/chromium-browser",
                "/usr/bin/google-chrome",
                "/usr/bin/google-chrome-stable"
            };

            foreach (var path in linuxCandidates)
            {
                if (File.Exists(path))
                    return path;
            }
        }

        // Windows
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var windowsCandidates = new[]
            {
                Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(localAppData, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(programFiles, "Chromium", "Application", "chrome.exe"),
                Path.Combine(programFilesX86, "Chromium", "Application", "chrome.exe")
            };

            foreach (var path in windowsCandidates)
            {
                if (File.Exists(path))
                    return path;
            }
        }

        // Mac
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var macCandidates = new[]
            {
                "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
                "/Applications/Chromium.app/Contents/MacOS/Chromium"
            };

            foreach (var path in macCandidates)
            {
                if (File.Exists(path))
                    return path;
            }
        }

        // Si no encuentra nada → devuelve vacío (provoca error arriba)
        return string.Empty;
    }
}