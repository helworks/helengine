using Nucleus.Animation.Interpolators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nucleus.Animation {
    public class AnimationPlayer {
        private double time;
        private double timeBetweenFrames;
        private int currentFrameIndex;
        private KeyValuePair<int, AnimationFrame>[] frames;

        public bool Playing { get; private set; }
        public Action NewFrame { get; set; }
        public double FrameRate { get; set; } = 60;
        public AnimationPlayerInvoker Invoker { get; set; }

        public AnimationPlayer() {

        }

        public void Play(AnimationClip clip) {
            if (Playing) {
                return;
            }
            this.timeBetweenFrames = (1 / FrameRate);

            this.Playing = true;
            this.time = 0;

            this.frames = clip.Frames.OrderBy(c => c.Key).ToArray();
            this.currentFrameIndex = 0;

            Thread thread = new Thread(AnimationThread);
            thread.Start();
        }

        private void AnimationThread() {
            for (; ; ) {
                if (!Playing ||
                    currentFrameIndex + 1 >= frames.Length) {
                    Playing = false;
                    break;
                }

                KeyValuePair<int, AnimationFrame> currentFrame = frames[currentFrameIndex];
                KeyValuePair<int, AnimationFrame> nextFrame = frames[currentFrameIndex + 1];

                // calculate 0-1 from one frame to the next
                double size = nextFrame.Key - currentFrame.Key;
                double delta = time / size;

                double interpolated = InterpolatorService.Interpolate(delta, currentFrame.Value.Interpolator);

                if (Invoker != null) {
                    Invoker(interpolated, nextFrame.Value.Action);
                } else {
                    nextFrame.Value.Action(interpolated);
                }

                if (delta >= 1.0) {
                    // execute again
                    currentFrameIndex++;
                    continue;
                }

                this.time += timeBetweenFrames;

                if (NewFrame != null) {
                    NewFrame();
                }
                Thread.Sleep(TimeSpan.FromMilliseconds(timeBetweenFrames));
            }
        }
    }
}
