using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

/// <summary>Charte graphique PDF SBMS — vert institutionnel, rendu documentaire professionnel.</summary>
public static class PdfThemeHelper
{
    public const string BrandPrimary = "#2D6A4F";
    public const string BrandDark = "#1B4332";
    public const string BrandLight = "#40916C";
    public const string BrandSoft = "#52B788";
    public const string BrandMuted = "#E8F5EE";
    public const string BrandMutedAlt = "#D8EEE3";

    public const string TextPrimary = "#0F172A";
    public const string TextSecondary = "#475569";
    public const string TextMuted = "#94A3B8";
    public const string Border = "#C5DDD0";
    public const string BorderLight = "#E2EFE8";
    public const string Surface = "#FFFFFF";
    public const string SurfaceMuted = "#F6FAF8";
    public const string GrayBg = "#F8FAFC";

    // Compatibilité ascendante
    public const string NavyLight = BrandMuted;

    static PdfThemeHelper()
    {
        EnsureLicense();
    }

    public static void EnsureLicense() =>
        QuestPDF.Settings.License = LicenseType.Community;

    public static string ResolveHeaderColor() =>
        AppConfigurationService.Instance?.Current.PrimaryColorHex ?? BrandPrimary;

    public static string ResolveAccentColor() => ResolveHeaderColor();

    public static string ResolveTextColor() => TextPrimary;

    public static string ResolveCompanyName() =>
        AppConfigurationService.Instance?.Current.CompanyName ?? BuildingInfoDefaults.CompanyName;

    public static string ResolveCompanySubtitle() =>
        AppConfigurationService.Instance?.Current.AppSubtitle ?? AppConfiguration.DefaultAppSubtitle;

    public static string SanitizeFileName(string name) =>
        string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

    public readonly record struct PdfHeaderOptions(
        string DocumentTitle,
        string? DocumentSubtitle = null,
        string? DepartmentLine = null,
        string? BadgeText = null,
        IReadOnlyList<(string Label, string Value)>? Meta = null);

    public static void AccentBar(IContainer container)
    {
        var green = ResolveHeaderColor();
        container.Height(5).Background(green);
    }

