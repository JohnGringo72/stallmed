using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using StallmedManager.Server.Controllers;
using StallmedManager.Server.Models;
using StallmedManager.Server.Services;
using StallmedManager.Shared.Models;
using Xunit;

namespace StallmedManager.Tests
{
    public class QuoteConvertTests
    {
        private static StallmedContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<StallmedContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new StallmedContext(options);
        }

        private static QuotesController CreateController(StallmedContext context)
        {
            var config = new ConfigurationBuilder().Build();
            return new QuotesController(
                context,
                NullLogger<QuotesController>.Instance,
                new QuotePdfService(config),
                new QuoteEmailService(config, NullLogger<QuoteEmailService>.Instance),
                config);
        }

        private static Quote SeedQuote(StallmedContext context, string status)
        {
            // Ο πελάτης-νοσοκομείο είναι εγγραφή του πίνακα Doctors.
            var hospital = new Doctor
            {
                FullName = "Γενικό Νοσοκομείο Test",
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            context.Doctors.Add(hospital);
            context.SaveChanges();

            var quote = new Quote
            {
                QuoteNumber = "ΠΡ-SM-2026-0001",
                Company = "SM",
                Status = status,
                IssueDate = DateTime.Today,
                ValidUntil = DateTime.Today.AddDays(30),
                CustomerDoctorID = hospital.DoctorID,
                CustomerName = "Γενικό Νοσοκομείο Test",
                CustomerContact = "Υπεύθυνος Προμηθειών",
                CustomerPhone = "2101234567",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            context.Quotes.Add(quote);
            context.SaveChanges();

            context.QuoteLines.AddRange(
                new QuoteLine
                {
                    QuoteID = quote.QuoteID,
                    CodePrick = "D001",
                    ProductTypeCode = "PT01",
                    Quantity = 10,
                    UnitPrice = 12.5m,
                    VatRate = 6
                },
                new QuoteLine
                {
                    QuoteID = quote.QuoteID,
                    CodePrick = "D002",
                    ProductTypeCode = "PT02",
                    Quantity = 5,
                    UnitPrice = 20m,
                    VatRate = 6
                });
            context.SaveChanges();
            return quote;
        }

        [Fact]
        public async Task Convert_AcceptedQuote_CreatesOrderWithSameLinesAndLinksBoth()
        {
            using var context = CreateContext();
            var quote = SeedQuote(context, QuoteStatus.Accepted);
            var controller = CreateController(context);

            var result = await controller.Convert(quote.QuoteID, new QuoteActionRequest { UserID = 7 });

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var action = Assert.IsType<QuoteActionResult>(ok.Value);
            Assert.True(action.Success);
            Assert.NotNull(action.OrderID);

            // Η παραγγελία δημιουργήθηκε με τις ίδιες γραμμές
            var order = await context.DoctorOrders.SingleAsync();
            Assert.Equal("Open", order.OrderStatus);
            Assert.Equal("SM", order.Company);
            Assert.Equal("Γενικό Νοσοκομείο Test", order.DoctorName);
            Assert.Equal(quote.CustomerDoctorID, order.DoctorID); // σύνδεση με τον πελάτη στον πίνακα Doctors
            Assert.StartsWith("SM", order.OrderCode);

            var orderLines = await context.DoctorOrderLines
                .Where(l => l.OrderID == order.OrderID).OrderBy(l => l.CodePrick).ToListAsync();
            Assert.Equal(2, orderLines.Count);
            Assert.Equal("D001", orderLines[0].CodePrick);
            Assert.Equal(10, orderLines[0].QuantityRequested);
            Assert.Equal("D002", orderLines[1].CodePrick);
            Assert.Equal(5, orderLines[1].QuantityRequested);
            Assert.All(orderLines, l => Assert.Equal("Pending", l.LineStatus));
            Assert.All(orderLines, l => Assert.Equal(0, l.QuantityAllocated));

            // Αμφίδρομη σύνδεση + κλείδωμα προσφοράς
            var updated = await context.Quotes.SingleAsync();
            Assert.Equal(QuoteStatus.Converted, updated.Status);
            Assert.Equal(order.OrderID, updated.ConvertedOrderID);
        }

        [Theory]
        [InlineData(QuoteStatus.Draft)]
        [InlineData(QuoteStatus.Sent)]
        [InlineData(QuoteStatus.Rejected)]
        [InlineData(QuoteStatus.Expired)]
        [InlineData(QuoteStatus.Converted)]
        public async Task Convert_FromNonAcceptedStatus_IsRejected(string status)
        {
            using var context = CreateContext();
            var quote = SeedQuote(context, status);
            var controller = CreateController(context);

            var result = await controller.Convert(quote.QuoteID, new QuoteActionRequest());

            Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Empty(context.DoctorOrders);
            var unchanged = await context.Quotes.SingleAsync();
            Assert.Equal(status, unchanged.Status);
        }

        [Fact]
        public async Task Convert_DoesNotTouchStockTables()
        {
            using var context = CreateContext();
            var quote = SeedQuote(context, QuoteStatus.Accepted);
            var controller = CreateController(context);

            await controller.Convert(quote.QuoteID, new QuoteActionRequest());

            // Η μετατροπή δημιουργεί μόνο την παραγγελία -- καμία δέσμευση αποθέματος.
            // Το απόθεμα δεσμεύεται αργότερα από την κανονική ροή παραγγελιών.
            Assert.Empty(context.OrderAllocations);
            Assert.Empty(context.StockTransactions);
            Assert.Empty(context.StockReceipts);
        }

        [Fact]
        public async Task Accept_FromSent_SetsRespondedAt()
        {
            using var context = CreateContext();
            var quote = SeedQuote(context, QuoteStatus.Sent);
            var controller = CreateController(context);

            var result = await controller.Accept(quote.QuoteID, new QuoteActionRequest());

            Assert.IsType<OkObjectResult>(result.Result);
            var updated = await context.Quotes.SingleAsync();
            Assert.Equal(QuoteStatus.Accepted, updated.Status);
            Assert.NotNull(updated.RespondedAt);
        }

        [Fact]
        public async Task Accept_FromDraft_Succeeds()
        {
            using var context = CreateContext();
            var quote = SeedQuote(context, QuoteStatus.Draft);
            var controller = CreateController(context);

            var result = await controller.Accept(quote.QuoteID, new QuoteActionRequest());

            Assert.IsType<OkObjectResult>(result.Result);
            var updated = await context.Quotes.SingleAsync();
            Assert.Equal(QuoteStatus.Accepted, updated.Status);
            Assert.NotNull(updated.RespondedAt);
        }
    }
}
