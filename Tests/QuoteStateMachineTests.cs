using StallmedManager.Shared.Models;
using Xunit;

namespace StallmedManager.Tests
{
    public class QuoteStateMachineTests
    {
        [Theory]
        [InlineData(QuoteStatus.Draft, QuoteStatus.Sent)]
        [InlineData(QuoteStatus.Draft, QuoteStatus.Accepted)]   // αποδοχή χωρίς αποστολή (εκτός συστήματος)
        [InlineData(QuoteStatus.Draft, QuoteStatus.Rejected)]
        [InlineData(QuoteStatus.Sent, QuoteStatus.Accepted)]
        [InlineData(QuoteStatus.Sent, QuoteStatus.Rejected)]
        [InlineData(QuoteStatus.Sent, QuoteStatus.Expired)]
        [InlineData(QuoteStatus.Expired, QuoteStatus.Draft)]
        [InlineData(QuoteStatus.Accepted, QuoteStatus.Converted)]
        public void AllowedTransitions_ReturnTrue(string from, string to)
        {
            Assert.True(QuoteStateMachine.CanTransition(from, to));
        }

        [Theory]
        [InlineData(QuoteStatus.Draft, QuoteStatus.Converted)]  // μετατροπή χωρίς αποδοχή
        [InlineData(QuoteStatus.Sent, QuoteStatus.Draft)]
        [InlineData(QuoteStatus.Sent, QuoteStatus.Converted)]
        [InlineData(QuoteStatus.Accepted, QuoteStatus.Sent)]
        [InlineData(QuoteStatus.Accepted, QuoteStatus.Rejected)]
        [InlineData(QuoteStatus.Rejected, QuoteStatus.Sent)]
        [InlineData(QuoteStatus.Rejected, QuoteStatus.Accepted)]
        [InlineData(QuoteStatus.Converted, QuoteStatus.Draft)]  // η Converted είναι τελική
        [InlineData(QuoteStatus.Converted, QuoteStatus.Sent)]
        [InlineData(QuoteStatus.Expired, QuoteStatus.Accepted)]
        [InlineData(QuoteStatus.Expired, QuoteStatus.Converted)]
        public void DisallowedTransitions_ReturnFalse(string from, string to)
        {
            Assert.False(QuoteStateMachine.CanTransition(from, to));
        }

        [Fact]
        public void UnknownOrNullStatus_CannotTransition()
        {
            Assert.False(QuoteStateMachine.CanTransition(null, QuoteStatus.Sent));
            Assert.False(QuoteStateMachine.CanTransition("Bogus", QuoteStatus.Sent));
        }

        [Theory]
        [InlineData(QuoteStatus.Draft, true)]
        [InlineData(QuoteStatus.Expired, true)]
        [InlineData(QuoteStatus.Sent, false)]
        [InlineData(QuoteStatus.Accepted, false)]
        [InlineData(QuoteStatus.Rejected, false)]
        [InlineData(QuoteStatus.Converted, false)]
        public void CanEdit_OnlyDraftAndExpired(string status, bool expected)
        {
            Assert.Equal(expected, QuoteStateMachine.CanEdit(status));
        }
    }

    public class QuoteCalculatorTests
    {
        [Fact]
        public void ComputeLine_NoDiscount()
        {
            var line = new QuoteLine { Quantity = 10, UnitPrice = 12.50m, DiscountPct = 0, VatRate = 6 };
            QuoteCalculator.ComputeLine(line);
            Assert.Equal(125.00m, line.LineNet);
            Assert.Equal(7.50m, line.LineVat);
            Assert.Equal(132.50m, line.LineTotal);
        }

        [Fact]
        public void ComputeLine_WithDiscount()
        {
            var line = new QuoteLine { Quantity = 4, UnitPrice = 100m, DiscountPct = 15, VatRate = 24 };
            QuoteCalculator.ComputeLine(line);
            Assert.Equal(340.00m, line.LineNet);   // 400 * 0.85
            Assert.Equal(81.60m, line.LineVat);    // 340 * 24%
            Assert.Equal(421.60m, line.LineTotal);
        }

        [Fact]
        public void ComputeTotals_SumsLines()
        {
            var quote = new Quote();
            var lines = new List<QuoteLine>
            {
                new() { Quantity = 10, UnitPrice = 12.50m, VatRate = 6 },
                new() { Quantity = 4, UnitPrice = 100m, DiscountPct = 15, VatRate = 24 },
            };
            QuoteCalculator.ComputeTotals(quote, lines);
            Assert.Equal(465.00m, quote.Subtotal);
            Assert.Equal(89.10m, quote.VatTotal);
            Assert.Equal(554.10m, quote.Total);
        }
    }
}
