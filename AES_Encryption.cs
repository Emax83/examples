using System;
using System.Security.Cryptography;
using System.Text;

namespace AesEncryption
{
    public static class AesHelper
    {
        public static string Encrypt(string plainText, string password)
        {
            // Generate a random salt
            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            // Derive the key and IV from the password and salt
            using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, 1000))
            {
                byte[] key = deriveBytes.GetBytes(32);
                byte[] iv = deriveBytes.GetBytes(16);

                // Encrypt the plaintext
                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;

                    using (ICryptoTransform encryptor = aes.CreateEncryptor())
                    {
                        byte[] cipherText = encryptor.TransformFinalBlock(Encoding.UTF8.GetBytes(plainText), 0, plainText.Length);

                        // Concatenate the salt and ciphertext and return the result as a base64 string
                        byte[] encryptedBytes = new byte[salt.Length + cipherText.Length];
                        Buffer.BlockCopy(salt, 0, encryptedBytes, 0, salt.Length);
                        Buffer.BlockCopy(cipherText, 0, encryptedBytes, salt.Length, cipherText.Length);
                        return Convert.ToBase64String(encryptedBytes);
                    }
                }
            }
        }

        public static string Decrypt(string encryptedText, string password)
        {
            // Parse the encrypted text to extract the salt and ciphertext
            byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
            byte[] salt = new byte[16];
            byte[] cipherText = new byte[encryptedBytes.Length - salt.Length];
            Buffer.BlockCopy(encryptedBytes, 0, salt, 0, salt.Length);
            Buffer.BlockCopy(encryptedBytes, salt.Length, cipherText, 0, cipherText.Length);

            // Derive the key and IV from the password and salt
            using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, 1000))
            {
                byte[] key = deriveBytes.GetBytes(32);
                byte[] iv = deriveBytes.GetBytes(16);

                // Decrypt the ciphertext
                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;

                    using (ICryptoTransform decryptor = aes.CreateDecryptor())
                    {
                        byte[] plainTextBytes = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
                        return Encoding.UTF8.GetString(plainTextBytes);
                    }
                }
            }
        }
    }
}
