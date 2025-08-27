using System;

namespace Nucleus {
    public static class MathUtil {
        public static float Lerp(float value1, float value2, float amount) {
            return value1 + (value2 - value1) * amount;
        }

        public static double Lerp(double value1, double value2, double amount) {
            return value1 + (value2 - value1) * amount;
        }

        public static int Lerp(int value1, int value2, float amount) {
            return (int)Lerp((float)value1, value2, amount);
        }

        public static int Lerp(int value1, int value2, double amount) {
            return (int)Lerp((double)value1, value2, amount);
        }

        public static int Clamp(int value, int min, int max) {
            return Math.Max(min, Math.Min(value, max));
        }

        public static float ToDegrees(float radians) {
            return (float)(radians * 57.295779513082320876798154814105);
        }

        public static float ToRadians(float degrees) {
            return (float)(degrees * 0.017453292519943295769236907684886);
        }

        /// <summary>
        /// Greatest Common Divisor
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static int GCD(int a, int b) {
            int Remainder;

            while (b != 0) {
                Remainder = a % b;
                a = b;
                b = Remainder;
            }

            return a;
        }

        public static double RecursiveSqrt(double value, int count) {
            RecursiveSqrt(ref value, count);
            return value;
        }

        public static void RecursiveSqrt(ref double value, int count) {
            for (int i = 0; i < count; i++) {
                value = Math.Sqrt(value);
            }
        }

        public static double RecursivePercentage(double pc, int count) {
            double remaining = 1;
            double value = 0;
            for (int i = 0; i < count; i++) {
                double removed = remaining * pc;
                value = removed;
                remaining -= removed;
            }
            return value;
        }
    }
}
