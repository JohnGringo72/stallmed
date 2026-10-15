using System.Globalization;
using System.Text;
using StallmedManager.Shared.Models;

namespace StallmedManager.Client
{
    // Ένα χρώμα ανά άτομο, ίδιο παντού (Tasks, Visits, ημερολόγιο πωλητών...), για να
    // ξεχωρίζουν τα ονόματα με μια ματιά. Το χρησιμοποιεί το <PersonBadge>.
    // Η παλέτα ΔΕΝ έχει τα χρώματα των εταιρειών (CompanyColors), για να μη μπερδεύονται.
    // Χρώμα με βάση τη σειρά του κωδικού χρήστη: διαφορετικό για καθέναν μέχρι να
    // ξεπεράσουμε τα χρώματα της παλέτας. Ονόματα που δεν βρίσκονται (π.χ. ανενεργοί
    // χρήστες σε παλιές εγγραφές) παίρνουν χρώμα από το ίδιο το όνομα -- πάντα το ίδιο.
    public class PersonColors
    {
        private static readonly string[] Palette =
        {
            "#6f42c1", "#0aa2c0", "#8d6e63", "#e03131", "#b8860b", "#20a39e",
            "#495057", "#5f3dc4", "#a9480f", "#0b7285", "#868e96", "#364fc7"
        };

        private readonly DataService dataService;
        private Task? loading;
        private Dictionary<int, int> indexById = new();
        private Dictionary<string, int> idByName = new();

        public PersonColors(DataService dataService)
        {
            this.dataService = dataService;
        }

        public Task EnsureLoadedAsync() => loading ??= LoadAsync();

        private async Task LoadAsync()
        {
            try
            {
                var users = await new IssuesService(dataService).GetUsers();
                if (users == null || users.Count == 0)
                {
                    loading = null;   // π.χ. πριν το login -- ξαναδοκιμάζει αργότερα
                    return;
                }
                var ordered = users.OrderBy(u => u.IdUser).ToList();
                indexById = ordered.Select((u, i) => (u.IdUser, i)).ToDictionary(x => x.IdUser, x => x.i);
                idByName = ordered
                    .GroupBy(u => Fold(u.FullName))
                    .ToDictionary(g => g.Key, g => g.First().IdUser);
            }
            catch (Exception)
            {
                loading = null;   // ξαναδοκιμάζει την επόμενη φορά
            }
        }

        // userId αν υπάρχει, αλλιώς το όνομα (πλήρες ή όπως είναι αποθηκευμένο).
        public string For(int? userId, string? name)
        {
            if (userId is int id && indexById.TryGetValue(id, out var index))
                return Palette[index % Palette.Length];
            if (!string.IsNullOrWhiteSpace(name) && idByName.TryGetValue(Fold(name), out var byName)
                && indexById.TryGetValue(byName, out var nameIndex))
                return Palette[nameIndex % Palette.Length];
            return string.IsNullOrWhiteSpace(name) ? Palette[^1] : Palette[StableHash(Fold(name)) % Palette.Length];
        }

        private static int StableHash(string s)
        {
            unchecked
            {
                var h = 17;
                foreach (var ch in s) h = h * 31 + ch;
                return h & 0x7fffffff;
            }
        }

        // Χωρίς τόνους/κεφαλαία/διπλά κενά: «Γιώτα  ΜΑΝΤΖΩΡΟΥ» = «γιωτα μαντζωρου».
        private static string Fold(string s)
        {
            var decomposed = string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (var ch in decomposed)
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    sb.Append(char.ToUpperInvariant(ch));
            return sb.ToString();
        }
    }
}
