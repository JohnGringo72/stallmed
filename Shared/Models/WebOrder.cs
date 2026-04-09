using System;
using System.ComponentModel.DataAnnotations;
namespace StallmedManager.Shared.Models
{
    public class WebOrder
    {
        [Key]
        public int ID { get; set; }
        public string? TreatmentID { get; set; }
        public string? CompanyID { get; set; }
        public string? Ref { get; set; }
        public string? Patient { get; set; }
        public string? Doctor { get; set; }
        public string? Pharmacy { get; set; }
        public string? TreatmentDescription { get; set; }
        public string? Allergen { get; set; }
        public string? ExpDate { get; set; }
        public int? QNT { get; set; }
        public string? Status { get; set; }
        public DateTime? Ordered { get; set; }
        public DateTime? TakingDDate { get; set; }
        public DateTime? SendDateClient { get; set; }
        public DateTime? WebLastSyncDate { get; set; }

        public string StatusLabel => Status switch
        {
            "1" => "Recorded",
            "2" => "Manufacturing",
            "3" => "Received",
            "4" => "Send",
            "5" => "Canceled",
            "11" => "To be invoiced",
            _ => Status ?? ""
        };

        public string StatusColor => Status switch
        {
            "1" => "secondary",
            "2" => "warning",
            "3" => "info",
            "4" => "primary",
            "5" => "danger",
            "11" => "success",
            _ => "light"
        };

        public string CompanyLabel => CompanyID switch
        {
            "1" => "SM",
            "2" => "BM",
            _ => CompanyID ?? ""
        };

        public string CompanyColor => CompanyID switch
        {
            "1" => "#28a745",
            "2" => "#b57bee",
            _ => "#6c757d"
        };
    }
}