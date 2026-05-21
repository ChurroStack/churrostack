using System.Security.Cryptography;
using System.Text;

namespace ChurrOS.Api.Services.Security
{
    public static class AesGcmEncryption
    {
        private const int TagSize = 16; // 128-bit authentication tag

        public static byte[] GenerateKey(int keySizeInBits = 32)
        {
            return RandomNumberGenerator.GetBytes(keySizeInBits);
        }

        public static string GenerateBase64Key(int keySizeInBits = 32)
        {
            byte[] key = GenerateKey(keySizeInBits);
            return Convert.ToBase64String(key);
        }

        public static string GenerateBase64Iv(int keySizeInBits = 12)
        {
            byte[] key = RandomNumberGenerator.GetBytes(keySizeInBits);
            return Convert.ToBase64String(key);
        }

        public static string Encrypt(
            string plaintext,
            string base64Key,
            string base64Iv)
        {
            if (plaintext is null)
                throw new ArgumentNullException(nameof(plaintext));

            byte[] key = Convert.FromBase64String(base64Key);
            byte[] iv = Convert.FromBase64String(base64Iv);
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

            byte[] ciphertext = new byte[plaintextBytes.Length];
            byte[] tag = new byte[TagSize];

            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(
                nonce: iv,
                plaintext: plaintextBytes,
                ciphertext: ciphertext,
                tag: tag);

            // Combine ciphertext + tag
            byte[] combined = new byte[ciphertext.Length + tag.Length];
            Buffer.BlockCopy(ciphertext, 0, combined, 0, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, combined, ciphertext.Length, tag.Length);

            return Convert.ToBase64String(combined);
        }


        public static byte[] Encrypt(
            byte[] plaintextBytes,
            string base64Key,
            string base64Iv)
        {
            if (plaintextBytes is null)
                throw new ArgumentNullException(nameof(plaintextBytes));

            byte[] key = Convert.FromBase64String(base64Key);
            byte[] iv = Convert.FromBase64String(base64Iv);

            byte[] ciphertext = new byte[plaintextBytes.Length];
            byte[] tag = new byte[TagSize];

            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(
                nonce: iv,
                plaintext: plaintextBytes,
                ciphertext: ciphertext,
                tag: tag);

            // Combine ciphertext + tag
            byte[] combined = new byte[ciphertext.Length + tag.Length];
            Buffer.BlockCopy(ciphertext, 0, combined, 0, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, combined, ciphertext.Length, tag.Length);

            return combined;
        }

        public static string Decrypt(
            string base64Ciphertext,
            string base64Key,
            string base64Iv)
        {
            if (base64Ciphertext is null)
                throw new ArgumentNullException(nameof(base64Ciphertext));

            byte[] key = Convert.FromBase64String(base64Key);
            byte[] iv = Convert.FromBase64String(base64Iv);
            byte[] combined = Convert.FromBase64String(base64Ciphertext);

            if (combined.Length < TagSize)
                throw new CryptographicException("Invalid ciphertext.");

            int ciphertextLength = combined.Length - TagSize;

            byte[] ciphertext = new byte[ciphertextLength];
            byte[] tag = new byte[TagSize];

            Buffer.BlockCopy(combined, 0, ciphertext, 0, ciphertextLength);
            Buffer.BlockCopy(combined, ciphertextLength, tag, 0, TagSize);

            byte[] plaintext = new byte[ciphertextLength];

            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(
                nonce: iv,
                ciphertext: ciphertext,
                tag: tag,
                plaintext: plaintext);

            return Encoding.UTF8.GetString(plaintext);
        }


        public static byte[] Decrypt(
            byte[] encryptedBytes,
            string base64Key,
            string base64Iv)
        {
            if (encryptedBytes is null)
                throw new ArgumentNullException(nameof(encryptedBytes));

            byte[] key = Convert.FromBase64String(base64Key);
            byte[] iv = Convert.FromBase64String(base64Iv);

            if (encryptedBytes.Length < TagSize)
                throw new CryptographicException("Invalid ciphertext.");

            int ciphertextLength = encryptedBytes.Length - TagSize;

            byte[] ciphertext = new byte[ciphertextLength];
            byte[] tag = new byte[TagSize];

            Buffer.BlockCopy(encryptedBytes, 0, ciphertext, 0, ciphertextLength);
            Buffer.BlockCopy(encryptedBytes, ciphertextLength, tag, 0, TagSize);

            byte[] plaintext = new byte[ciphertextLength];

            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(
                nonce: iv,
                ciphertext: ciphertext,
                tag: tag,
                plaintext: plaintext);

            return plaintext;
        }
    }
}
