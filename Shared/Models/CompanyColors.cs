namespace StallmedManager.Shared.Models
{
    // Ένα χρώμα ανά εταιρεία, ίδιο παντού στην εφαρμογή (ομάδες, ημερολόγιο, χρήστες),
    // ώστε με μια ματιά να φαίνεται τι αφορά τι. Οι κωδικοί είναι ίδιοι με τις ομάδες (Teams).
    public static class CompanyColors
    {
        public const string Stall = "#2e9e5b";    // πράσινο  -- StallMedicals (SM)
        public const string Belta = "#c2189b";    // ματζέντα -- BeltaMed (BM)
        public const string Teco = "#1e6fd9";     // μπλε     -- Teco
        public const string VfPro = "#f08c00";    // πορτοκαλί -- VFpro (μόνο admin)
        public const string Neutral = "#6c757d";  // ό,τι δεν είναι εταιρεία (π.χ. Sales)

        public static string For(string? code) => code?.Trim().ToUpperInvariant() switch
        {
            "SM" => Stall,
            "BM" => Belta,
            "TECO" => Teco,
            "VF" => VfPro,
            _ => Neutral
        };

        // Έτοιμο style για φόντο + άσπρα γράμματα. Το «SM + BM» μοιράζεται στα δύο χρώματα.
        public static string Style(string? code) =>
            string.Equals(code?.Trim(), "SMBM", StringComparison.OrdinalIgnoreCase)
                ? $"background:linear-gradient(90deg,{Stall} 50%,{Belta} 50%);color:#fff"
                : $"background:{For(code)};color:#fff";
    }
}
