using System.Security.Cryptography;
using System.Text;

namespace StallmedManager.Server.Services
{
    public class AesService
    {
        private readonly byte[] _key;

        public AesService(IConfiguration configuration)
        {
            var keyString = configuration["AesKey"];
            _key = Encoding.UTF8.GetBytes(keyString);
        }

        public string Encrypt(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            // Αποθηκεύουμε IV + encrypted μαζί
            var result = new byte[aes.IV.Length + encryptedBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);

            return Convert.ToBase64String(result);
        }

        public string Decrypt(string cipherText)
        {
            var fullBytes = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key = _key;

            // Παίρνουμε το IV από τα πρώτα 16 bytes
            var iv = new byte[16];
            var encryptedBytes = new byte[fullBytes.Length - 16];
            Buffer.BlockCopy(fullBytes, 0, iv, 0, 16);
            Buffer.BlockCopy(fullBytes, 16, encryptedBytes, 0, encryptedBytes.Length);

            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}