# Λειτουργία Προσφορών (Quotes Module)

Προσφορές προς νοσοκομεία/φορείς που **δεν δεσμεύουν απόθεμα** και δεν τιμολογούνται.
Όταν γίνουν αποδεκτές, μετατρέπονται με ένα κουμπί σε κανονική παραγγελία Prick
(DoctorOrders) και από εκεί ισχύει η υπάρχουσα ροή (δέσμευση αποθέματος, αποστολή κ.λπ.).

## Πρώτη εγκατάσταση

### 1. Βάση δεδομένων

Τρέξε **μία φορά** το script [sql/quotes_module.sql](sql/quotes_module.sql) στη βάση
`OnlineData`. Κάνει δύο πράγματα:

1. **ALTER στον πίνακα `Doctors`**: προσθέτει `VatNumber`, `Department`,
   `ContactPerson`, `PostalCode` — οι πελάτες/νοσοκομεία των προσφορών είναι
   εγγραφές αυτού του πίνακα (τα ονόματα υπάρχουν ήδη, τα υπόλοιπα στοιχεία
   συμπληρώνονται από τη φόρμα προσφοράς με το κουμπί ✎).
2. Δημιουργεί 4 νέους πίνακες: `Quotes`, `QuoteLines`, `QuoteEvents`, `QuoteAttachments`.

> ⚠️ **Το ALTER πρέπει να τρέξει ΠΡΙΝ γίνει deploy αυτής της έκδοσης** — το EF
> μοντέλο του Doctor περιλαμβάνει πλέον τις νέες στήλες, οπότε χωρίς αυτές
> ΟΛΕΣ οι σελίδες που διαβάζουν γιατρούς θα σκάνε με SQL error.

### 2. Ρυθμίσεις (Server/appsettings.json)

**SMTP** — απαραίτητο για την αποστολή email (χωρίς αυτό η «Αποστολή» επιστρέφει
καθαρό μήνυμα σφάλματος, όλα τα υπόλοιπα δουλεύουν). **Κάθε εταιρεία έχει δικό
της λογαριασμό αποστολής** (`Smtp:SM` / `Smtp:BM`)· ό,τι πεδίο λείπει από την
εταιρεία διαβάζεται από το κοινό επίπεδο `Smtp` (π.χ. κοινό Host/Port). Αν οι
δύο λογαριασμοί είναι σε διαφορετικό πάροχο, βάλε `Host`/`Port` και μέσα στο
`SM`/`BM` — υπερισχύουν:

```json
"Smtp": {
  "Host": "smtp.example.gr",
  "Port": "587",
  "UseSsl": "true",
  "SM": {
    "Username": "offers@stallergenes.gr",
    "Password": "********",
    "FromAddress": "offers@stallergenes.gr",
    "FromName": "Stallergenes Medicals"
  },
  "BM": {
    "Username": "offers@belta.gr",
    "Password": "********",
    "FromAddress": "offers@belta.gr",
    "FromName": "Belta Medicals"
  }
}
```

**Φάκελος αρχειοθέτησης** — τα PDF αποθηκεύονται πάντα στη βάση (`Quotes.PdfData`).
Αν οριστεί `ArchiveBasePath`, γράφεται και αντίγραφο στον δίσκο του server:

```
{ArchiveBasePath}\Προσφορές\{ΕΤΟΣ}\{Πελάτης}\{Αριθμός}.pdf
{ArchiveBasePath}\Προσφορές\{ΕΤΟΣ}\{Πελάτης}\{Αριθμός}_email.txt   (αντίγραφο email)
```

```json
"Quotes": {
  "ArchiveBasePath": "D:\\StallmedArchive",
  "ValidityDays": "30",
  "DefaultVatRate": "6"
}
```

**Στοιχεία εταιρειών** — για το επιστολόχαρτο του PDF, ανά εταιρεία (SM/BM):
`Companies:SM` και `Companies:BM` με πεδία `Name`, `LegalName`, `VatNumber`, `Gemi`,
`Address`, `City`, `PostalCode`, `Phone`, `Email`, `LogoPath` (PNG/JPG στον δίσκο του
server· αν λείπει, τυπώνεται το όνομα ως κείμενο), `AccentColor`.

