namespace StallmedManager.Shared.Models
{
    public class ChatBotRequest
    {
        public string Message { get; set; }
        // Προαιρετικό: "SM"/"BM" (ή "1"/"2" κατά WebOrders) -- αν λείπει, ο bot
        // ανιχνεύει εταιρεία μέσα στο μήνυμα ή δείχνει και τις δύο.
        // ΠΡΟΣΟΧΗ: πρέπει να είναι nullable (string?) -- με <Nullable>enable</Nullable>
        // το ASP.NET Core κάνει τα non-nullable properties υποχρεωτικά στο model
        // validation και γυρνάει αυτόματο 400 όταν ο client δεν το στέλνει.
        public string? CompanyId { get; set; }
    }
}
