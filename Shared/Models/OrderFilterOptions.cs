namespace StallmedManager.Shared.Models
{
    public class OrderFilterOptions
    {
        public List<string> Doctors { get; set; } = new();
        public List<string> Patients { get; set; } = new();
        public List<string> Pharmacies { get; set; } = new();
        public List<string> Statuses { get; set; } = new();
        public List<string> Companies { get; set; } = new();
        public List<string> Treatments { get; set; } = new();
    }
}