using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using StallmedManager.Shared.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Helpers;
using Document = QuestPDF.Fluent.Document;

namespace StallmedManager.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExportController : ControllerBase
    {
        [HttpPost("orders-excel")]
        public IActionResult ExportOrdersToExcel([FromBody] List<WebOrder> orders)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Παραγγελίες");

            // ── HEADERS ──
            var headers = new[] { "ΦΑΡΜΑΚΕΙΟ", "ΑΣΘΕΝΗΣ", "ΙΑΤΡΟΣ", "ΗΜΕΡΟΜΗΝΙΑ", "ΠΟΣΟΤΗΤΑ", "Treatment" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontName = "Arial";
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2E75B6");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // ── GROUP BY Ασθενής + Θεραπεία, SUM Ποσότητα ──
            var grouped = orders
                .GroupBy(x => new
                {
                    Patient = x.Patient?.Trim() ?? "",
                    Treatment = x.TreatmentDescription?.Trim() ?? ""
                })
                .Select(g => new
                {
                    g.Key.Patient,
                    g.Key.Treatment,
                    Pharmacy = g.First().Pharmacy ?? "",
                    Doctor = g.First().Doctor ?? "",
                    LastDate = g.Max(x => x.Ordered),
                    TotalQNT = g.Sum(x => x.QNT ?? 0)
                })
                .OrderBy(x => x.Patient)
                .ThenBy(x => x.Treatment)
                .ToList();

            int row = 2;

            foreach (var item in grouped)
            {
                var bgColor = row % 2 == 0
                    ? XLColor.White
                    : XLColor.FromHtml("#EBF3FB");

                ws.Cell(row, 1).Value = item.Pharmacy;
                ws.Cell(row, 2).Value = item.Patient;
                ws.Cell(row, 3).Value = item.Doctor;

                if (item.LastDate.HasValue)
                {
                    ws.Cell(row, 4).Value = item.LastDate.Value;
                    ws.Cell(row, 4).Style.NumberFormat.Format = "dd/mm/yyyy";
                }

                ws.Cell(row, 5).Value = item.TotalQNT;
                ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(row, 6).Value = item.Treatment;

                ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = bgColor;
                ws.Range(row, 1, row, headers.Length).Style.Font.FontName = "Arial";

                row++;
            }

            // ── GRAND TOTAL ──
            var totalRange = ws.Range(row, 1, row, headers.Length);
            totalRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#2E75B6");
            totalRange.Style.Font.FontColor = XLColor.White;
            totalRange.Style.Font.Bold = true;
            totalRange.Style.Font.FontName = "Arial";

            ws.Range(row, 1, row, 4).Merge();
            ws.Cell(row, 1).Value = "ΓΕΝΙΚΟ ΣΥΝΟΛΟ";
            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(row, 5).Value = orders.Sum(x => x.QNT ?? 0);
            ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // ── BORDERS ──
            ws.Range(1, 1, row, headers.Length).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(1, 1, row, headers.Length).Style.Border.InsideBorder = XLBorderStyleValues.Hair;

            // ── COLUMN WIDTHS ──
            ws.Columns().AdjustToContents();
            ws.Column(1).Width = Math.Max(ws.Column(1).Width, 35);
            ws.Column(2).Width = Math.Max(ws.Column(2).Width, 25);
            ws.Column(3).Width = Math.Max(ws.Column(3).Width, 25);
            ws.Column(6).Width = Math.Max(ws.Column(6).Width, 30);

            ws.SheetView.FreezeRows(1);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Παραγγελίες_{DateTime.Today:dd-MM-yyyy}.xlsx");
        }

        [HttpPost("orders-pdf")]
        public IActionResult ExportOrdersToPdf([FromBody] PdfExportRequest request)
        {
            var orders = request.Orders;
            var doctor = orders.FirstOrDefault()?.Doctor ?? "";
            var fromDate = request.FromDate.ToString("d/M/yyyy");
            var toDate = request.ToDate.ToString("d/M/yyyy");
            var total = orders.Sum(x => x.QNT ?? 0);

            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(35);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item().Background("#2E75B6").Padding(10).Row(row =>
                        {
                            row.RelativeItem().Text(doctor)
                                .FontSize(14).Bold().FontColor("#FFFFFF");
                            row.ConstantItem(100).AlignRight()
                                .Text("Σύνολο").FontSize(10).FontColor("#FFFFFF");
                            row.ConstantItem(40).AlignRight()
                                .Text(total.ToString())
                                .FontSize(14).Bold().FontColor("#FFFFFF");
                        });

                        col.Item().Background("#2E75B6").PaddingBottom(10).PaddingRight(10)
                            .AlignRight().Text($"{fromDate} - {toDate}")
                            .FontSize(10).FontColor("#FFFFFF");

                        col.Item().PaddingTop(4).LineHorizontal(1).LineColor("#000000");
                    });

                    page.Content().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(60);   // Ordered
                            columns.ConstantColumn(60);   // Send
                            columns.RelativeColumn();     // Σκεύασμα
                            columns.RelativeColumn();     // Ασθενής
                            columns.ConstantColumn(25);   // Τεμ.
                        });

                        table.Header(header =>
                        {
                            void HeaderCell(string text)
                            {
                                header.Cell().BorderBottom(1).BorderColor("#000000")
                                    .PaddingVertical(4).PaddingHorizontal(3)
                                    .Text(text).FontSize(9).Bold();
                            }

                            HeaderCell("Παραγγελία");
                            HeaderCell("Αποστολή");
                            HeaderCell("Σκεύασμα");
                            HeaderCell("Ασθενής");
                            HeaderCell("Τεμ.");
                        });

                        int rowIndex = 0;
                        foreach (var order in orders.OrderBy(x => x.Patient))
                        {
                            var bg = rowIndex % 2 == 0 ? "#FFFFFF" : "#F2F2F2";

                            void DataCell(string text)
                            {
                                table.Cell().Background(bg)
                                    .BorderBottom(1).BorderColor("#E0E0E0")
                                    .PaddingVertical(4).PaddingHorizontal(3)
                                    .Text(text).FontSize(9);
                            }

                            DataCell(order.Ordered.HasValue
                                ? order.Ordered.Value.ToString("d/M/yyyy") : "");
                            DataCell(order.SendDateClient.HasValue
                                ? order.SendDateClient.Value.ToString("d/M/yyyy") : "");
                            DataCell(order.TreatmentDescription ?? "");
                            DataCell((order.Patient ?? "").Trim());
                            DataCell((order.QNT ?? 0).ToString());

                            rowIndex++;
                        }
                    });

                    page.Footer().Column(col =>
                    {
                        col.Item().LineHorizontal(1).LineColor("#000000");
                        col.Item().PaddingTop(5).Row(row =>
                        {
                            row.RelativeItem()
                                .Text($"Εκτύπωση: {DateTime.Today:dd/MM/yyyy}").FontSize(9);
                            row.ConstantItem(120).AlignRight()
                                .Text($"Σύνολο τεμαχίων: {total}").FontSize(9).Bold();
                        });
                    });
                });
            });

            var pdfStream = new MemoryStream();
            document.GeneratePdf(pdfStream);
            pdfStream.Position = 0;

            var fileName = $"{doctor}_{fromDate}-{toDate}.pdf"
                .Replace("/", "-").Replace(" ", "_");
            return File(pdfStream.ToArray(), "application/pdf", fileName);
        }
    }

    public class PdfExportRequest
    {
        public List<WebOrder> Orders { get; set; } = new();
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }
}