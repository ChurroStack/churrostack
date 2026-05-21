using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ChurrunKubernetes.Utils
{
    public static class Extensions
    {
        public static string GetSha1HashAsHex(this string text)
        {
            return Convert.ToHexString(GetSha1Hash(text));
        }

        public static byte[] GetSha1Hash(this string text)
        {
            using var sha1 = SHA1.Create();
            return sha1.ComputeHash(Encoding.UTF8.GetBytes(text));
        }

        public static bool TryParseCpuToCores(this string cpu, out double cores)
        {
            cores = 0;

            if (string.IsNullOrWhiteSpace(cpu))
                return false;

            cpu = cpu.Trim();

            if (cpu.EndsWith("m") &&
                double.TryParse(cpu[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var milli))
            {
                cores = milli / 1000.0;
                return true;
            }

            if (double.TryParse(cpu, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                cores = value;
                return true;
            }

            return false;
        }

        public static bool TryParseMemoryToBytes(this string memory, out long bytes)
        {
            bytes = 0;

            if (string.IsNullOrWhiteSpace(memory))
                return false;

            memory = memory.Trim();

            // Split numeric and unit parts
            var unitIndex = memory.Length;
            while (unitIndex > 0 &&
                   !char.IsDigit(memory[unitIndex - 1]) &&
                   memory[unitIndex - 1] != '.')
            {
                unitIndex--;
            }

            if (unitIndex == 0)
                return false;

            var numberPart = memory.Substring(0, unitIndex);
            var unitPart = memory.Substring(unitIndex);

            if (!decimal.TryParse(numberPart, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                return false;

            long multiplier;
            switch (unitPart)
            {
                case "":
                    multiplier = 1L;
                    break;

                // Decimal units
                case "K":
                    multiplier = 1_000L;
                    break;
                case "M":
                    multiplier = 1_000_000L;
                    break;
                case "G":
                    multiplier = 1_000_000_000L;
                    break;
                case "T":
                    multiplier = 1_000_000_000_000L;
                    break;

                // Binary units
                case "Ki":
                    multiplier = 1L << 10;
                    break;
                case "Mi":
                    multiplier = 1L << 20;
                    break;
                case "Gi":
                    multiplier = 1L << 30;
                    break;
                case "Ti":
                    multiplier = 1L << 40;
                    break;

                default:
                    return false;
            }

            bytes = (long)(value * multiplier);
            return true;
        }
    }
}
