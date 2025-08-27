using System;
using System.Collections;
using System.Linq;
using System.Text;

namespace Nucleus {
    public static class StringUtil {
        public static string CapitalizerFirstLetter(string str) {
            return str[0].ToString().ToUpperInvariant() + str.Remove(0, 1);
        }

        public static string ToCamelCase(string str) {
            return str[0].ToString().ToLowerInvariant() + str.Remove(0, 1);
        }

        public static string Repeat(string str, int times) {
            if (times == 0) {
                return "";
            }

            string result = "";
            for (int i = 0; i < times; i++) {
                result += str;
            }

            return result;
        }

        public static string GetFileSize(int size) {
            if (size < 1024) {
                return $"{size}b";
            } else if (size < Math.Pow(1024, 2)) {
                return $"{(size / Math.Pow(1024, 1)).ToString("F2")}kb";
            } else if (size < Math.Pow(1024, 3)) {
                return $"{(size / Math.Pow(1024, 2)).ToString("F2")}mb";
            } else if (size < Math.Pow(1024, 4)) {
                return $"{(size / Math.Pow(1024, 3)).ToString("F2")}gb";
            } else if (size < Math.Pow(1024, 5)) {
                return $"{(size / Math.Pow(1024, 4)).ToString("F2")}tb";
            } else if (size < Math.Pow(1024, 6)) {
                return $"{(size / Math.Pow(1024, 5)).ToString("F2")}pb";
            }
            return "unknown";
        }

        public static string AddZero(int val) {
            string str = val.ToString();

            if (str.Length == 1) {
                str = '0' + str;
            }

            return str;
        }

        public static string LoremIpsum(Random rand, int minWords, int maxWords,
            int minSentences, int maxSentences,
            int numParagraphs) {

            var words = new[]{"lorem", "ipsum", "dolor", "sit", "amet", "consectetuer",
                "adipiscing", "elit", "sed", "diam", "nonummy", "nibh", "euismod",
                "tincidunt", "ut", "laoreet", "dolore", "magna", "aliquam", "erat"};

            int numSentences = rand.Next(maxSentences - minSentences)
                + minSentences + 1;
            int numWords = rand.Next(maxWords - minWords) + minWords + 1;

            StringBuilder result = new StringBuilder();

            for (int p = 0; p < numParagraphs; p++) {
                for (int s = 0; s < numSentences; s++) {
                    for (int w = 0; w < numWords; w++) {
                        if (w > 0) { result.Append(" "); }
                        result.Append(words[rand.Next(words.Length)]);
                    }
                    result.Append(". ");
                }
            }

            return result.ToString();
        }

        public static string Replace(string strValue, string matchPattern, string toReplace) {
            while (strValue.Contains(matchPattern)) {
                strValue = strValue.Replace(matchPattern, toReplace);
            }
            return strValue;
        }

        public static string ReplaceCall(
            string strValue,
            string matchPattern, string toReplace,
            Func<string, string, string> replaced
            ) {
            string final = "";

            int index = strValue.IndexOf(matchPattern);
            if (index == -1) {
                return strValue;
            }

            while (index != -1) {
                final += strValue.Remove(index);
                final += toReplace;
                final = replaced(final, strValue.Remove(0, index + matchPattern.Length));

                index = strValue.IndexOf(matchPattern, index + 1);
            }
            return final;
        }

        public static string ReplaceCallBefore(string strValue,
            string matchPattern,
            string toReplace,
            Func<string, int, bool> replaced) {
            string final = "";

            int index = strValue.IndexOf(matchPattern);
            if (index == -1) {
                return strValue;
            }

            while (index != -1) {
                if (replaced(strValue, index)) {
                    final += strValue.Remove(index);
                    final += toReplace;
                } else {
                    final += strValue.Remove(index + matchPattern.Length);
                }

                int newIndex = strValue.IndexOf(matchPattern, index + 1);
                if (newIndex == -1) {
                    final += strValue.Remove(0, index + matchPattern.Length);
                }
                index = newIndex;
            }
            return final;
        }


