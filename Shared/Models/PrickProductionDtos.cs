namespace StallmedManager.Shared.Models
{
    public class ProductionOrderLineDto
    {
        public long ProductionOrderLineID { get; set; }
        public string CodePrick { get; set; }
        public string? AllergenDescription { get; set; }
        public string ProductTypeCode { get; set; }
        public string? ProductDescription { get; set; }
        public int QuantityOrdered { get; set; }
        public int QuantityReceived { get; set; }
        public string LineStatus { get; set; }
    }

    public class ProductionOrderDto
    {
        public long ProductionOrderID { get; set; }
        public string? ProductionOrderCode { get; set; }
        public string Company { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public string Status { get; set; }
        public string? Notes { get; set; }
        public List<ProductionOrderLineDto> Lines { get; set; } = new();
    }

    public class CreateProductionOrderLineRequest
    {
        public string CodePrick { get; set; }
        public string ProductTypeCode { get; set; }
        public int QuantityOrdered { get; set; }
    }

    public class CreateProductionOrderRequest
    {
        public string Company { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public string? Notes { get; set; }
        public int? CreatedBy { get; set; }
        public List<CreateProductionOrderLineRequest> Lines { get; set; } = new();
    }

    public class ReceiveStockRequest
    {
        public string CodePrick { get; set; }
        public string ProductTypeCode { get; set; }
        public DateTime ReceivedDate { get; set; }
        public int Quantity { get; set; }
        public string? Notes { get; set; }
        public int? CreatedBy { get; set; }
    }

    public class ReceiveStockResult
    {
        public long ReceiptID { get; set; }
        public int QuantityAppliedToOrders { get; set; }
        public int QuantityExcess { get; set; }
    }

    public class SimpleCodeOptionDto
    {
        public string Code { get; set; }
        public string? Description { get; set; }
        public string? DescriptionGreek { get; set; }
        public string? DescriptionOther { get; set; }
        public string? Company { get; set; }
    }

    public class StockCheckDto
    {
        public string CodePrick { get; set; }
        public string? Description { get; set; }
        public string ProductTypeCode { get; set; }
        public string? ProductDescription { get; set; }
        public string? Company { get; set; }
        public int TotalRemaining { get; set; }
    }
}
