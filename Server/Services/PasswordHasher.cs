using System.Security.Cryptography;

namespace StallmedManager.Server.Services
{
    // Κωδικοί ως hash, όχι κρυπτογραφημένοι: το hash δεν αντιστρέφεται, οπότε
    // ούτε διαρροή της βάσης ούτε πρόσβαση στο κλειδί αποκαλύπτει κωδικό.
    //
    // Μορφή αποθήκευσης:  PBKDF2$<επαναλήψεις>$<salt base64>$<hash base64>
    // Το πρόθεμα ξεχωρίζει τα νέα hash από τις παλιές AES τιμές, ώστε να
    // μπορούν να συνυπάρχουν όσο οι χρήστες δεν έχουν ξανασυνδεθεί.
    public class PasswordHasher
    {
        public const string Prefix = "PBKDF2$";

        // Κάθε κωδικός παίρνει δικό του τυχαίο salt, ώστε δύο ίδιοι κωδικοί
        // να δίνουν διαφορετικό hash και να μην φαίνεται ποιοι ταιριάζουν.
        private const int SaltBytes = 16;
        private const int HashBytes = 32;
        private const int Iterations = 210_000;

        public static bool IsHashed(string? stored) =>
            stored != null && stored.StartsWith(Prefix, StringComparison.Ordinal);

        public string Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltBytes);
            var hash = Derive(password, salt, Iterations);
            return $"{Prefix}{Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public bool Verify(string password, string? stored)
        {
            if (!IsHashed(stored)) return false;

            // PBKDF2$<iter>$<salt>$<hash>
            var parts = stored!.Split('$');
            if (parts.Length != 4) return false;
            if (!int.TryParse(parts[1], out var iterations) || iterations <= 0) return false;

            byte[] salt, expected;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                expected = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            var actual = Derive(password, salt, iterations, expected.Length);

            // Σταθερού χρόνου σύγκριση: μια απλή ισότητα σταματά στο πρώτο
            // διαφορετικό byte και μαρτυρά πόσο κοντά ήταν η προσπάθεια.
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        private static byte[] Derive(string password, byte[] salt, int iterations, int length = HashBytes)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(length);
        }
    }
}
