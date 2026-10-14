namespace StallmedManager.Shared.Models
{
    // Οι ομάδες χρηστών. Ένας χρήστης ανήκει το πολύ σε μία· ο admin μπορεί
    // να μην ανήκει πουθενά. Η σειρά εδώ είναι και η σειρά που εμφανίζονται.
    public static class Teams
    {
        public const string Sales = "SALES";
        public const string StallMedicals = "SM";
        public const string BeltaMed = "BM";
        public const string Teco = "TECO";

        public static readonly (string Code, string Label)[] All =
        {
            (Sales,         "Sales"),
            (StallMedicals, "StallMedicals"),
            (BeltaMed,      "BeltaMed"),
            (Teco,          "Teco")
        };

        public static string Label(string? code) =>
            All.FirstOrDefault(t => string.Equals(t.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase)).Label ?? "";

        // Δέχεται μόνο γνωστές ομάδες· οτιδήποτε άλλο γίνεται «καμία».
        public static string? Normalize(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            var match = All.FirstOrDefault(t =>
                string.Equals(t.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
            return match.Code;
        }
    }
}
