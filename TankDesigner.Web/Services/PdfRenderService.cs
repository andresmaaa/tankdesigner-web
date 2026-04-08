using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace TankDesigner.Web.Services;

public class PdfRenderService
{
    public async Task<byte[]> GenerarPdfDesdeHtmlAsync(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            throw new InvalidOperationException("No hay HTML para generar el PDF.");

        var navegador = ResolverRutaNavegador();
        if (string.IsNullOrWhiteSpace(navegador))
            throw new InvalidOperationException(
                "No se ha encontrado Chromium/Chrome en el sistema. " +
                "En Railway debe quedar instalado desde el Dockerfile.");

        var tempRoot = Path.Combine(Path.GetTempPath(), "tankdesigner-pdf", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        var htmlPath = Path.Combine(tempRoot, "informe.html");
        var pdfPath = Path.Combine(tempRoot, "informe.pdf");

        try
        {
            var htmlFinal = PrepararHtmlParaPdf(html);
            await File.WriteAllTextAsync(htmlPath, htmlFinal, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var argumentos = new[]
            {
                "--headless=new",
                "--disable-gpu",
                "--no-sandbox",
                "--disable-dev-shm-usage",
                "--allow-file-access-from-files",
                "--enable-local-file-accesses",
                "--no-first-run",
                "--no-default-browser-check",
                "--hide-scrollbars",
                "--run-all-compositor-stages-before-draw",
                "--virtual-time-budget=3000",
                "--no-pdf-header-footer",
                $"--print-to-pdf=\"{pdfPath}\"",
                $"\"file://{htmlPath.Replace("\\", "/")}\""
            };

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

            process.Start();

            var stdOutTask = process.StandardOutput.ReadToEndAsync();
            var stdErrTask = process.StandardError.ReadToEndAsync();

            var exited = await Task.Run(() => process.WaitForExit(45000));
            var stdOut = await stdOutTask;
            var stdErr = await stdErrTask;

            if (!exited)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                throw new InvalidOperationException("Chromium tardó demasiado en generar el PDF.");
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "Chromium no pudo generar el PDF.\n" +
                    $"ExitCode: {process.ExitCode}\n" +
                    $"STDOUT: {stdOut}\n" +
                    $"STDERR: {stdErr}");
            }

            if (!File.Exists(pdfPath))
            {
                throw new InvalidOperationException(
                    "Chromium terminó, pero no dejó el archivo PDF esperado.\n" +
                    $"STDOUT: {stdOut}\nSTDERR: {stdErr}");
            }

            return await File.ReadAllBytesAsync(pdfPath);
        }
        finally
        {
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

    .preview-shell,
    .preview-frame,
    .ia-fab,
    .td-sidebar,
    .topbar,
    nav,
    .no-print {
        box-shadow: none !important;
    }

    table {
        width: 100% !important;
        border-collapse: collapse !important;
    }

    thead {
        display: table-header-group;
    }

    tr, td, th {
        page-break-inside: avoid !important;
    }

    .page-break {
        page-break-before: always !important;
        break-before: page !important;
    }
</style>
""";

        if (html.Contains("</head>", StringComparison.OrdinalIgnoreCase))
        {
            return html.Replace("</head>", $"{printCss}</head>", StringComparison.OrdinalIgnoreCase);
        }

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

    private static string ResolverRutaNavegador()
    {
        var env = Environment.GetEnvironmentVariable("CHROME_BIN");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
            return env;

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

        return string.Empty;
    }
}