        public static string ReplaceChar(string strValue, char matchPattern, string toReplace) {
            string final = "";

            for (int i = 0; i < strValue.Length; i++) {
                char c = strValue[i];

                if (c == matchPattern) {
                    final += toReplace;
                } else {
                    final += c;
                }
            }

            return final;
        }

        public static string ClearTextString(string strValue) {
            while (strValue.Contains("\t")) {
                strValue = strValue.Replace("\t", "");
            }
            return strValue;
        }

        public static string ClearStartSpaces(string strValue) {
            while (strValue[0] == ' ') {
                strValue = strValue.Remove(0, 1);
            }
            return strValue;
        }

        public static string GetCollisionStringStart(string aStr, string bStr) {
            string col = "";

            for (int i = 0; i < aStr.Length; i++) {
                if (bStr.Length <= i) {
                    break;
                }

                char a = aStr[i];
                char b = bStr[i];
                if (a == b) {
                    col += a;
                } else {
                    break;
                }
            }

            return col;
        }

        public static readonly char[] Numbers = new char[]
        {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'
        };

        public static bool IsNumber(char c) {
            // We COULD use Int.TryParse, but this looks way cleaner
            return Numbers.Contains(c);
        }

        public static string RepeatCharacter(char c, int times) {
            string s = "";
            for (int i = 0; i < times; i++) {
                s += c;
            }
            return s;
        }

        public static void MakeSameSize(ref string a, ref string b) {
            if (a.Length < b.Length) {
                int dif = b.Length - a.Length;
                for (int k = 0; k < dif; k++) {
                    a = " " + a;
                }
            } else if (a.Length > b.Length) {
                int dif = a.Length - b.Length;
                for (int k = 0; k < dif; k++) {
                    b = " " + b;
                }
            }
        }

        public static string ReplaceCaseInsensitive(string str, string toFind, string toReplace) {
            string lowerOriginal = str.ToLower();
            string lowerFind = toFind.ToLower();
            string lowerRep = toReplace.ToLower();

            int start = lowerOriginal.IndexOf(lowerFind);
            if (start == -1) {
                return str;
            }

            string end = str.Remove(start, toFind.Length);
            end = end.Insert(start, toReplace);

            return end;
        }

        public static string ArrayToString(Array array) {
            string str = "";
            for (int i = 0; i < array.Length; i++) {
                object ob = ((IList)array)[i];
                str += ob;
                if (i != array.Length - 1) {
                    ob += ", ";
                }
            }
            return str;
        }

        public static string Base64Encode(string plainText) {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        public static string Base64Decode(string base64EncodedData) {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }

#if WINFORMS
        /// <summary>
        /// This method can be made better
        /// </summary>
        /// <param name="maxWidth"></param>
        /// <param name="str"></param>
        /// <param name="graphics"></param>
        /// <param name="font"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public static string WrapString(float maxWidth, string str, Graphics graphics, Font font, out SizeF size) {
            size = graphics.MeasureString(str, font);

            string[] sep = str.Split(' ');
            if (sep.Length == 0) {
                return str;
            }

            float spaceSize = graphics.MeasureString(" ", font).Width;

            string result = sep[0];

            float currentWidth = graphics.MeasureString(result, font).Width;
            float maxUsedWidth = 0;
            int lines = 1;
            string currentLine = result;

            for (int i = 1; i < sep.Length; i++) {
                string word = sep[i];
                string spaceWord = " " + word;
                SizeF wordSize = graphics.MeasureString(spaceWord, font);
                currentWidth += wordSize.Width;

                if (currentWidth > maxWidth) {
                    currentWidth = wordSize.Width;
                    maxUsedWidth = Math.Max(maxUsedWidth, graphics.MeasureString(currentLine, font).Width);
                    result += "\n" + word;
                    currentLine = "";
                    lines++;
                } else {
                    result += spaceWord;
                    currentLine += spaceWord;
                }
            }

            if (maxUsedWidth == 0) {
                maxUsedWidth = Math.Max(maxUsedWidth, graphics.MeasureString(currentLine, font).Width);
            }
            size = new SizeF(maxUsedWidth, lines * font.GetHeight(graphics));

            return result;
        }
#endif
    }
}
