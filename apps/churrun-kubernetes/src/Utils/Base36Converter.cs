using System.Text;

namespace ChurrunKubernetes.Utils
{
    public static class Base36Converter
    {
        private const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";
        public static string ToBase36(this long value)
        {
            if (value == 0) return "0";

            StringBuilder sb = new StringBuilder();
            long target = value;

            while (target > 0)
            {
                int remainder = (int)(target % 36);
                sb.Insert(0, chars[remainder]);
                target /= 36;
            }

            return sb.ToString();
        }
        public static long FromBase36(this string base36)
        {
            if (string.IsNullOrEmpty(base36)) throw new ArgumentException("Invalid input");

            long result = 0;
            foreach (char c in base36)
            {
                int value;
                if (c >= '0' && c <= '9') value = c - '0';
                else if (c >= 'a' && c <= 'z') value = c - 'a' + 10;
                else throw new ArgumentException("Invalid character in base36 string");

                result = result * 36 + value;
            }

            return result;
        }
    }
}
