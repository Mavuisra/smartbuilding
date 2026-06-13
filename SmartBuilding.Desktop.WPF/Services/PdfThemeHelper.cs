using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SmartBuilding.Domain.Entities.Building;

namespace SmartBuilding.Desktop.WPF.Services;

public static class PdfThemeHelper
{
    public const string Border = "#CBD5E1";
    public const string NavyLight = "#E8EEF5";
    public const string GrayBg = "#F8FAFC";

    public static string ResolveHeaderColor() =>
        AppConfigurationService.Instance?.Current.PdfHeaderHex ?? "#1B365D";

    public static string ResolveAccentColor() =>
        AppConfigurationService.Instance?.Current.PdfAccentHex ?? "#16A34A";

    public static string ResolveCompanyName() =>
        AppConfigurationService.Instance?.Current.CompanyName ?? BuildingInfoDefaults.CompanyName;

    public static void SectionBox(IContainer container, string title, string headerColor, Action<ColumnDescriptor> content)
    {
        container.Border(1).BorderColor(Border).Column(col =>
        {
            col.Item().Background(NavyLight).PaddingVertical(4).PaddingHorizontal(6)
                .Text(title).Bold().FontSize(7).FontColor(headerColor);
            col.Item().Padding(8).Column(content);
        });
    }

    public static void InfoLine(ColumnDescriptor col, string label, string value)
    {
        col.Item().PaddingBottom(3).Text(t =>
        {
            t.Span($"{label} : ").FontSize(7).FontColor("#64748B");
            t.Span(string.IsNullOrWhiteSpace(value) ? "—" : value).FontSize(8);
        });
    }

    public static void MetaLine(ColumnDescriptor col, string label, string value)
    {
        col.Item().Text(t =>
        {
            t.Span($"{label} : ").FontSize(7).FontColor("#64748B");
            t.Span(string.IsNullOrWhiteSpace(value) ? "—" : value).FontSize(8).SemiBold();
        });
    }
}
