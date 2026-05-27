namespace SmartBuilding.Desktop.WPF.Models;

public class ModuleListRow
{
    public string Col0 { get; set; } = string.Empty;
    public string Col1 { get; set; } = string.Empty;
    public string Col2 { get; set; } = string.Empty;
    public string Col3 { get; set; } = string.Empty;
    public string Col4 { get; set; } = string.Empty;
    public string Col5 { get; set; } = string.Empty;

    public static ModuleListRow From(params string?[] values)
    {
        var row = new ModuleListRow();
        if (values.Length > 0) row.Col0 = values[0] ?? string.Empty;
        if (values.Length > 1) row.Col1 = values[1] ?? string.Empty;
        if (values.Length > 2) row.Col2 = values[2] ?? string.Empty;
        if (values.Length > 3) row.Col3 = values[3] ?? string.Empty;
        if (values.Length > 4) row.Col4 = values[4] ?? string.Empty;
        if (values.Length > 5) row.Col5 = values[5] ?? string.Empty;
        return row;
    }
}
