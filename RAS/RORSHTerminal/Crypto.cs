using System;
using System.Security.Cryptography;
using System.Text;

namespace RORSHTerminal
{
    /// <summary>
    /// Cryptographic utilities for RORSH Admin Shell
    /// Provides AES-256-GCM encryption, key derivation, and ECDH key exchange
    /// </summary>
    public static class Crypto
    {
        private const int KeySize = 32; // 256 bits
        private const int IvSize = 16;  // 128 bits
        private const int TagSize = 16; // 128 bits
        private const int SaltSize = 32;
        private const int Iterations = 100000;

        /// <summary>
        /// Generate a new ECDH key pair using NIST P-256 curve
        /// </summary>
        public static (string privateKey, string publicKey) GenerateKeyPair()
        {
            using (var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256))
            {
                var privateKey = Convert.ToHexString(ecdh.ExportECPrivateKey());
                var publicKey = Convert.ToHexString(ecdh.ExportSubjectPublicKeyInfo());
                return (privateKey, publicKey);
            }
        }

        /// <summary>
        /// Derive AES-256 key from shared secret using PBKDF2
        /// </summary>
        public static string DeriveKey(string sharedSecret, byte[] salt = null)
        {
            if (salt == null)
            {
                salt = new byte[SaltSize];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(salt);
                }
            }

            using (var pbkdf2 = new Rfc2898DeriveBytes(
                Encoding.UTF8.GetBytes(sharedSecret),
                salt,
                Iterations,
                HashAlgorithmName.SHA256))
            {
                var key = pbkdf2.GetBytes(KeySize);
                return Convert.ToHexString(key);
            }
        }

        /// <summary>
        /// Encrypt plaintext using AES-256-GCM
        /// Format: iv:tag:ciphertext (hex encoded)
        /// </summary>
        public static string Encrypt(string plaintext, string keyHex)
        {
            try
            {
                var key = Convert.FromHexString(keyHex);
                var iv = new byte[IvSize];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(iv);
                }

                var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
                var ciphertext = new byte[plaintextBytes.Length];
                var tag = new byte[TagSize];

                using (var aes = new AesGcm(key, TagSize))
                {
                    aes.Encrypt(iv, plaintextBytes, ciphertext, tag);
                }

                return Convert.ToHexString(iv) + ":" + Convert.ToHexString(tag) + ":" + Convert.ToHexString(ciphertext);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Crypto] Encryption error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Decrypt ciphertext using AES-256-GCM
        /// Input: iv:tag:ciphertext (hex encoded)
        /// </summary>
        public static string Decrypt(string encryptedData, string keyHex)
        {
            try
            {
                var key = Convert.FromHexString(keyHex);
                var parts = encryptedData.Split(':');

                if (parts.Length != 3)
                {
                    throw new Exception("Invalid encrypted data format");
                }

                var iv = Convert.FromHexString(parts[0]);
                var tag = Convert.FromHexString(parts[1]);
                var ciphertext = Convert.FromHexString(parts[2]);
                var plaintext = new byte[ciphertext.Length];

                using (var aes = new AesGcm(key, TagSize))
                {
                    aes.Decrypt(iv, ciphertext, tag, plaintext);
                }

                return Encoding.UTF8.GetString(plaintext);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Crypto] Decryption error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Compute SHA-256 hash
        /// </summary>
        public static string Sha256(string input)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                return Convert.ToHexString(hash);
            }
        }
    }
}
