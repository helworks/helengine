namespace Nucleus {
    public static class ArrayUtil {
        public static bool IsContentsEqual<T>(T[] a, T[] b) {
            if (a.Length != b.Length) {
                return false;
            }

            for (int i = 0; i < a.Length; i++) {
                T aa = a[i];
                T bb = b[i];
                if (!aa.Equals(bb)) {
                    return false;
                }
            }
            return true;
        }

        public static T[] Join<T>(T[] a, T[] b) {
            T[] final = new T[a.Length + b.Length];

            for (int i = 0; i < a.Length; i++) {
                final[i] = a[i];
            }

            for (int i = 0; i < b.Length; i++) {
                final[i + a.Length] = b[i];
            }

            return final;
        }

        public static string ToString<T>(T[] data) {
            string str = "";

            for (int i = 0; i < data.Length; i++) {
                str += data[i].ToString();
                if (i != data.Length - 1) {
                    str += ", ";
                }
            }

            return str;
        }
    }
}
