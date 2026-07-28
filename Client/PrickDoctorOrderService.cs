using StallmedManager.Shared.Models;

namespace StallmedManager.Client
{
    public class PrickDoctorOrderService
    {
        private readonly DataService dataService;

        public PrickDoctorOrderService(DataService dataService)
        {
            this.dataService = dataService;
        }

        public async Task<List<DoctorOrderViewDto>> GetOrders(string? company, int? doctorId, string? status)
        {
            var qs = new List<string>();
            if (!string.IsNullOrEmpty(company)) qs.Add($"company={company}");
            if (doctorId.HasValue) qs.Add($"doctorId={doctorId.Value}");
            if (!string.IsNullOrEmpty(status)) qs.Add($"status={status}");
            var query = qs.Count > 0 ? "?" + string.Join("&", qs) : "";
            return await dataService.Get<List<DoctorOrderViewDto>>($"api/prickdoctororder/orders{query}");
        }

        public async Task<List<PrickDoctorSummaryRow>> GetSummaryByDoctor(DateTime fromDate, DateTime toDate)
        {
            var url = $"api/prickdoctororder/summary-by-doctor?fromDate={fromDate:yyyy-MM-dd}" +
                      $"&toDate={toDate:yyyy-MM-dd}";
            return await dataService.Get<List<PrickDoctorSummaryRow>>(url) ?? new();
        }

        public async Task<List<DoctorOptionDto>> SearchDoctors(string? search)
        {
            var query = string.IsNullOrEmpty(search) ? "" : $"?search={Uri.EscapeDataString(search)}";
            return await dataService.Get<List<DoctorOptionDto>>($"api/prickdoctororder/doctors{query}");
        }

        public async Task<DoctorOptionDto> QuickAddDoctor(QuickAddDoctorRequest req)
        {
            return await dataService.Post<QuickAddDoctorRequest, DoctorOptionDto>("api/prickdoctororder/doctors/quickadd", req);
        }

        public async Task<DoctorOrderViewDto> CreateOrder(CreateDoctorOrderRequest req)
        {
            return await dataService.Post<CreateDoctorOrderRequest, DoctorOrderViewDto>("api/prickdoctororder/orders", req);
        }

        public async Task<byte[]> DownloadTemplate()
        {
            return await dataService.GetBytes("api/prickdoctororder/import/template");
        }

        public async Task<byte[]> PrintPickingSheet(long orderId)
        {
            return await dataService.GetBytes($"api/prickdoctororder/print/{orderId}");
        }

        public async Task<ImportPreviewResult> ImportPreview(byte[] fileBytes, string fileName)
        {
            return await dataService.PostFile<ImportPreviewResult>("api/prickdoctororder/import/preview", fileBytes, fileName);
        }

        public async Task<int> ImportCommit(CommitImportRequest req)
        {
            return await dataService.Post<CommitImportRequest, int>("api/prickdoctororder/import/commit", req);
        }

        public async Task<ShipResult> ForceComplete(ShipOrderRequest req)
        {
            return await dataService.Post<ShipOrderRequest, ShipResult>("api/prickdoctororder/force-complete", req);
        }

        public async Task<ShipResult> SplitPending(ShipOrderRequest req)
        {
            return await dataService.Post<ShipOrderRequest, ShipResult>("api/prickdoctororder/split-pending", req);
        }

        public async Task<List<ElsewhereAllocationDto>> GetElsewhere(string codePrick, string productTypeCode, long excludeOrderLineId)
        {
            var query = $"?codePrick={Uri.EscapeDataString(codePrick)}&productTypeCode={Uri.EscapeDataString(productTypeCode)}&excludeOrderLineId={excludeOrderLineId}";
            return await dataService.Get<List<ElsewhereAllocationDto>>($"api/prickdoctororder/elsewhere{query}");
        }

        public async Task<StealResult> Steal(StealRequest req)
        {
            return await dataService.Post<StealRequest, StealResult>("api/prickdoctororder/steal", req);
        }

        public async Task<bool> CancelLine(CancelLineRequest req)
        {
            await dataService.Post<CancelLineRequest, object>("api/prickdoctororder/cancel-line", req);
            return true;
        }

        public async Task<bool> UncancelLine(UncancelLineRequest req)
        {
            await dataService.Post<UncancelLineRequest, object>("api/prickdoctororder/uncancel-line", req);
            return true;
        }

        public async Task<bool> ReverseLine(ReverseLineRequest req)
        {
            await dataService.Post<ReverseLineRequest, object>("api/prickdoctororder/reverse-line", req);
            return true;
        }

        public async Task<bool> UpdateNotes(UpdateNotesRequest req)
        {
            await dataService.Post<UpdateNotesRequest, object>("api/prickdoctororder/update-notes", req);
            return true;
        }

        public async Task<bool> UpdateInvoice(UpdateInvoiceRequest req)
        {
            await dataService.Post<UpdateInvoiceRequest, object>("api/prickdoctororder/update-invoice", req);
            return true;
        }

        public async Task<bool> AddLine(AddOrderLineRequest req)
        {
            await dataService.Post<AddOrderLineRequest, object>("api/prickdoctororder/add-line", req);
            return true;
        }

        public async Task<bool> RemoveLine(RemoveOrderLineRequest req)
        {
            await dataService.Post<RemoveOrderLineRequest, object>("api/prickdoctororder/remove-line", req);
            return true;
        }

        public async Task<List<AttachmentDto>> GetAttachments(long orderId)
        {
            return await dataService.Get<List<AttachmentDto>>($"api/prickdoctororder/attachments/{orderId}");
        }

        public async Task<bool> UploadAttachment(long orderId, byte[] fileBytes, string fileName)
        {
            await dataService.PostFile<object>($"api/prickdoctororder/attachments/{orderId}", fileBytes, fileName);
            return true;
        }

        public async Task<bool> DeleteAttachment(long attachmentId)
        {
            await dataService.Post<object, object>($"api/prickdoctororder/attachments/delete/{attachmentId}", new { });
            return true;
        }

        public async Task<List<DeliveryPersonDto>> GetDeliveryPersons()
        {
            return await dataService.Get<List<DeliveryPersonDto>>("api/prickdoctororder/delivery-persons") ?? new();
        }

        public async Task<DeliveryPersonDto?> AddDeliveryPerson(string name)
        {
            return await dataService.Post<AddDeliveryPersonRequest, DeliveryPersonDto>(
                "api/prickdoctororder/delivery-persons", new AddDeliveryPersonRequest { Name = name });
        }

        public async Task<ShipResult> SetShipment(SetShipmentRequest req)
        {
            var result = await dataService.Post<SetShipmentRequest, ShipResult>("api/prickdoctororder/set-shipment", req);
            return result ?? new ShipResult { Success = false, Message = "Κάτι πήγε στραβά, δοκίμασε ξανά." };
        }

        public async Task<List<ShippingCourierDto>> GetCouriers()
        {
            return await dataService.Get<List<ShippingCourierDto>>("api/prickdoctororder/couriers") ?? new();
        }

        public async Task<ShippingCourierDto?> AddCourier(string name)
        {
            return await dataService.Post<AddCourierRequest, ShippingCourierDto>(
                "api/prickdoctororder/couriers", new AddCourierRequest { Name = name });
        }
    }
}
