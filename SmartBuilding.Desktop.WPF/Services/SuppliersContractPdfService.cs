using System.Globalization;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public class SuppliersContractPdfService
{
    private const string NavyLight = PdfThemeHelper.NavyLight;
    private const string GrayBg = PdfThemeHelper.GrayBg;
    private const string Border = PdfThemeHelper.Border;

    private readonly string _navy = PdfThemeHelper.ResolveHeaderColor();
    private readonly string _green = PdfThemeHelper.ResolveAccentColor();

    static SuppliersContractPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string ExportContractDetails(SupplierListItem supplier)
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SBMS", "Fournisseurs");
        Directory.CreateDirectory(folder);

        var fileSafe = string.Concat(supplier.Name.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)));
        if (string.IsNullOrWhiteSpace(fileSafe))
            fileSafe = "fournisseur";

        var path = Path.Combine(folder, $"contrat_{fileSafe}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(_navy));

                page.Content().Column(root =>
                {
                    root.Item().Element(c => DrawHeader(c, supplier, culture));

                    root.Item().PaddingTop(12).Row(row =>
                    {
                        row.RelativeItem().Element(c => SectionBox(c, "INFORMATIONS FOURNISSEUR", col =>
                        {
                            InfoLine(col, "Nom", supplier.Name);
                            InfoLine(col, "Code", supplier.Code);
                            InfoLine(col, "Contact", supplier.ContactName);
                            InfoLine(col, "Téléphone", supplier.Phone);
                            InfoLine(col, "Email", supplier.Email);
                            InfoLine(col, "Bâtiment", supplier.Building);
                        }));

                        row.ConstantItem(12);

                        row.RelativeItem().Element(c => SectionBox(c, "INFORMATIONS CONTRAT", col =>
                        {
                            InfoLine(col, "N° contrat", supplier.ContractDisplay);
                            InfoLine(col, "Statut", supplier.ContractStatus);
                            InfoLine(col, "Date début", supplier.ContractStartDisplay);
                            InfoLine(col, "Date fin", supplier.ContractEndDisplay);
                            InfoLine(col, "Montant", supplier.ContractAmountDisplay);
                        }));
                    });

                    root.Item().PaddingTop(12).Element(c => SectionBox(c, "DÉTAILS FINANCIERS", col =>
                    {
                        col.Item().Border(1).BorderColor(Border).Column(table =>
                        {
                            table.Item().Background(_navy).Padding(8).Row(r =>
                            {
                                r.RelativeItem().Text("DÉSIGNATION").Bold().FontColor(Colors.White).FontSize(9);
                                r.ConstantItem(150).AlignRight().Text("VALEUR").Bold().FontColor(Colors.White).FontSize(9);
                            });
                            TableRow(table, "Montant contrat", supplier.ContractAmountDisplay);
                            TableRow(table, "Dépenses totales", supplier.TotalExpensesDisplay);
                            TableRow(table, "Statut", supplier.ContractStatus);
                        });
                    }));

                    root.Item().PaddingTop(10).Element(c => SectionBox(c, "DESCRIPTION", col =>
                    {
                        col.Item().Background(GrayBg).Padding(8).Text(
                            string.IsNullOrWhiteSpace(supplier.ContractDescription)
                                ? "Aucune description."
                                : supplier.ContractDescription);
                    }));

                    root.Item().PaddingTop(16).Element(c => SectionBox(c, "SIGNATURE & CACHET", col =>
                    {
                        col.Item().Text("Document généré par SBMS Immobilier SARL").FontSize(8).FontColor("#64748B");
                        col.Item().PaddingTop(12).Text("_________________________").FontSize(10);
                        col.Item().Text("Signature autorisée").SemiBold().FontSize(8);
                        col.Item().Text($"Fait le {DateTime.Now.ToString("dd/MM/yyyy HH:mm", culture)}").FontSize(7).FontColor("#64748B");
                    }));
                });
            });
        }).GeneratePdf(path);

        return path;
    }

    private void DrawHeader(IContainer container, SupplierListItem supplier, CultureInfo culture)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("SBMS").Bold().FontSize(20).FontColor(_navy);
                col.Item().Text("Smart Building Management System").FontSize(8).FontColor("#64748B");
            });

            row.RelativeItem(2).Column(col =>
            {
                col.Item().AlignCenter().Text("CONTRAT FOURNISSEUR").Bold().FontSize(16).FontColor(_navy);
                col.Item().AlignCenter().Text("FICHE CONTRACTUELLE").FontSize(9).FontColor("#64748B");
            });

            row.RelativeItem().AlignRight().Background(GrayBg).Border(1).BorderColor(Border).Padding(8).Column(meta =>
            {
                MetaLine(meta, "Document", "CONTRAT");
                MetaLine(meta, "Date", DateTime.Now.ToString("dd MMMM yyyy", culture));
                MetaLine(meta, "Heure", DateTime.Now.ToString("HH:mm", culture));
                meta.Item().PaddingTop(4).Background(NavyLight).Padding(4)
                    .Text($"Réf : {supplier.Code}").Bold().FontSize(8).FontColor(_green);
            });
        });
    }

    private void SectionBox(IContainer container, string title, Action<ColumnDescriptor> content)
        => PdfThemeHelper.SectionBox(container, title, _navy, content);

    private void InfoLine(ColumnDescriptor col, string label, string value)
        => PdfThemeHelper.InfoLine(col, label, value);

    private void MetaLine(ColumnDescriptor col, string label, string value)
        => PdfThemeHelper.MetaLine(col, label, value);

    private void TableRow(ColumnDescriptor col, string label, string value)
    {
        col.Item().BorderBottom(1).BorderColor(Border).PaddingVertical(6).PaddingHorizontal(8).Row(r =>
        {
            r.RelativeItem().Text(label).FontSize(9);
            r.ConstantItem(150).AlignRight().Text(value).FontSize(9);
        });
    }
}
