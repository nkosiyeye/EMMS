using System.Security.Cryptography;
using System.Text;
using System;

namespace EMMS.Auth
{
    public static class PasswordManager
    {
        private static HashAlgorithmName _hashAlgo = new HashAlgorithmName("SHA256");
        public static string Encrypt(string plainTextPassword)
        {
            byte[] salt = new Byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            var pbkdf2 = new Rfc2898DeriveBytes(plainTextPassword,salt, 10000, _hashAlgo);
            byte[] hash = pbkdf2.GetBytes(20);

            byte[] hashBytes = new Byte[36];
            Array.Copy(salt, 0, hashBytes, 0, salt.Length);
            Array.Copy(hash, 0, hashBytes, salt.Length, hash.Length);

            return Convert.ToBase64String(hashBytes);
        }

        public static bool VerifyPassword(string plainTextPassword, string encryptedPassword)
        {
            byte[] hashBytes = Convert.FromBase64String(encryptedPassword);

            byte[] salt = new Byte[16];
            Array.Copy(hashBytes, 0, salt, 0, 16);

            var pbkdf2 = new Rfc2898DeriveBytes(plainTextPassword, salt, 10000, _hashAlgo);
            byte[] hash = pbkdf2.GetBytes(20);

            for (int i = 0; i < 20; i++)
            {
                if (hashBytes[i + 16] != hash[i])
                    return false;
            }

            return true;
        }
    }
}
