using System.IO;
using System.Text;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public static class VisitsExportService
{
    public static string ExportCsv(IReadOnlyList<VisitListItem> visits)
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"visites_{DateTime.Now:yyyyMMdd_HHmm}.csv");

        var sb = new StringBuilder();
        sb.AppendLine("Code;Visiteur;Téléphone;Personne visitée;Motif;Type;Entrée;Sortie;Statut;Badge;Zone");
        foreach (var v in visits)
        {
            sb.Append(Csv(v.VisitCode)).Append(';')
                .Append(Csv(v.FullName)).Append(';')
                .Append(Csv(v.Phone)).Append(';')
                .Append(Csv(v.HostName)).Append(';')
                .Append(Csv(v.Purpose)).Append(';')
                .Append(Csv(v.VisitType)).Append(';')
                .Append(Csv(v.CheckInDisplay)).Append(';')
                .Append(Csv(v.CheckOutDisplay)).Append(';')
                .Append(Csv(v.AccessStatus)).Append(';')
                .Append(Csv(v.BadgeNumber)).Append(';')
                .AppendLine(Csv(v.Zone));
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        return path;
    }

    private static string Csv(string? value) =>
        $"\"{(value ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