    public static void DocumentHeader(IContainer container, PdfHeaderOptions options)
    {
        var green = ResolveHeaderColor();
        var text = ResolveTextColor();
        var company = ResolveCompanyName();
        var dept = options.DepartmentLine ?? ResolveCompanySubtitle();

        container.Column(col =>
        {
            col.Item().Element(AccentBar);

            col.Item().PaddingTop(14).Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(company).Bold().FontSize(15).FontColor(green);
                    left.Item().PaddingTop(2).Text(dept).FontSize(8).FontColor(TextSecondary);
                });

                row.RelativeItem(2).AlignCenter().Column(center =>
                {
                    center.Item().Text(options.DocumentTitle.ToUpperInvariant()).Bold().FontSize(13).FontColor(text);
                    if (!string.IsNullOrWhiteSpace(options.DocumentSubtitle))
                        center.Item().PaddingTop(2).Text(options.DocumentSubtitle).FontSize(8).FontColor(TextSecondary);

                    if (!string.IsNullOrWhiteSpace(options.BadgeText))
                    {
                        center.Item().PaddingTop(8).AlignCenter()
                            .Background(green).PaddingVertical(4).PaddingHorizontal(12)
                            .Text(options.BadgeText).Bold().FontSize(9).FontColor(Colors.White);
                    }
                });

                if (options.Meta is { Count: > 0 })
                    row.RelativeItem().Element(c => MetaCard(c, options.Meta));
                else
                    row.RelativeItem();
            });
        });
    }

    public static void MetaCard(IContainer container, IReadOnlyList<(string Label, string Value)> lines)
    {
        container.AlignRight().Background(SurfaceMuted).Border(1).BorderColor(BorderLight).Padding(10).Column(meta =>
        {
            foreach (var (label, value) in lines)
                MetaLine(meta, label, value);
        });
    }

    public static void SectionBox(IContainer container, string title, Action<ColumnDescriptor> content) =>
        SectionBox(container, title, ResolveHeaderColor(), content);

    public static void SectionBox(IContainer container, string title, string headerColor, Action<ColumnDescriptor> content)
    {
        container.Border(1).BorderColor(BorderLight).Column(col =>
        {
            col.Item().Background(headerColor).PaddingVertical(5).PaddingHorizontal(8)
                .Text(title.ToUpperInvariant()).Bold().FontSize(7.5f).FontColor(Colors.White);
            col.Item().Background(Surface).Padding(10).Column(content);
        });
    }

    public static void InfoLine(ColumnDescriptor col, string label, string value)
    {
        col.Item().PaddingBottom(4).Row(row =>
        {
            row.ConstantItem(92).Text($"{label}").FontSize(7.5f).FontColor(TextSecondary);
            row.RelativeItem().Text(string.IsNullOrWhiteSpace(value) ? "—" : value).FontSize(8.5f).FontColor(TextPrimary);
        });
    }

    public static void MetaLine(ColumnDescriptor col, string label, string value)
    {
        col.Item().PaddingBottom(3).Text(t =>
        {
            t.Span($"{label} : ").FontSize(7).FontColor(TextSecondary);
            t.Span(string.IsNullOrWhiteSpace(value) ? "—" : value).FontSize(8).SemiBold().FontColor(TextPrimary);
        });
    }

    public static void StatusBadge(IContainer container, string text)
    {
        var green = ResolveHeaderColor();
        container.Background(BrandMuted).Border(1).BorderColor(Border).PaddingVertical(2).PaddingHorizontal(8)
            .Text(text).Bold().FontSize(8).FontColor(green);
    }

    public static void KpiCard(IContainer container, string label, string value)
    {
        var green = ResolveHeaderColor();
        container.Border(1).BorderColor(BorderLight).Background(SurfaceMuted).Padding(10).Column(col =>
        {
            col.Item().Text(label).FontSize(7).FontColor(TextSecondary);
            col.Item().PaddingTop(4).Text(value).Bold().FontSize(11).FontColor(green);
        });
    }

    public static void KpiRow(IContainer container, IReadOnlyList<(string Label, string Value)> items)
    {
        container.Row(row =>
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0)
                    row.ConstantItem(8);
                var (label, value) = items[i];
                row.RelativeItem().Element(c => KpiCard(c, label, value));
            }
        });
    }

    public static void DataTableHeaderCell(IContainer cell, string text)
    {
        var green = ResolveHeaderColor();
        cell.Background(green).PaddingVertical(5).PaddingHorizontal(5)
            .Text(text).Bold().FontSize(7.5f).FontColor(Colors.White);
    }

    public static IContainer DataTableBodyCell(IContainer cell, bool alternate) =>
        cell.BorderBottom(1).BorderColor(BorderLight)
            .Background(alternate ? SurfaceMuted : Surface)
            .PaddingVertical(4).PaddingHorizontal(5);

    public static void DataTable(
        IContainer container,
        IReadOnlyList<string> headers,
        IEnumerable<string[]> rows,
        float? fontSize = 7.5f)
    {
        var list = rows.ToList();
        container.Border(1).BorderColor(BorderLight).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                foreach (var _ in headers)
                    c.RelativeColumn();
            });

            table.Header(header =>
            {
                foreach (var h in headers)
                    header.Cell().Element(c => DataTableHeaderCell(c, h));
            });

            var index = 0;
            foreach (var data in list)
            {
                var alternate = index % 2 == 1;
                foreach (var cell in data)
                {
                    table.Cell().Element(c => DataTableBodyCell(c, alternate))
                        .Text(cell ?? "—").FontSize(fontSize ?? 7.5f).FontColor(TextPrimary);
                }
                index++;
            }
        });
    }

    public static void AmountHighlight(IContainer container, string prefix, string amountText)
    {
        var green = ResolveHeaderColor();
        container.Text(t =>
        {
            t.Span(prefix).Italic().FontSize(8.5f).FontColor(TextSecondary);
            t.Span(amountText).Bold().FontSize(9).FontColor(green);
        });
    }

    public static void SignatureBlock(IContainer container, string title, Action<ColumnDescriptor>? extra = null)
    {
        SectionBox(container, title, col =>
        {
            extra?.Invoke(col);
            col.Item().PaddingTop(10).Height(48).BorderBottom(1).BorderColor(Border)
                .AlignBottom().PaddingBottom(4).Text(" ").FontSize(1);
        });
    }

    public static void DocumentFooter(IContainer container, string? extraLine = null)
    {
        var company = ResolveCompanyName();
        var config = AppConfigurationService.Instance?.Current;
        var contact = config is not null
            ? $"{config.Phone}  ·  {config.Email}"
            : null;

        container.Column(col =>
        {
            col.Item().LineHorizontal(1).LineColor(Border);
            col.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Text(config?.FullAddress ?? company).FontSize(7).FontColor(TextMuted);
                if (!string.IsNullOrWhiteSpace(contact))
                    row.RelativeItem().AlignCenter().Text(contact).FontSize(7).FontColor(TextMuted);
                row.RelativeItem().AlignRight().Text(company).FontSize(7).FontColor(TextMuted);
            });
            if (!string.IsNullOrWhiteSpace(extraLine))
                col.Item().PaddingTop(4).AlignCenter().Text(extraLine).FontSize(7).FontColor(TextMuted).Italic();
        });
    }

    public static void PageFooter(PageDescriptor page, string? leftText = null)
    {
        page.Footer().PaddingTop(8).Row(row =>
        {
            row.RelativeItem().Text(leftText ?? ResolveCompanyName()).FontSize(7).FontColor(TextMuted);
            row.RelativeItem().AlignCenter().Text(text =>
            {
                text.CurrentPageNumber().FontSize(7).FontColor(TextMuted);
                text.Span(" / ").FontSize(7).FontColor(TextMuted);
                text.TotalPages().FontSize(7).FontColor(TextMuted);
            });
            row.RelativeItem().AlignRight().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(7).FontColor(TextMuted);
        });
    }

    public static void ConfigurePage(PageDescriptor page, bool landscape = false, float margin = 32)
    {
        page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
        page.Margin(margin);
        page.DefaultTextStyle(x => x.FontSize(9).FontColor(ResolveTextColor()));
        PageFooter(page);
    }
}