## Λειτουργία

- **Αρίθμηση:** `ΠΡ-{SM|BM}-{ΕΤΟΣ}-{NNNN}`, ξεχωριστή σειρά ανά εταιρεία, μηδενισμός ανά έτος.
- **Καταστάσεις:** Draft → Sent → Accepted/Rejected/Expired, Expired → Draft (επανέκδοση),
  Accepted → Converted. Κάθε άλλη μετάβαση απορρίπτεται από τον server.
  Οι Sent προσφορές που περνά η ισχύς τους γίνονται Expired αυτόματα (στο επόμενο άνοιγμα της λίστας).
- **Επεξεργασία:** μόνο σε Draft/Expired. Τα σύνολα υπολογίζονται πάντα server-side.
- **Μετατροπή:** δημιουργεί DoctorOrder (status Open) με τις ίδιες γραμμές μέσα σε
  transaction, με αμφίδρομη σύνδεση (Quote.ConvertedOrderID ↔ σημείωση στην παραγγελία).
  Οι τιμές μένουν στην προσφορά (οι DoctorOrderLines δεν έχουν πεδία τιμών).
- **Πελάτες:** εγγραφές του πίνακα `Doctors` (τα ονόματα των νοσοκομείων είναι ήδη
  περασμένα εκεί). Η προσφορά κρατά snapshot των στοιχείων τη στιγμή της έκδοσης,
  και η παραγγελία που προκύπτει από μετατροπή παίρνει τον ίδιο `DoctorID`.
- **Excel import:** μέσα στη φόρμα προσφοράς — «Πρότυπο» κατεβάζει xlsx με στήλες
  (Κωδικός, Τύπος, Περιγραφή, Ποσότητα, Τιμή, Έκπτωση %, ΦΠΑ %), το ανέβασμα
  γεμίζει τις γραμμές. Άκυρες γραμμές απορρίπτονται με προειδοποίηση· κενή τιμή
  σημαίνει προσυμπλήρωση από τον τύπο προϊόντος, κενό ΦΠΑ = το default (6%).
- **Συνημμένα:** στο modal προβολής — οποιοδήποτε αρχείο (έως 10MB), αποθηκεύεται
  στη βάση (`QuoteAttachments`), με λήψη/διαγραφή και ένδειξη 📎 στη λίστα.
- **Ιστορικό:** κάθε ενέργεια (δημιουργία, PDF, email, αλλαγή κατάστασης, μετατροπή)
  γράφεται στον πίνακα `QuoteEvents`.
- **Πρόσβαση:** το UI (menu PRICK → Προσφορές) και το API εξαιρούν τον ρόλο `warehouse`
  (policy `NotWarehouse`).

## Tests

```
dotnet test Tests/StallmedManager.Tests.csproj
```

Καλύπτουν τη μηχανή καταστάσεων (επιτρεπτές/μη μεταβάσεις), τους υπολογισμούς
(έκπτωση/ΦΠΑ/σύνολα) και τη μετατροπή (σωστές γραμμές, σύνδεση, καμία επαφή με απόθεμα).

## Αρχεία

| Τι | Πού |
| --- | --- |
| Entities | `Shared/Models/QuoteModels.cs` |
| DTOs | `Shared/Models/QuoteDtos.cs` |
| Μηχανή καταστάσεων + υπολογισμοί | `Shared/Models/QuoteLogic.cs` |
| API | `Server/Controllers/QuotesController.cs` (`api/quotes/...`) |
| PDF (QuestPDF) | `Server/Services/QuotePdfService.cs` |
| Email (MailKit) | `Server/Services/QuoteEmailService.cs` |
| UI | `Client/Pages/Quotes.razor` + `Client/QuotesService.cs` |
| Schema | `sql/quotes_module.sql` |
| Tests | `Tests/` |
