using System;
using System.Security.Cryptography;
using System.Text;

namespace Nucleus {
    public static class HashUtils {
        private static readonly SHA256 sha256Hash = SHA256.Create();
        private static readonly MD5 md5Hash = MD5.Create();

        public static byte[] ComputeSha256Hash(byte[] rawData, int offset = 0, int count = 0) {
            if (count == 0) {
                count = rawData.Length - offset;
            }
            return sha256Hash.ComputeHash(rawData, offset, count);
        }

        public static string ComputeSha256HashString(string rawData) {
            byte[] bytes = ComputeSha256Hash(Encoding.UTF8.GetBytes(rawData));
            return ConvertToHexString(bytes);
        }

        public static byte[] ComputeMd5Hash(byte[] rawData) {
            return md5Hash.ComputeHash(rawData);
        }

        public static string ComputeMd5HashString(string rawData) {
            byte[] bytes = ComputeMd5Hash(Encoding.UTF8.GetBytes(rawData));
            return ConvertToHexString(bytes);
        }

        public static string ConvertToHexString(byte[] bytes) {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++) {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString();
        }

        public static byte[] ConvertFromHexString(string hexString) {
            if (hexString.Length % 2 != 0)
                throw new ArgumentException("Invalid hex string length.");

            byte[] bytes = new byte[hexString.Length / 2];
            for (int i = 0; i < bytes.Length; i++) {
                bytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            }
            return bytes;
        }
    }
}
