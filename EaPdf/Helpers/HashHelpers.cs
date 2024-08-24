using System.Security.Cryptography;

namespace UIUCLibrary.EaPdf.Helpers
{
    public static class HashHelpers
    {
        /// <summary>
        /// Create a HashAlgorithm instance based on the hash algorithm name.
        /// Replaces the deprecated HashAlgorithm.Create(String) method.
        /// </summary>
        /// <param name="hashAlgorithmName"></param>
        /// <returns>HashAlgorithm or null if the hashAlgorithmName is not supported</returns>
        public static HashAlgorithm? CreateHashAlgorithm(string hashAlgorithmName)
        {
            return hashAlgorithmName.ToUpperInvariant() switch
            {
                "MD5" => MD5.Create(),
                "SHA1" => SHA1.Create(),
                "SHA256" => SHA256.Create(),
                "SHA384" => SHA384.Create(),
                "SHA512" => SHA512.Create(),
                _ => null
            };
        }
    }
}
