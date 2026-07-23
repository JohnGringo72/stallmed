using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using StallmedManager.Shared.Models;
using Document = QuestPDF.Fluent.Document;

namespace StallmedManager.Server.Services
{
    // Στοιχεία εταιρείας για το επιστολόχαρτο -- διαβάζονται από το
    // appsettings.json (section "Companies:SM" / "Companies:BM").
    public class CompanyProfile
    {
        public string Name { get; set; } = "";
        public string? LegalName { get; set; }
        public string? VatNumber { get; set; }
        public string? Gemi { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? PostalCode { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? LogoPath { get; set; }
        public string? AccentColor { get; set; }
    }

    public class QuotePdfService
    {
        private readonly IConfiguration _config;

        public QuotePdfService(IConfiguration config)
        {
            _config = config;
        }

        public CompanyProfile GetCompanyProfile(string company)
        {
            var profile = _config.GetSection($"Companies:{company}").Get<CompanyProfile>();
            return profile ?? new CompanyProfile { Name = company };
        }

        public byte[] Generate(Quote quote, List<QuoteLineViewDto> lines)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var company = GetCompanyProfile(quote.Company);
            var accent = string.IsNullOrEmpty(company.AccentColor) ? "#2E75B6" : company.AccentColor;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(35);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            if (!string.IsNullOrEmpty(company.LogoPath) && File.Exists(company.LogoPath))
                            {
                                row.ConstantItem(140).MaxHeight(60).AlignLeft().AlignMiddle()
                                    .Image(company.LogoPath).FitArea();
                            }
                            else
                            {
                                row.ConstantItem(200).AlignLeft().AlignMiddle()
                                    .Text(company.Name).FontSize(16).Bold().FontColor(accent);
                            }

                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                if (!string.IsNullOrEmpty(company.LegalName))
                                    c.Item().AlignRight().Text(company.LegalName).FontSize(9).Bold();
                                var addressLine = string.Join(", ", new[] { company.Address, company.PostalCode, company.City }
                                    .Where(s => !string.IsNullOrEmpty(s)));
                                if (addressLine.Length > 0)
                                    c.Item().AlignRight().Text(addressLine).FontSize(8);
                                if (!string.IsNullOrEmpty(company.VatNumber))
                                    c.Item().AlignRight().Text($"ΑΦΜ: {company.VatNumber}").FontSize(8);
                                if (!string.IsNullOrEmpty(company.Gemi))
                                    c.Item().AlignRight().Text($"ΓΕΜΗ: {company.Gemi}").FontSize(8);
                                var contactLine = string.Join(" · ", new[] { company.Phone, company.Email }
                                    .Where(s => !string.IsNullOrEmpty(s)));
                                if (contactLine.Length > 0)
                                    c.Item().AlignRight().Text(contactLine).FontSize(8);
                            });
                        });
                        col.Item().PaddingTop(6).LineHorizontal(2).LineColor(accent);
                    });

                    page.Content().PaddingTop(12).Column(col =>
                    {
                        // Τίτλος + στοιχεία προσφοράς
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("ΠΡΟΣΦΟΡΑ").FontSize(18).Bold().FontColor(accent);
                                c.Item().Text($"Αριθμός: {quote.QuoteNumber}").FontSize(10).Bold();
                                c.Item().Text($"Ημερομηνία: {quote.IssueDate:dd/MM/yyyy}").FontSize(10);
                                c.Item().Text($"Ισχύς έως: {quote.ValidUntil:dd/MM/yyyy}").FontSize(10);
                            });

                            row.RelativeItem().Background("#F5F5F5").Padding(8).Column(c =>
                            {
                                c.Item().Text("Προς:").FontSize(9).Bold();
                                c.Item().Text(quote.CustomerName ?? "").FontSize(10).Bold();
                                if (!string.IsNullOrEmpty(quote.CustomerDepartment))
                                    c.Item().Text(quote.CustomerDepartment).FontSize(9);
                                if (!string.IsNullOrEmpty(quote.CustomerContact))
                                    c.Item().Text($"Υπόψη: {quote.CustomerContact}").FontSize(9);
                                if (!string.IsNullOrEmpty(quote.CustomerVat))
                                    c.Item().Text($"ΑΦΜ: {quote.CustomerVat}").FontSize(9);
                                var contact = string.Join(" · ", new[] { quote.CustomerPhone, quote.CustomerEmail }
                                    .Where(s => !string.IsNullOrEmpty(s)));
                                if (contact.Length > 0)
                                    c.Item().Text(contact).FontSize(9);
                                if (!string.IsNullOrEmpty(quote.HospitalRequestRef))
                                    c.Item().PaddingTop(3).Text($"Σχετ.: {quote.HospitalRequestRef}").FontSize(9).Italic();
                            });
                        });

                        col.Item().PaddingTop(10).Text(
                            "Σε απάντηση του αιτήματός σας, σας υποβάλλουμε την παρακάτω προσφορά. " +
                            "Παραμένουμε στη διάθεσή σας για κάθε διευκρίνιση.")
                            .FontSize(9);

                        // Πίνακας ειδών
                        col.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(25);   // Α/Α
                                columns.ConstantColumn(55);   // Κωδικός
                                columns.RelativeColumn();     // Περιγραφή
                                columns.ConstantColumn(45);   // Ποσότητα
                                columns.ConstantColumn(55);   // Τιμή μον.
                                columns.ConstantColumn(40);   // Έκπτ. %
                                columns.ConstantColumn(40);   // ΦΠΑ %
                                columns.ConstantColumn(60);   // Σύνολο
                            });

                            table.Header(header =>
                            {
                                void HeaderCell(string text, bool right = false)
                                {
                                    var cell = header.Cell().Background(accent)
                                        .PaddingVertical(4).PaddingHorizontal(3);
                                    (right ? cell.AlignRight() : cell)
                                        .Text(text).FontSize(8).Bold().FontColor("#FFFFFF");
                                }

                                HeaderCell("Α/Α");
                                HeaderCell("Κωδικός");
                                HeaderCell("Περιγραφή");
                                HeaderCell("Ποσ.", right: true);
                                HeaderCell("Τιμή μον.", right: true);
                                HeaderCell("Έκπτ.", right: true);
                                HeaderCell("ΦΠΑ", right: true);
                                HeaderCell("Σύνολο", right: true);
                            });

                            int rowIndex = 0;
                            foreach (var line in lines)
                            {
                                var bg = rowIndex % 2 == 0 ? "#FFFFFF" : "#F2F2F2";

                                void DataCell(string text, bool right = false)
                                {
                                    var cell = table.Cell().Background(bg)
                                        .BorderBottom(1).BorderColor("#E0E0E0")
                                        .PaddingVertical(4).PaddingHorizontal(3);
                                    (right ? cell.AlignRight() : cell).Text(text).FontSize(8);
                                }

                                var description = !string.IsNullOrEmpty(line.Description)
                                    ? line.Description
                                    : string.Join(" - ", new[] { line.AllergenDescription, line.ProductDescription }
                                        .Where(s => !string.IsNullOrEmpty(s)));

                                DataCell((rowIndex + 1).ToString());
                                DataCell(line.CodePrick);
                                DataCell(description ?? "");
                                DataCell($"{line.Quantity} {line.Unit}", right: true);
                                DataCell($"{line.UnitPrice:N2} €", right: true);
                                DataCell(line.DiscountPct > 0 ? $"{line.DiscountPct:N1}%" : "-", right: true);
                                DataCell($"{line.VatRate:N1}%", right: true);
                                DataCell($"{line.LineTotal:N2} €", right: true);

                                rowIndex++;
                            }
                        });

                        // Σύνολα
                        col.Item().PaddingTop(8).AlignRight().Column(c =>
                        {
                            void TotalRow(string label, decimal amount, bool bold = false)
                            {
                                c.Item().Row(row =>
                                {
                                    var l = row.ConstantItem(120).AlignRight().Text(label).FontSize(9);
                                    if (bold) l.Bold();
                                    var v = row.ConstantItem(80).AlignRight().Text($"{amount:N2} €").FontSize(9);
                                    if (bold) v.Bold();
                                });
                            }

                            TotalRow("Καθαρή αξία:", quote.Subtotal);
                            TotalRow("ΦΠΑ:", quote.VatTotal);
                            TotalRow("Γενικό σύνολο:", quote.Total, bold: true);
                        });

                        // Όροι
                        var terms = new List<(string, string?)>
                        {
                            ("Παράδοση", quote.TermsDelivery),
                            ("Πληρωμή", quote.TermsPayment),
                            ("Εγγύηση", quote.TermsWarranty),
                        }.Where(t => !string.IsNullOrEmpty(t.Item2)).ToList();

                        if (terms.Count > 0)
                        {
                            col.Item().PaddingTop(12).Column(c =>
                            {
                                c.Item().Text("Όροι").FontSize(10).Bold().FontColor(accent);
                                foreach (var (label, value) in terms)
                                    c.Item().Text($"• {label}: {value}").FontSize(9);
                            });
                        }

                        // Υπογραφή
                        col.Item().PaddingTop(25).AlignRight().Column(c =>
                        {
                            c.Item().AlignCenter().Text("Για την εταιρεία").FontSize(9);
                            c.Item().PaddingTop(35).AlignCenter().Text("(υπογραφή / σφραγίδα)").FontSize(8).Italic();
                        });
                    });

                    page.Footer().Column(col =>
                    {
                        col.Item().LineHorizontal(1).LineColor("#CCCCCC");
                        col.Item().PaddingTop(4).Row(row =>
                        {
                            row.RelativeItem().Text($"{company.Name} · Προσφορά {quote.QuoteNumber}").FontSize(8);
                            row.ConstantItem(80).AlignRight().Text(txt =>
                            {
                                txt.DefaultTextStyle(x => x.FontSize(8));
                                txt.Span("Σελίδα ");
                                txt.CurrentPageNumber();
                                txt.Span(" / ");
                                txt.TotalPages();
                            });
                        });
                    });
                });
            });

            using var stream = new MemoryStream();
            document.GeneratePdf(stream);
            return stream.ToArray();
        }
    }
}
