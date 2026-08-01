namespace StallmedManager.Shared.Models
{
    public static class QuoteStatus
    {
        public const string Draft = "Draft";
        public const string Sent = "Sent";
        public const string Accepted = "Accepted";
        public const string Rejected = "Rejected";
        public const string Expired = "Expired";
        public const string Converted = "Converted";

        public static string Label(string? status) => status switch
        {
            Draft => "Πρόχειρη",
            Sent => "Απεσταλμένη",
            Accepted => "Αποδεκτή",
            Rejected => "Απορρίφθηκε",
            Expired => "Έληξε",
            Converted => "Μετατράπηκε",
            _ => status ?? ""
        };

        public static string Color(string? status) => status switch
        {
            Draft => "secondary",
            Sent => "primary",
            Accepted => "success",
            Rejected => "danger",
            Expired => "warning",
            Converted => "info",
            _ => "light"
        };
    }

    // Μηχανή καταστάσεων προσφοράς. Κοινή για server (επιβολή) και tests.
    public static class QuoteStateMachine
    {
        private static readonly Dictionary<string, string[]> Allowed = new()
        {
            // Αποδοχή/απόρριψη επιτρέπεται και απευθείας από Draft: η προσφορά
            // μπορεί να έχει σταλεί εκτός συστήματος (τηλέφωνο, χειροκίνητο email).
            [QuoteStatus.Draft] = new[] { QuoteStatus.Sent, QuoteStatus.Accepted, QuoteStatus.Rejected },
            [QuoteStatus.Sent] = new[] { QuoteStatus.Accepted, QuoteStatus.Rejected, QuoteStatus.Expired },
            [QuoteStatus.Expired] = new[] { QuoteStatus.Draft },
            [QuoteStatus.Accepted] = new[] { QuoteStatus.Converted },
            [QuoteStatus.Rejected] = Array.Empty<string>(),
            [QuoteStatus.Converted] = Array.Empty<string>(),
        };

        public static bool CanTransition(string? from, string to)
            => from != null && Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

        // Η επεξεργασία επιτρέπεται μόνο πριν την αποστολή ή μετά από λήξη.
        public static bool CanEdit(string? status)
            => status == QuoteStatus.Draft || status == QuoteStatus.Expired;
    }

    // Υπολογισμοί συνόλων. Τρέχουν ΠΑΝΤΑ server-side πριν την αποθήκευση
    // (δεν εμπιστευόμαστε τιμές από το client) -- το client τους χρησιμοποιεί
    // μόνο για live προεπισκόπηση στη φόρμα.
    public static class QuoteCalculator
    {
        public static void ComputeLine(QuoteLine line)
        {
            var gross = line.Quantity * line.UnitPrice;
            var net = Math.Round(gross * (1m - line.DiscountPct / 100m), 2, MidpointRounding.AwayFromZero);
            var vat = Math.Round(net * line.VatRate / 100m, 2, MidpointRounding.AwayFromZero);
            line.LineNet = net;
            line.LineVat = vat;
            line.LineTotal = net + vat;
        }

        public static void ComputeTotals(Quote quote, IEnumerable<QuoteLine> lines)
        {
            decimal subtotal = 0, vatTotal = 0;
            foreach (var line in lines)
            {
                ComputeLine(line);
                subtotal += line.LineNet;
                vatTotal += line.LineVat;
            }
            quote.Subtotal = subtotal;
            quote.VatTotal = vatTotal;
            quote.Total = subtotal + vatTotal;
        }
    }
}
