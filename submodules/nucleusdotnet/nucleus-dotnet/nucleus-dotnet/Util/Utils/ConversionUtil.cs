namespace Nucleus {
    public static class ConversionUtil {
        public static float MillisecondToSecond(float milliseconds) {
            return milliseconds / 1000f;
        }
        public static float SecondToMillisecond(float seconds) {
            return seconds * 1000f;
        }

        public static float FrameToMillisecond(float frame, float timeStep) {
            //float timeStep = ((1 / (float)TimeStep) * 1000);
            return frame * timeStep;
        }
        public static float FrameToSecond(float frame, float timeStep) {
            //float timeStep = ((1 / (float)TimeStep) * 1000);
            float sec = MillisecondToSecond(timeStep);
            return frame * sec;
        }

        public static double BytesToMegabytes(double size) {
            return size / 1024.0 / 1024.0;
        }

        public static string BytesToMegabytesString(double size) {
            return BytesToMegabytes(size).ToString("F2") + " MB";
        }
    }
}
