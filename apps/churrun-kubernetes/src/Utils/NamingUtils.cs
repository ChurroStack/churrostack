using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace ChurrunKubernetes.Utils
{
    public static class NamingUtils
    {
        private static Regex _namingRegex = new Regex(@"^[a-z0-9-]*$", RegexOptions.Compiled);
        private static Regex _namingTemplateRegex = new Regex(@"^[a-z0-9-._]*$", RegexOptions.Compiled);
        private static Regex _namingRegexIgnoreCase = new Regex(_namingRegex.ToString(), RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool IsValidName(string name, bool isSystem = false, bool ignoreCase = false)
        {
            if (!isSystem && name.StartsWith("_") || name.Contains("__"))
            {
                return false;
            }
            return ignoreCase ? _namingRegexIgnoreCase.IsMatch(name) : _namingRegex.IsMatch(name);
        }

        public static bool IsValidTemplateName(string name)
        {
            if (name.StartsWith("_") || name.Contains("__"))
            {
                return false;
            }
            return _namingTemplateRegex.IsMatch(name);
        }

        private static Regex _invalidChars = new Regex(@"[<>:""/\\|?*]");
        public static bool IsValidFilesystemName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == "." || name == ".." || _invalidChars.IsMatch(name ?? ""))
                return false;
            return true;
        }

        public static string? EncodeFieldName(this string toEncode)
        {
            if (toEncode != null)
            {
                var encodedString = new StringBuilder();

                foreach (char chr in toEncode.ToCharArray())
                {
                    string encodedChar = HttpUtility.UrlEncode(chr.ToString());

                    if (encodedChar.StartsWith("%"))
                    {
                        encodedChar = encodedChar.Replace("u", "x");
                        encodedChar = encodedChar.Substring(1, encodedChar.Length - 1);
                        encodedChar = string.Format("_{0}_", encodedChar);
                        encodedString.Append(encodedChar);
                    }
                    else if (encodedChar == "+" || encodedChar == " ")
                    {
                        encodedString.Append("_x0020_");
                    }
                    else if (encodedChar == ".")
                    {
                        encodedString.Append("_x002e_");
                    }
                    else
                    {
                        encodedString.Append(chr);
                    }

                }
                return encodedString.ToString();
            }
            return null;
        }

        public static string ToVariableName(this string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "_";

            // 1. Replace any non-alphanumeric character with underscore
            string result = Regex.Replace(input, @"[^A-Za-z0-9]", "_");

            // 2. Remove multiple underscores in a row
            result = Regex.Replace(result, @"_+", "_");

            // 3. Trim leading/trailing underscores
            result = result.Trim('_');

            // 4. If empty after cleaning, default to underscore
            if (string.IsNullOrEmpty(result))
                result = "_";

            // 5. If starts with a digit, prepend underscore
            if (char.IsDigit(result[0]))
                result = "_" + result;

            return result;
        }

        public static string? DecodeFieldName(this string toDecode)
        {
            if (toDecode != null)
            {
                string decodedString = toDecode.Replace("_x", "%u").Replace("_", "");
                return HttpUtility.UrlDecode(decodedString);
            }
            else
            {
                return null;
            }
        }
    }
}
