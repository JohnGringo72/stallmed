using Microsoft.EntityFrameworkCore;
using StallmedManager.Server.Models;
using StallmedManager.Shared.Models;
using System.Globalization;
using System.Text;

namespace StallmedManager.Server.Services
{
    // Rule-based chat bot: αναγνωρίζει keywords στο μήνυμα και απαντά με
    // απευθείας queries στη βάση -- κανένα εξωτερικό AI API.
    //
    // Σημειώσεις σχήματος (δεν υπάρχουν πίνακες Treatments/Stock/Shipments):
    // - "Treatments" = AllergenCodes, "Stock" = StockReceipts (χωρίς expiry/lot
    //   πεδία -- τα "lots" είναι παραλαβές, FIFO κατά ReceivedDate)
    // - "Αποστολές" = DoctorOrders με OrderStatus "ReadyToShip" (όπως η σελίδα
    //   Deliveries) + πρόσφατα σταλμένες (ShippedAt τελευταίες 7 ημέρες)
    public class ChatBotService
    {
        private const int LowStockThreshold = 5;
        private const int AgingMonths = 6;

        private readonly StallmedContext _context;

        public ChatBotService(StallmedContext context)
        {
            _context = context;
        }

        public async Task<ChatBotResponse> ProcessMessage(string message, string companyId)
        {
            var msg = RemoveDiacritics((message ?? "").ToLowerInvariant().Trim());
            var company = ResolveCompany(msg, companyId);

            if (string.IsNullOrWhiteSpace(msg))
                return DefaultResponse();

            // Η σειρά έχει σημασία: το "παραγγελίες γιατρού Χ" πρέπει να πάει
            // στον γιατρό, όχι στις εκκρεμείς -- γι' αυτό ο γιατρός ελέγχεται
            // πριν από το γενικό "παραγγελ".
            if (ContainsAny(msg, "help", "βοηθ", "τι μπορ", "εντολ"))
                return HelpResponse();

            if (ContainsAny(msg, "alert", "χαμηλ", "low", "ληξ", "expir"))
                return await GetStockAlerts(company);

            if (ContainsAny(msg, "γιατρ", "doctor", "dr "))
                return await GetDoctorOrders(ExtractDoctorName(msg), company);

            if (ContainsAny(msg, "αποστολ", "αποστελ", "shipment", "courier"))
                return await GetShipments(company);

            if (ContainsAny(msg, "εκκρεμ", "open", "ανοιχτ", "pending", "παραγγελ"))
                return await GetPendingOrders(company);

            var stockResult = await SearchStock(msg, company);
            if (stockResult != null)
                return stockResult;

            return DefaultResponse();
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static bool ContainsAny(string text, params string[] keywords)
            => keywords.Any(text.Contains);

        // Αφαιρεί τόνους ώστε "βοήθεια" και "βοηθεια" να ταιριάζουν το ίδιο
        private static string RemoveDiacritics(string text)
        {
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        // "SM"/"BM" από το μήνυμα (μεμονωμένη λέξη) ή από το companyId ("1"/"2"/"SM"/"BM")
        private static string ResolveCompany(string msg, string companyId)
        {
            var tokens = msg.Split(new[] { ' ', ',', '.', ';', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Contains("sm")) return "SM";
            if (tokens.Contains("bm")) return "BM";

            return (companyId ?? "").Trim().ToUpperInvariant() switch
            {
                "1" or "SM" => "SM",
                "2" or "BM" => "BM",
                _ => null
            };
        }

        private static string CompanyLabel(string company) => company switch
        {
            "SM" => "SM (StallMedicals)",
            "BM" => "BM (BeltaMed)",
            _ => company
        };

        // Κρατά ό,τι ακολουθεί το "γιατρ*"/"doctor"/"dr" ως όνομα προς αναζήτηση
        private static string ExtractDoctorName(string msg)
        {
            var tokens = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < tokens.Length; i++)
            {
                if (tokens[i].StartsWith("γιατρ") || tokens[i] == "doctor" || tokens[i] == "dr" || tokens[i] == "dr.")
                {
                    var rest = tokens.Skip(i + 1)
                        .Where(t => t != "sm" && t != "bm")
                        .ToArray();
                    return string.Join(" ", rest).Trim();
                }
            }
            return "";
        }

        private async Task<Dictionary<string, string>> GetAllergenDescriptions(IEnumerable<string> codes)
        {
            var list = codes.Distinct().ToList();
            // GroupBy/First αντί για ToDictionaryAsync: αν ποτέ υπάρξουν
            // duplicate CodePrick, το ToDictionary θα πετούσε exception
            return (await _context.AllergenCodes
                .Where(a => list.Contains(a.CodePrick))
                .ToListAsync())
                .GroupBy(a => a.CodePrick)
                .ToDictionary(g => g.Key, g => g.First().DescriptionGreek ?? g.First().Description ?? g.Key);
        }

        // ---------------------------------------------------------------
        // 1. Stock check (αναζήτηση treatment)
        // ---------------------------------------------------------------

        private async Task<ChatBotResponse> SearchStock(string term, string company)
        {
            if (term.Length < 2)
                return null;

            var matches = await _context.AllergenCodes
                .Where(a => a.IsActive &&
                            (EF.Functions.Like(a.CodePrick, $"%{term}%") ||
                             EF.Functions.Like(a.Description, $"%{term}%") ||
                             EF.Functions.Like(a.DescriptionGreek, $"%{term}%")))
                .Where(a => company == null || a.Company == company)
                .Take(6)
                .ToListAsync();

            if (matches.Count == 0)
                return null;

            // Πολλά αποτελέσματα: λίστα με σύνολα για να διευκρινίσει ο χρήστης
            if (matches.Count > 1)
            {
                var codes = matches.Select(m => m.CodePrick).ToList();
                var totals = await _context.StockReceipts
                    .Where(r => !r.IsDepleted && codes.Contains(r.CodePrick))
                    .GroupBy(r => r.CodePrick)
                    .Select(g => new { CodePrick = g.Key, Total = g.Sum(x => x.QuantityRemaining) })
                    .ToDictionaryAsync(g => g.CodePrick, g => g.Total);

                var sb = new StringBuilder();
                sb.AppendLine($"🔎 Βρήκα {matches.Count} treatments για «{term}»:");
                sb.AppendLine();
                foreach (var m in matches)
                {
                    totals.TryGetValue(m.CodePrick, out var total);
                    var icon = total <= 0 ? "❌" : total < LowStockThreshold ? "⚠️" : "✅";
                    sb.AppendLine($"• {m.CodePrick} — {m.DescriptionGreek ?? m.Description}: {total} τμχ {icon}");
                }
                sb.AppendLine();
                sb.AppendLine("💡 Γράψε τον κωδικό για αναλυτικό stock ανά παραλαβή.");
                return new ChatBotResponse { Reply = sb.ToString().TrimEnd(), Type = "stock" };
            }

            // Ένα αποτέλεσμα: αναλυτική εικόνα με παραλαβές FIFO
            var allergen = matches[0];
            var receipts = await _context.StockReceipts
                .Where(r => !r.IsDepleted && r.QuantityRemaining > 0 && r.CodePrick == allergen.CodePrick)
                .OrderBy(r => r.ReceivedDate)
                .ToListAsync();

            var name = allergen.DescriptionGreek ?? allergen.Description ?? allergen.CodePrick;
            var sum = receipts.Sum(r => r.QuantityRemaining);
            var result = new StringBuilder();
            result.AppendLine($"📦 Stock: {name} ({allergen.CodePrick})");
            result.AppendLine();

            if (sum == 0)
            {
                result.AppendLine("Διαθέσιμα: 0 τμχ ❌");
                result.AppendLine();
                result.AppendLine("Δεν υπάρχει διαθέσιμο απόθεμα.");
                return new ChatBotResponse { Reply = result.ToString().TrimEnd(), Type = "stock" };
            }

            var sumIcon = sum < LowStockThreshold ? "⚠️" : "✅";
            result.AppendLine($"Διαθέσιμα: {sum} τμχ {sumIcon}");
            result.AppendLine();
            result.AppendLine("Παραλαβές (FIFO):");
            foreach (var r in receipts.Take(8))
            {
                var age = (DateTime.Now - r.ReceivedDate).Days;
                var ageNote = age > AgingMonths * 30 ? $" ⚠️ ({age / 30} μήνες στο ράφι)" : "";
                result.AppendLine($"• {r.ReceivedDate:dd/MM/yyyy} ({r.ProductTypeCode}) → {r.QuantityRemaining} τμχ{ageNote}");
            }
            if (receipts.Count > 8)
                result.AppendLine($"• ... και {receipts.Count - 8} ακόμα παραλαβές");

            result.AppendLine();
            result.AppendLine($"💡 Χρησιμοποίησε πρώτα την παραλαβή {receipts[0].ReceivedDate:dd/MM/yyyy} (παλαιότερη)");
            return new ChatBotResponse { Reply = result.ToString().TrimEnd(), Type = "stock" };
        }

        // ---------------------------------------------------------------
        // 2. Εκκρεμείς παραγγελίες
        // ---------------------------------------------------------------

        private async Task<ChatBotResponse> GetPendingOrders(string company)
        {
            var rows = await _context.DoctorOrders
                .Where(o => o.OrderStatus == "Open" || o.OrderStatus == "ReadyToShip")
                .Where(o => company == null || o.Company == company)
                .GroupBy(o => new { o.Company, o.OrderStatus })
                .Select(g => new { g.Key.Company, g.Key.OrderStatus, Count = g.Count() })
                .ToListAsync();

            if (rows.Count == 0)
                return new ChatBotResponse
                {
                    Reply = "📋 Δεν υπάρχουν εκκρεμείς παραγγελίες" + (company != null ? $" για {CompanyLabel(company)}" : "") + ". 🎉",
                    Type = "orders"
                };

            var sb = new StringBuilder();
            sb.AppendLine("📋 Εκκρεμείς Παραγγελίες:");
            sb.AppendLine();
            foreach (var grp in rows.GroupBy(r => r.Company).OrderBy(g => g.Key))
            {
                var open = grp.FirstOrDefault(r => r.OrderStatus == "Open")?.Count ?? 0;
                var ready = grp.FirstOrDefault(r => r.OrderStatus == "ReadyToShip")?.Count ?? 0;
                sb.AppendLine($"{CompanyLabel(grp.Key)}: {open} Open, {ready} ReadyToShip");
            }
            sb.AppendLine();
            sb.AppendLine($"Σύνολο: {rows.Sum(r => r.Count)} εκκρεμείς");
            return new ChatBotResponse { Reply = sb.ToString().TrimEnd(), Type = "orders" };
        }

        // ---------------------------------------------------------------
        // 3. Παραγγελίες γιατρού
        // ---------------------------------------------------------------

        private async Task<ChatBotResponse> GetDoctorOrders(string name, string company)
        {
            if (string.IsNullOrWhiteSpace(name))
                return new ChatBotResponse
                {
                    Reply = "👨‍⚕️ Γράψε και το όνομα του γιατρού, π.χ. «γιατρός Papadopoulos».\n\n💡 Τα ονόματα είναι καταχωρημένα με λατινικούς χαρακτήρες.",
                    Type = "doctor"
                };

            var orders = await _context.DoctorOrders
                .Where(o => EF.Functions.Like(o.DoctorName, $"%{name}%") ||
                            (o.Doctor != null && EF.Functions.Like(o.Doctor.FullName, $"%{name}%")))
                .Where(o => company == null || o.Company == company)
                .OrderByDescending(o => o.OrderDate)
                .Take(10)
                .ToListAsync();

            if (orders.Count == 0)
                return new ChatBotResponse
                {
                    Reply = $"Δεν βρέθηκαν αποτελέσματα για: {name}\n\n💡 Τα ονόματα γιατρών είναι με λατινικούς χαρακτήρες (π.χ. Papadopoulos).",
                    Type = "doctor"
                };

            var orderIds = orders.Select(o => o.OrderID).ToList();
            var lines = await _context.DoctorOrderLines
                .Where(l => orderIds.Contains(l.OrderID) && l.LineStatus != "Cancelled")
                .ToListAsync();
            var descriptions = await GetAllergenDescriptions(lines.Select(l => l.CodePrick));

            var doctorLabel = orders[0].Doctor?.FullName ?? orders[0].DoctorName ?? name;
            var sb = new StringBuilder();
            sb.AppendLine($"👨‍⚕️ Παραγγελίες Dr. {doctorLabel}:");
            sb.AppendLine();
            foreach (var o in orders)
            {
                var orderLines = lines.Where(l => l.OrderID == o.OrderID).ToList();
                var items = orderLines.Take(2)
                    .Select(l =>
                    {
                        descriptions.TryGetValue(l.CodePrick, out var d);
                        return $"{d ?? l.CodePrick} x{l.QuantityRequested}";
                    });
                var itemText = string.Join(", ", items);
                if (orderLines.Count > 2)
                    itemText += $" +{orderLines.Count - 2} ακόμα";
                if (string.IsNullOrEmpty(itemText))
                    itemText = "χωρίς γραμμές";

                sb.AppendLine($"• #{o.OrderCode} — {itemText} → {o.OrderStatus} ({o.OrderDate:dd/MM/yyyy})");
            }

            var pending = orders.Count(o => o.OrderStatus == "Open" || o.OrderStatus == "ReadyToShip");
            sb.AppendLine();
            sb.AppendLine($"Σύνολο: {orders.Count} παραγγελίες ({pending} εκκρεμείς)");
            if (orders.Count == 10)
                sb.AppendLine("(εμφανίζονται οι 10 πιο πρόσφατες)");
            return new ChatBotResponse { Reply = sb.ToString().TrimEnd(), Type = "doctor" };
        }

        // ---------------------------------------------------------------
        // 4. Stock alerts (χαμηλό stock + παλιές παραλαβές αντί για expiry)
        // ---------------------------------------------------------------

        private async Task<ChatBotResponse> GetStockAlerts(string company)
        {
            var stock = await _context.StockReceipts
                .Where(r => !r.IsDepleted)
                .GroupBy(r => r.CodePrick)
                .Select(g => new { CodePrick = g.Key, Total = g.Sum(x => x.QuantityRemaining) })
                .ToListAsync();

            var codes = stock.Select(s => s.CodePrick).ToList();
            // GroupBy/First αντί για ToDictionaryAsync (βλ. GetAllergenDescriptions)
            var allergens = (await _context.AllergenCodes
                .Where(a => codes.Contains(a.CodePrick))
                .ToListAsync())
                .GroupBy(a => a.CodePrick)
                .ToDictionary(g => g.Key, g => g.First());

            bool CompanyOk(string code) =>
                company == null ||
                (allergens.TryGetValue(code, out var a) && a.Company == company);

            string Describe(string code) =>
                allergens.TryGetValue(code, out var a) ? (a.DescriptionGreek ?? a.Description ?? code) : code;

            var low = stock
                .Where(s => s.Total > 0 && s.Total < LowStockThreshold && CompanyOk(s.CodePrick))
                .OrderBy(s => s.Total)
                .Take(12)
                .ToList();

            var agingCutoff = DateTime.Now.AddMonths(-AgingMonths);
            var aging = await _context.StockReceipts
                .Where(r => !r.IsDepleted && r.QuantityRemaining > 0 && r.ReceivedDate < agingCutoff)
                .OrderBy(r => r.ReceivedDate)
                .Take(12)
                .ToListAsync();
            aging = aging.Where(r => CompanyOk(r.CodePrick)).ToList();

            if (low.Count == 0 && aging.Count == 0)
                return new ChatBotResponse
                {
                    Reply = "🚨 Stock Alerts:\n\nΌλα καλά! ✅ Κανένα είδος με χαμηλό stock και καμία παλιά παραλαβή.",
                    Type = "alerts"
                };

            var sb = new StringBuilder();
            sb.AppendLine("🚨 Stock Alerts:");
            sb.AppendLine();

            if (low.Count > 0)
            {
                sb.AppendLine("⚠️ Χαμηλό stock (< 5 τμχ):");
                foreach (var s in low)
                    sb.AppendLine($"• {Describe(s.CodePrick)} ({s.CodePrick}) → μόνο {s.Total} τμχ");
                sb.AppendLine();
            }

            if (aging.Count > 0)
            {
                // Δεν υπάρχουν ημερομηνίες λήξης στο σχήμα -- δείχνουμε παλιές
                // παραλαβές (> 6 μήνες στο ράφι) ως ένδειξη παλαίωσης
                sb.AppendLine($"⏰ Παλιές παραλαβές (> {AgingMonths} μήνες στο ράφι):");
                foreach (var r in aging)
                {
                    var months = (int)((DateTime.Now - r.ReceivedDate).Days / 30.0);
                    sb.AppendLine($"• {Describe(r.CodePrick)} ({r.CodePrick}) → παραλαβή {r.ReceivedDate:dd/MM/yyyy}, {r.QuantityRemaining} τμχ ({months} μήνες)");
                }
            }

            return new ChatBotResponse { Reply = sb.ToString().TrimEnd(), Type = "alerts" };
        }

        // ---------------------------------------------------------------
        // 5. Αποστολές
        // ---------------------------------------------------------------

        private async Task<ChatBotResponse> GetShipments(string company)
        {
            var ready = await _context.DoctorOrders
                .Where(o => o.OrderStatus == "ReadyToShip")
                .Where(o => company == null || o.Company == company)
                .OrderBy(o => o.OrderDate)
                .Take(10)
                .ToListAsync();

            var shippedSince = DateTime.Now.AddDays(-7);
            var shipped = await _context.DoctorOrders
                .Where(o => o.ShippedAt != null && o.ShippedAt >= shippedSince)
                .Where(o => company == null || o.Company == company)
                .OrderByDescending(o => o.ShippedAt)
                .Take(10)
                .ToListAsync();

            if (ready.Count == 0 && shipped.Count == 0)
                return new ChatBotResponse
                {
                    Reply = "🚚 Δεν υπάρχουν ενεργές ή πρόσφατες αποστολές" + (company != null ? $" για {CompanyLabel(company)}" : "") + ".",
                    Type = "shipments"
                };

            var sb = new StringBuilder();
            sb.AppendLine("🚚 Αποστολές:");
            sb.AppendLine();

            if (ready.Count > 0)
            {
                sb.AppendLine("📦 Έτοιμες προς αποστολή (ReadyToShip):");
                foreach (var o in ready)
                {
                    var who = o.RecipientName ?? o.DoctorName ?? "—";
                    var city = string.IsNullOrWhiteSpace(o.ShippingCity) ? "" : $", {o.ShippingCity}";
                    sb.AppendLine($"• #{o.OrderCode} → {who}{city} ({o.Company})");
                }
                sb.AppendLine();
            }

            if (shipped.Count > 0)
            {
                sb.AppendLine("✅ Στάλθηκαν τις τελευταίες 7 ημέρες:");
                foreach (var o in shipped)
                {
                    var who = o.RecipientName ?? o.DoctorName ?? "—";
                    var carrier = o.ShippingCarrier ?? o.DeliveryPersonName ?? "courier";
                    var tracking = string.IsNullOrWhiteSpace(o.CourierTrackingCode) ? "" : $", voucher {o.CourierTrackingCode}";
                    sb.AppendLine($"• #{o.OrderCode} → {who}, {o.ShippedAt:dd/MM} ({carrier}{tracking})");
                }
                sb.AppendLine();
            }

            sb.AppendLine($"Σύνολο: {ready.Count} έτοιμες, {shipped.Count} σταλμένες (7 ημέρες)");
            return new ChatBotResponse { Reply = sb.ToString().TrimEnd(), Type = "shipments" };
        }

        // ---------------------------------------------------------------
        // 6 & 7. Help / Default
        // ---------------------------------------------------------------

        private static ChatBotResponse HelpResponse() => new()
        {
            Reply = """
                🤖 Μπορώ να σε βοηθήσω με:

                📦 Stock → γράψε όνομα ή κωδικό treatment (π.χ. «grass pollen»)
                📋 Παραγγελίες → «εκκρεμείς παραγγελίες»
                👨‍⚕️ Γιατρός → «γιατρός Papadopoulos» (λατινικά ονόματα)
                🚨 Alerts → «stock alerts» ή «χαμηλό stock»
                🚚 Αποστολές → «αποστολές»

                💡 Πρόσθεσε «SM» ή «BM» για φιλτράρισμα ανά εταιρεία.
                Ή απλά γράψε ό,τι ψάχνεις!
                """,
            Type = "help"
        };

        private static ChatBotResponse DefaultResponse() => new()
        {
            Reply = """
                🤔 Δεν κατάλαβα. Δοκίμασε:
                • Όνομα ή κωδικό treatment για stock check
                • «εκκρεμείς» για παραγγελίες
                • «alerts» για ειδοποιήσεις
                • «βοήθεια» για όλες τις εντολές
                """,
            Type = "default"
        };
    }
}
