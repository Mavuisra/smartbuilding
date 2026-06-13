using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using OfficeOpenXml;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public static class RapportsExportService
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    static RapportsExportService()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    public static string ExportExcel(string sectionName, IReadOnlyList<string> headers, IEnumerable<string[]> rows)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SBMS", "Rapports");
        Directory.CreateDirectory(folder);

        var safeName = SanitizeFileName(sectionName);
        var path = Path.Combine(folder, $"rapport_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add(safeName.Length > 31 ? safeName[..31] : safeName);

        for (var c = 0; c < headers.Count; c++)
            sheet.Cells[1, c + 1].Value = headers[c];

        var row = 2;
        foreach (var data in rows)
        {
            for (var c = 0; c < data.Length && c < headers.Count; c++)
                sheet.Cells[row, c + 1].Value = data[c];
            row++;
        }

        sheet.Cells[1, 1, 1, headers.Count].Style.Font.Bold = true;
        sheet.Cells.AutoFitColumns();
        package.SaveAs(new FileInfo(path));
        return path;
    }

    public static string ExportCsv(string sectionName, IReadOnlyList<string> headers, IEnumerable<string[]> rows)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SBMS", "Rapports");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"rapport_{SanitizeFileName(sectionName)}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", headers.Select(Csv)));
        foreach (var data in rows)
            sb.AppendLine(string.Join(";", data.Select(Csv)));

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        return path;
    }

    public static bool PrintTable(string title, IReadOnlyList<string> headers, IEnumerable<string[]> rows)
    {
        var table = new Table { CellSpacing = 0 };
        foreach (var _ in headers)
            table.Columns.Add(new TableColumn());

        var headerGroup = new TableRowGroup();
        var headerRow = new TableRow { Background = Brushes.LightGray, FontWeight = FontWeights.SemiBold };
        foreach (var h in headers)
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run(h))) { Padding = new Thickness(4) });
        headerGroup.Rows.Add(headerRow);
        table.RowGroups.Add(headerGroup);

        var body = new TableRowGroup();
        foreach (var data in rows)
        {
            var row = new TableRow();
            foreach (var v in data)
                row.Cells.Add(new TableCell(new Paragraph(new Run(v))) { Padding = new Thickness(4) });
            body.Rows.Add(row);
        }
        table.RowGroups.Add(body);

        var doc = new FlowDocument
        {
            PagePadding = new Thickness(40),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 9
        };
        doc.Blocks.Add(new Paragraph(new Run(title)) { FontSize = 16, FontWeight = FontWeights.Bold });
        doc.Blocks.Add(new Paragraph(new Run($"Généré le {DateTime.Now.ToString("dd/MM/yyyy HH:mm", Fr)}")) { FontSize = 9, Foreground = Brushes.Gray });
        doc.Blocks.Add(table);

        var viewer = new FlowDocumentScrollViewer { Document = doc };
        var w = new Window
        {
            Title = "Impression — SBMS Rapports",
            Width = 900,
            Height = 600,
            Content = viewer,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        w.ShowDialog();
        return true;
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

    private static string Csv(string? value)
    {
        var v = value ?? "";
        return v.Contains(';') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }

    private static string SanitizeFileName(string name) =>
        string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
}
