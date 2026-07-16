namespace StallmedManager.Shared.Models
{
    // ---- Κατάταξη γιατρών βάσει εμβολίων (WebOrders, BELTA/STALORAL) ----
    public class DoctorSummaryRow
    {
        public string Doctor { get; set; }
        public int TotalOrders { get; set; }
        public int QtySM { get; set; }
        public int QtyBM { get; set; }
        public int QtyTotal { get; set; }
        // Σύνολο ίδιας περιόδου προηγούμενου έτους, για ένδειξη τάσης
        public int PrevQtyTotal { get; set; }
        // Σύνολο πρικ από το άλλο σύστημα (DoctorOrders), best-effort ταύτιση
        // με όνομα -- null αν δεν βρέθηκε αντιστοιχία.
        public int? PrickQtyTotal { get; set; }
    }
}
