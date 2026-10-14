namespace StallmedManager.Shared.Models
{
    // Αρχική σελίδα ανά χρήστη (Users.StartPage, sql/user_start_page.sql).
    // Κενό = Home. Ο χρήστης την ορίζει με το 🏠 σε όποια σελίδα βρίσκεται,
    // ο admin από τη σελίδα Χρήστες.
    public static class StartPages
    {
        // Οι σελίδες του μενού, για τη λίστα του admin και για όμορφο όνομα.
        // Η σειρά εδώ είναι και η σειρά στη λίστα.
        public static readonly (string Path, string Label)[] Known =
        {
            ("/",                "Home"),
            ("/orders",          "Vaccines · Orders"),
            ("/doctors",         "Vaccines · Doctors"),
            ("/stock-search",    "Vaccines · Stock"),
            ("/treatment-stats", "Vaccines · Mixes"),
            ("/apothema",        "Prick · Inventory"),
            ("/prick-orders",    "Prick · Orders"),
            ("/prick-supply",    "Prick · Supply"),
            ("/todo",            "Tasks · Todo"),
            ("/mytasks",         "Tasks · Issues"),
            ("/visits",          "Tasks · Visits"),
            ("/expenses",        "Tasks · Εξοδολόγιο"),
            ("/daily-summary",   "Σύνοψη"),
            ("/users",           "Admin · Χρήστες")
        };

        public static string Label(string? path)
        {
            var p = Normalize(path) ?? "/";
            var known = Known.FirstOrDefault(k => string.Equals(k.Path, p, StringComparison.OrdinalIgnoreCase));
            return known.Label ?? p;
        }

        // Δέχεται μόνο εσωτερική διαδρομή της εφαρμογής (όχι άλλο site, όχι login).
        // Κρατά μόνο τη διαδρομή, χωρίς ?query: αλλιώς π.χ. το /mytasks?issue=12 θα
        // άνοιγε κάθε φορά το ίδιο θέμα. Επιστρέφει null για Home ή για ό,τι δεν είναι έγκυρο.
        public static string? Normalize(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var p = path.Trim().Split('?', '#')[0].TrimEnd('/');
            if (!p.StartsWith("/") || p.StartsWith("//") || p.Contains('\\') || p.Contains(':') || p.Length > 100)
                return null;
            if (p.Equals("/login", StringComparison.OrdinalIgnoreCase))
                return null;
            return p;
        }
    }
}
