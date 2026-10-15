using System.Globalization;
using System.Text;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;

namespace StallmedManager.Client
{
    // Κοινή ταξινόμηση πινάκων για όλη την εφαρμογή -- μαζί με το <SortTh>.
    //
    // Χρήση σε μια σελίδα:
    //   private TableSort<OrderDto>? ordersSort;
    //   private TableSort<OrderDto> OrdersSort => ordersSort ??= new(this, "orders");
    //   <SortTh Sorter="OrdersSort" Key="date" By="o => o.Date">Ημ/νία</SortTh>
    //   @foreach (var o in OrdersSort.Apply(orders)) { ... }
    //
    // Κλικ: 1ο αύξουσα ▲, 2ο φθίνουσα ▼, 3ο πίσω στην αρχική σειρά.
    // Η επιλογή θυμάται ανά πίνακα (localStorage, κλειδί "sort:{storageKey}").
    // Κενά (null / "") πάντα στο τέλος, όποια φορά κι αν είναι.
    public class TableSort<T>
    {
        private readonly IHandleEvent owner;
        private readonly string storageKey;
        private readonly Dictionary<string, Func<T, object?>> columns = new();
        private Task? loading;
        private ILocalStorageService? storage;

        // owner: η σελίδα -- ξαναζωγραφίζεται όταν αλλάξει η ταξινόμηση.
        // defaultColumn: ταξινόμηση πριν διαλέξει κάτι ο χρήστης (null = η σειρά του server).
        public TableSort(IHandleEvent owner, string storageKey, string? defaultColumn = null, bool defaultDescending = false)
        {
            this.owner = owner;
            this.storageKey = "sort:" + storageKey;
            Column = defaultColumn;
            Descending = defaultDescending;
        }

        public string? Column { get; private set; }
        public bool Descending { get; private set; }

        // Το καλεί κάθε <SortTh>: ποια τιμή ταξινομεί τη στήλη του.
        public void Register(string key, Func<T, object?> selector) => columns[key] = selector;

        public IEnumerable<T> Apply(IEnumerable<T>? source)
        {
            if (source == null) return Enumerable.Empty<T>();
            if (Column == null || !columns.TryGetValue(Column, out var selector)) return source;

            // Κάθε τιμή υπολογίζεται μία φορά (όχι σε κάθε σύγκριση).
            var keyed = source.Select(item => (Item: item, Key: SortKey(selector(item)))).ToList();
            var filled = keyed.Where(x => x.Key != null);
            var ordered = Descending
                ? filled.OrderByDescending(x => x.Key, KeyComparer.Instance)
                : filled.OrderBy(x => x.Key, KeyComparer.Instance);
            return ordered.Concat(keyed.Where(x => x.Key == null)).Select(x => x.Item).ToList();
        }

        public string Indicator(string key) =>
            Column != key ? "" : Descending ? " ▼" : " ▲";

        // Η πρώτη κεφαλίδα που εμφανίζεται φορτώνει την αποθηκευμένη επιλογή (μία φορά).
        public Task EnsureLoadedAsync(ILocalStorageService localStorage)
        {
            storage = localStorage;
            return loading ??= LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                var saved = await storage!.GetItemAsync<SavedSort>(storageKey);
                if (saved?.Column != null)
                {
                    Column = saved.Column;
                    Descending = saved.Descending;
                }
            }
            catch (Exception) { /* χωρίς localStorage: απλώς η προεπιλογή */ }

            // Πάντα: στην πρώτη εμφάνιση οι στήλες δεν είχαν δηλωθεί ακόμα, οπότε
            // ο πίνακας βγήκε αταξινόμητος -- τώρα που δηλώθηκαν, ξαναζωγραφίζεται.
            await Refresh();
        }

        public async Task ToggleAsync(string key)
        {
            if (Column != key) { Column = key; Descending = false; }
            else if (!Descending) Descending = true;
            else { Column = null; Descending = false; }

            try
            {
                if (storage != null)
                {
                    if (Column == null) await storage.RemoveItemAsync(storageKey);
                    else await storage.SetItemAsync(storageKey, new SavedSort { Column = Column, Descending = Descending });
                }
            }
            catch (Exception) { /* δεν πειράζει -- απλώς δεν θα θυμάται */ }

            await Refresh();
        }

        // Ξαναζωγραφίζει τη σελίδα (το κλικ γίνεται στην κεφαλίδα, όχι στη σελίδα).
        private Task Refresh() => EventCallback.Factory.Create(owner, () => { }).InvokeAsync();

        private class SavedSort
        {
            public string? Column { get; set; }
            public bool Descending { get; set; }
        }

        // ---- Σύγκριση ----
        // Κείμενο: χωρίς κεφαλαία/τόνους (Ά = α). Κείμενο που είναι αριθμός (π.χ. κωδικοί)
        // συγκρίνεται ως αριθμός, ώστε 9 < 10. Ημερομηνίες/αριθμοί ως έχουν.
        private static object? SortKey(object? value) => value switch
        {
            null => null,
            string s when string.IsNullOrWhiteSpace(s) => null,
            string s when decimal.TryParse(s.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) => n,
            string s => Fold(s),
            bool b => b ? 1 : 0,
            Enum e => e.ToString(),
            IConvertible c when IsNumber(value) => c.ToDecimal(CultureInfo.InvariantCulture),
            _ => value
        };

        private static bool IsNumber(object value) =>
            value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

        private static string Fold(string s)
        {
            var decomposed = s.Trim().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (var ch in decomposed)
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    sb.Append(char.ToUpperInvariant(ch));
            return sb.ToString();
        }

        private sealed class KeyComparer : IComparer<object?>
        {
            public static readonly KeyComparer Instance = new();

            public int Compare(object? x, object? y)
            {
                if (x is decimal dx && y is decimal dy) return dx.CompareTo(dy);
                // Αριθμοί πριν από κείμενο όταν μια στήλη έχει και τα δύο.
                if (x is decimal) return -1;
                if (y is decimal) return 1;
                if (x is string sx && y is string sy) return string.CompareOrdinal(sx, sy);
                if (x is IComparable cx && x.GetType() == y?.GetType()) return cx.CompareTo(y);
                return string.CompareOrdinal(x?.ToString(), y?.ToString());
            }
        }
    }
}
