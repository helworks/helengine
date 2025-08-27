using System.Collections.Generic;

namespace Nucleus {
    public static class ListUtil {
        public static List<T> GetDeleteFirstNElements<T>(List<T> list, int n) {
            // Ensure n does not exceed the list count
            if (n > list.Count) n = list.Count;

            // Get the first n elements
            List<T> firstNElements = list.GetRange(0, n);

            // Remove the first n elements from the original list
            list.RemoveRange(0, n);

            return firstNElements;
        }
    }
}
