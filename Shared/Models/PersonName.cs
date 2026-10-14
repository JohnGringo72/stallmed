namespace StallmedManager.Shared.Models
{
    // Ονόματα χρηστών στην οθόνη: μικρό όνομα + αρχικό επωνύμου («Ιωάννης Γρηγοριάδης» -> «Ιωάννης Γ.»).
    // ΜΟΝΟ για εμφάνιση: στη βάση μένει το πλήρες όνομα (αναζήτηση, εξαγωγές Excel/PDF).
    // Ολόκληρα φαίνονται μόνο στη σελίδα Χρήστες. ΟΧΙ για γιατρούς/πελάτες.
    public static class PersonName
    {
        public static string Short(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return fullName ?? "";
            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return parts[0];
            return $"{parts[0]} {char.ToUpper(parts[^1][0])}.";
        }
    }
}
