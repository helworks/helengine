using System;
using System.IO;

namespace Nucleus {
    public static class RandomExtensions {

        public static byte[] NextArray(this Random rnd, int size) {
            byte[] array = new byte[size];

            for (int i = 0; i < size; i++) {
                array[i] = (byte)rnd.Next(byte.MaxValue);
            }

            return array;
        }
    }
}
