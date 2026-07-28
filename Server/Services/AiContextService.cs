// ============================================================
// ΑΝΕΝΕΡΓΟΣ ΚΩΔΙΚΑΣ (23/07/2026): δεν αναφέρεται πουθενά στο project.
// Σχολιάστηκε αντί να διαγραφεί -- αφαίρεσε τα // αν ξαναχρειαστεί.
// ============================================================
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Caching.Memory;
// using StallmedManager.Server.Models;
// using System.Text;
// 
// namespace StallmedManager.Server.Services
// {
//     // Συγκεντρώνει σύντομα aggregates από τη βάση (ΟΧΙ raw data) και τα
//     // μορφοποιεί ως κείμενο-context για το system prompt του AI βοηθού.
//     // Τα αποτελέσματα κρατιούνται σε MemoryCache για 5 λεπτά ώστε η βάση
//     // να μη χτυπιέται σε κάθε μήνυμα του chat.
//     public class AiContextService
//     {
//         private const string CacheKey = "AiChatContext";
//         private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
// 
//         private readonly StallmedContext _context;
//         private readonly IMemoryCache _cache;
// 
//         public AiContextService(StallmedContext context, IMemoryCache cache)
//         {
//             _context = context;
//             _cache = cache;
//         }
// 
//         public async Task<string> GetContextAsync()
//         {
//             if (_cache.TryGetValue(CacheKey, out string cached))
//                 return cached;
// 
//             var text = await BuildContextAsync();
//             _cache.Set(CacheKey, text, CacheDuration);
//             return text;
//         }
// 
//         private async Task<string> BuildContextAsync()
//         {
//             var sb = new StringBuilder();
// 
//             // ---- Prick Test: παραγγελίες γιατρών ανά εταιρεία & status ----
//             var doctorOrders = await _context.DoctorOrders
//                 .Where(o => o.OrderStatus != "Cancelled")
//                 .GroupBy(o => new { o.Company, o.OrderStatus })
//                 .Select(g => new { g.Key.Company, g.Key.OrderStatus, Count = g.Count() })
//                 .ToListAsync();
// 
//             sb.AppendLine("Παραγγελίες Prick Test (DoctorOrders) ανά εταιρεία και status:");
//             if (doctorOrders.Count == 0)
//                 sb.AppendLine("- Καμία ενεργή παραγγελία");
//             foreach (var row in doctorOrders.OrderBy(r => r.Company).ThenBy(r => r.OrderStatus))
//                 sb.AppendLine($"- {row.Company} / {row.OrderStatus}: {row.Count}");
// 
//             // ---- Εκκρεμείς αποστολές (έτοιμες προς αποστολή) ----
//             var readyToShip = doctorOrders.Where(r => r.OrderStatus == "ReadyToShip").Sum(r => r.Count);
//             sb.AppendLine($"Εκκρεμείς αποστολές Prick (ReadyToShip): {readyToShip}");
//             sb.AppendLine();
// 
//             // ---- WebOrders (εμβόλια αλλεργίας) ανά εταιρεία & status ----
//             // Status: 1=Recorded, 2=Manufacturing, 3=Received, 4=Send, 5=Canceled, 11=To be invoiced
//             var webOrders = await _context.WebOrders
//                 .Where(o => o.Status == "1" || o.Status == "2" || o.Status == "3" || o.Status == "4")
//                 .GroupBy(o => new { o.CompanyID, o.Status })
//                 .Select(g => new { g.Key.CompanyID, g.Key.Status, Count = g.Count() })
//                 .ToListAsync();
// 
//             sb.AppendLine("Ενεργές παραγγελίες εμβολίων (WebOrders) ανά εταιρεία και status:");
//             if (webOrders.Count == 0)
//                 sb.AppendLine("- Καμία ενεργή παραγγελία");
//             foreach (var row in webOrders.OrderBy(r => r.CompanyID).ThenBy(r => r.Status))
//             {
//                 var company = row.CompanyID == "1" ? "SM" : row.CompanyID == "2" ? "BM" : row.CompanyID;
//                 var label = row.Status switch
//                 {
//                     "1" => "Recorded",
//                     "2" => "Manufacturing",
//                     "3" => "Received",
//                     "4" => "Send",
//                     _ => row.Status
//                 };
//                 sb.AppendLine($"- {company} / {label}: {row.Count}");
//             }
//             sb.AppendLine();
// 
//             // ---- Stock: σύνολο ειδών και χαμηλά αποθέματα (< 5 τεμάχια) ----
//             var stock = await _context.StockReceipts
//                 .Where(r => !r.IsDepleted)
//                 .GroupBy(r => new { r.CodePrick, r.ProductTypeCode })
//                 .Select(g => new { g.Key.CodePrick, g.Key.ProductTypeCode, Total = g.Sum(x => x.QuantityRemaining) })
//                 .ToListAsync();
// 
//             var withStock = stock.Where(s => s.Total > 0).ToList();
//             var lowStock = withStock.Where(s => s.Total < 5).ToList();
// 
//             sb.AppendLine($"Αποθέματα Prick: {withStock.Count} είδη (κωδικός+τύπος) με διαθέσιμο stock.");
//             if (lowStock.Count > 0)
//             {
//                 // Περιγραφές μόνο για τους λίγους κωδικούς με χαμηλό stock
//                 var lowCodes = lowStock.Select(s => s.CodePrick).Distinct().ToList();
//                 var descriptions = await _context.AllergenCodes
//                     .Where(a => lowCodes.Contains(a.CodePrick))
//                     .ToDictionaryAsync(a => a.CodePrick, a => a.DescriptionGreek ?? a.Description ?? a.CodePrick);
// 
//                 sb.AppendLine($"ΧΑΜΗΛΟ STOCK (< 5 τεμάχια) σε {lowStock.Count} είδη:");
//                 foreach (var s in lowStock.OrderBy(s => s.Total).Take(15))
//                 {
//                     descriptions.TryGetValue(s.CodePrick, out var desc);
//                     sb.AppendLine($"- {s.CodePrick} ({desc ?? "?"}) / {s.ProductTypeCode}: {s.Total} τεμ.");
//                 }
//             }
//             else
//             {
//                 sb.AppendLine("Δεν υπάρχουν είδη με χαμηλό stock (< 5 τεμάχια).");
//             }
//             sb.AppendLine();
// 
//             // ---- Ενεργοί γιατροί (Prick) ----
//             var activeDoctors = await _context.Doctors.CountAsync(d => d.IsActive);
//             sb.AppendLine($"Ενεργοί γιατροί (Prick): {activeDoctors}");
//             sb.AppendLine();
// 
//             // ---- Top 5 αλλεργιογόνα σε παραγγελίες τελευταίων 30 ημερών ----
//             var since = DateTime.Now.AddDays(-30);
//             var topTreatments = await _context.DoctorOrderLines
//                 .Where(l => l.Order.OrderDate >= since && l.LineStatus != "Cancelled")
//                 .GroupBy(l => l.CodePrick)
//                 .Select(g => new { CodePrick = g.Key, Qty = g.Sum(x => x.QuantityRequested) })
//                 .OrderByDescending(g => g.Qty)
//                 .Take(5)
//                 .ToListAsync();
// 
//             sb.AppendLine("Top 5 αλλεργιογόνα σε παραγγελίες Prick τελευταίων 30 ημερών:");
//             if (topTreatments.Count == 0)
//                 sb.AppendLine("- Καμία παραγγελία τον τελευταίο μήνα");
//             else
//             {
//                 var topCodes = topTreatments.Select(t => t.CodePrick).ToList();
//                 var topDescriptions = await _context.AllergenCodes
//                     .Where(a => topCodes.Contains(a.CodePrick))
//                     .ToDictionaryAsync(a => a.CodePrick, a => a.DescriptionGreek ?? a.Description ?? a.CodePrick);
// 
//                 foreach (var t in topTreatments)
//                 {
//                     topDescriptions.TryGetValue(t.CodePrick, out var desc);
//                     sb.AppendLine($"- {t.CodePrick} ({desc ?? "?"}): {t.Qty} τεμ.");
//                 }
//             }
// 
//             sb.AppendLine();
//             sb.AppendLine($"(Στοιχεία υπολογισμένα: {DateTime.Now:dd/MM/yyyy HH:mm}, ανανεώνονται κάθε 5 λεπτά)");
// 
//             return sb.ToString();
//         }
//     }
// }
