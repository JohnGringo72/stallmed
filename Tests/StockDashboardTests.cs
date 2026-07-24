using StallmedManager.Shared.Models;
using Xunit;

namespace StallmedManager.Tests
{
    public class StockDashboardTests
    {
        // ---- available = on_hand - committed ----
        [Theory]
        [InlineData(10, 0, 10)]
        [InlineData(10, 4, 6)]
        [InlineData(3, 8, -5)]   // η ζήτηση μπορεί να ξεπερνά το φυσικό -> αρνητικό διαθέσιμο
        [InlineData(0, 0, 0)]
        public void Available_IsOnHandMinusCommitted(int onHand, int committed, int expected)
        {
            Assert.Equal(expected, StockDashboardLogic.Available(onHand, committed));
        }

        // ---- to_order = max(0, reorder_point - (available + on_order)) ----
        [Theory]
        [InlineData(20, 5, 0, 15)]   // όριο 20, διαθέσιμο 5, τίποτα παραγγελμένο -> 15
        [InlineData(20, 5, 10, 5)]   // τα παραγγελμένα μετράνε στην κάλυψη
        [InlineData(20, 25, 0, 0)]   // πάνω από το όριο -> δεν χρειάζεται
        [InlineData(20, 5, 30, 0)]   // ήδη παραγγελμένα αρκετά -> ποτέ αρνητικό
        [InlineData(0, -5, 0, 5)]    // χωρίς όριο αλλά αρνητικό διαθέσιμο -> καλύπτει το έλλειμμα
        [InlineData(10, -3, 4, 9)]
        [InlineData(0, 0, 0, 0)]
        public void ToOrder_Formula(int reorderPoint, int available, int onOrder, int expected)
        {
            Assert.Equal(expected, StockDashboardLogic.ToOrder(reorderPoint, available, onOrder));
        }

        // ---- Ομαδοποίηση: Τρόφιμα = κωδικός F% (case-insensitive) ----
        [Theory]
        [InlineData("F12", true)]
        [InlineData("f7", true)]      // case-insensitive
        [InlineData(" F3", true)]     // αγνοεί αρχικά κενά
        [InlineData("A10", false)]
        [InlineData("GF1", false)]    // το F πρέπει να είναι ΠΡΩΤΟΣ χαρακτήρας
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsFood_CodeStartsWithF(string? code, bool expected)
        {
            Assert.Equal(expected, StockDashboardLogic.IsFood(code));
        }

        // ---- Χρωματική ένδειξη στήλης «Για παραγγελία» ----
        [Theory]
        [InlineData(-2, 5, 7, StockDashboardLogic.UrgencyUrgent)]  // εξαντλημένο διαθέσιμο + ζήτηση
        [InlineData(0, 3, 0, StockDashboardLogic.UrgencyUrgent)]   // μηδέν διαθέσιμο με δεσμεύσεις
        [InlineData(0, 0, 4, StockDashboardLogic.UrgencyUrgent)]   // μηδέν διαθέσιμο με έλλειμμα ορίου
        [InlineData(3, 0, 2, StockDashboardLogic.UrgencyLow)]      // κάτω από το όριο αλλά υπάρχει διαθέσιμο
        [InlineData(5, 2, 0, StockDashboardLogic.UrgencyOk)]       // όλα καλά
        [InlineData(0, 0, 0, StockDashboardLogic.UrgencyOk)]       // ανενεργό είδος: όχι ψευδο-επείγον
        public void Urgency_Levels(int available, int committed, int toOrder, string expected)
        {
            Assert.Equal(expected, StockDashboardLogic.Urgency(available, committed, toOrder));
        }

        // ---- Τα παράγωγα του DTO βγαίνουν από την ίδια φόρμουλα ----
        [Fact]
        public void Dto_DerivedFields_MatchLogic()
        {
            var item = new StockDashboardItemDto
            {
                CodePrick = "F5",
                ProductTypeCode = "PT1",
                OnHand = 10,
                Committed = 4,
                OnOrder = 2,
                ReorderPoint = 15
            };

            Assert.Equal(6, item.Available);          // 10 - 4
            Assert.Equal(7, item.ToOrder);            // max(0, 15 - (6 + 2))
            Assert.True(item.IsFood);
            Assert.Equal(StockDashboardLogic.UrgencyLow, item.Urgency);
        }
    }
}
