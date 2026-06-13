using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public static class RapportsExportService
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public static string ExportExcel(string sectionName, IReadOnlyList<string> headers, IEnumerable<string[]> rows) =>
        ExportPdf(sectionName, headers, rows);

    public static string ExportPdf(string sectionName, IReadOnlyList<string> headers, IEnumerable<string[]> rows) =>
        PdfListExportService.Export(
            moduleFolder: "Rapports",
            filePrefix: $"rapport_{SanitizeFileName(sectionName)}",
            documentTitle: sectionName,
            documentSubtitle: "Rapport analytique SBMS",
            headers: headers,
            rows: rows);

    public static string ExportCsv(string sectionName, IReadOnlyList<string> headers, IEnumerable<string[]> rows) =>
        ExportPdf(sectionName, headers, rows);

    public static bool PrintTable(string title, IReadOnlyList<string> headers, IEnumerable<string[]> rows)
    {
        var path = ExportPdf(title, headers, rows);
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void SaveFilters(RapportsSavedFilters filters)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SBMS");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "rapports_filters.json");
        var json = System.Text.Json.JsonSerializer.Serialize(filters, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public static RapportsSavedFilters? LoadFilters()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SBMS", "rapports_filters.json");
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return System.Text.Json.JsonSerializer.Deserialize<RapportsSavedFilters>(json);
        }
        catch
        {
            return null;
        }
    }

    private static string SanitizeFileName(string name) =>
        string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
}
