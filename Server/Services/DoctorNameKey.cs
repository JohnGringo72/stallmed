using System.Globalization;
using System.Text;

namespace StallmedManager.Server.Services
{
    // Κανονικοποίηση ονόματος γιατρού για best-effort ταύτιση μεταξύ των δύο
    // ασύνδετων συστημάτων: WebOrders.Doctor (ελεύθερο string) και
    // Doctors.FullName. Trim + κεφαλαία + αφαίρεση τόνων/διαλυτικών, γιατί τα
    // ονόματα που είναι ήδη γραμμένα σε κεφαλαία στη ΒΔ συνήθως είναι άτονα.
    public static class DoctorNameKey
    {
        public static string Normalize(string name)
        {
            var formD = name.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(formD.Length);
            foreach (var c in formD)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
