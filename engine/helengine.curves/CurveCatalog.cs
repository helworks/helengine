namespace helengine {
    /// <summary>
    /// Versioned easing curves shared by media compositions and timelines. A curve id names one exact formula forever
    /// (a changed formula gets a new <c>.vN</c> id), so authored motion looks the same after engine upgrades. Every curve
    /// also has a stable one-byte code that cooked formats store instead of the id string.
    /// </summary>
    public static class CurveCatalog {
        /// <summary>
        /// Id of the straight-line curve: progress maps to itself.
        /// </summary>
        public const string Linear = "linear.v1";

        /// <summary>
        /// Id of the cubic Hermite smoothstep curve: slow start, slow end.
        /// </summary>
        public const string Smoothstep = "smoothstep.v1";

        /// <summary>
        /// Id of the cubic ease-out curve: fast start that settles gently.
        /// </summary>
        public const string EaseOutCubic = "ease_out_cubic.v1";

        /// <summary>
        /// Id of the quadratic ease-in curve: slow start that accelerates.
        /// </summary>
        public const string EaseInQuad = "ease_in_quad.v1";

        /// <summary>
        /// Id of the back ease-out curve: travels about ten percent past the target near 60% progress and settles on it,
        /// giving pops a soft overshoot.
        /// </summary>
        public const string EaseOutBack = "ease_out_back.v1";

        /// <summary>
        /// Cooked code of <see cref="Linear"/>.
        /// </summary>
        public const byte LinearCode = 0;

        /// <summary>
        /// Cooked code of <see cref="Smoothstep"/>.
        /// </summary>
        public const byte SmoothstepCode = 1;

        /// <summary>
        /// Cooked code of <see cref="EaseOutCubic"/>.
        /// </summary>
        public const byte EaseOutCubicCode = 2;

        /// <summary>
        /// Cooked code of <see cref="EaseInQuad"/>.
        /// </summary>
        public const byte EaseInQuadCode = 3;

        /// <summary>
        /// Cooked code of <see cref="EaseOutBack"/>.
        /// </summary>
        public const byte EaseOutBackCode = 4;

        /// <summary>
        /// Overshoot constant of the back ease-out formula (the classic Penner value).
        /// </summary>
        const double BackOvershoot = 1.70158;

        /// <summary>
        /// Every supported curve id, indexed by its cooked code.
        /// </summary>
        static readonly string[] IdsByCode = new string[] { Linear, Smoothstep, EaseOutCubic, EaseInQuad, EaseOutBack };

        /// <summary>
        /// Gets the number of curves in the catalog; valid cooked codes are zero up to this count minus one.
        /// </summary>
        public static int Count {
            get {
                return IdsByCode.Length;
            }
        }

        /// <summary>
        /// Checks whether a curve id names an exact formula of this catalog.
        /// </summary>
        /// <param name="id">Curve id such as <c>ease_out_back.v1</c>.</param>
        /// <returns>True when the id is supported; false for unknown or null ids.</returns>
        public static bool Supports(string id) {
            return FindCode(id) >= 0;
        }

        /// <summary>
        /// Returns the cooked one-byte code of a curve id.
        /// </summary>
        /// <param name="id">Supported curve id.</param>
        /// <returns>Stable code between zero and <see cref="Count"/> minus one.</returns>
        /// <exception cref="InvalidDataException">The id is not in the catalog.</exception>
        public static byte GetCode(string id) {
            int code = FindCode(id);
            if (code < 0) {
                throw new InvalidDataException("Unsupported curve: " + id);
            }
            return (byte)code;
        }

        /// <summary>
        /// Returns the curve id stored under a cooked code.
        /// </summary>
        /// <param name="code">Cooked curve code.</param>
        /// <returns>The matching curve id.</returns>
        /// <exception cref="InvalidDataException">The code is outside the catalog.</exception>
        public static string GetId(byte code) {
            if (code >= IdsByCode.Length) {
                throw new InvalidDataException("Unsupported curve code: " + code);
            }
            return IdsByCode[code];
        }

        /// <summary>
        /// Evaluates the named curve at a progress value, never substituting a different easing.
        /// </summary>
        /// <param name="id">Supported curve id.</param>
        /// <param name="progress">Segment progress; clamped to zero..one, must be finite.</param>
        /// <returns>Eased progress; only <see cref="EaseOutBack"/> leaves zero..one.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The progress is NaN or infinite.</exception>
        /// <exception cref="InvalidDataException">The id is not in the catalog.</exception>
        public static double Evaluate(string id, double progress) {
            ValidateProgress(progress);
            return Evaluate(GetCode(id), progress);
        }

        /// <summary>
        /// Evaluates the curve stored under a cooked code at a progress value.
        /// </summary>
        /// <param name="code">Cooked curve code.</param>
        /// <param name="progress">Segment progress; clamped to zero..one, must be finite.</param>
        /// <returns>Eased progress.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The progress is NaN or infinite.</exception>
        /// <exception cref="InvalidDataException">The code is outside the catalog.</exception>
        public static double Evaluate(byte code, double progress) {
            ValidateProgress(progress);
            double value = progress < 0 ? 0 : (progress > 1 ? 1 : progress);
            if (code == LinearCode) {
                return value;
            } else if (code == SmoothstepCode) {
                return value * value * (3 - 2 * value);
            } else if (code == EaseOutCubicCode) {
                return 1 - Math.Pow(1 - value, 3);
            } else if (code == EaseInQuadCode) {
                return value * value;
            } else if (code == EaseOutBackCode) {
                double shifted = value - 1;
                return 1 + (BackOvershoot + 1) * shifted * shifted * shifted + BackOvershoot * shifted * shifted;
            }
            throw new InvalidDataException("Unsupported curve code: " + code);
        }

        /// <summary>
        /// Finds the cooked code of a curve id by ordinal comparison.
        /// </summary>
        /// <param name="id">Candidate curve id.</param>
        /// <returns>The code, or -1 when the id is null or unknown.</returns>
        static int FindCode(string id) {
            if (id == null) {
                return -1;
            }
            for (int index = 0; index < IdsByCode.Length; index++) {
                if (string.Equals(IdsByCode[index], id, StringComparison.Ordinal)) {
                    return index;
                }
            }
            return -1;
        }

        /// <summary>
        /// Rejects NaN and infinite progress, which would otherwise propagate silently into rendered motion.
        /// </summary>
        /// <param name="progress">Progress to check.</param>
        static void ValidateProgress(double progress) {
            if (double.IsNaN(progress) || double.IsInfinity(progress)) {
                throw new ArgumentOutOfRangeException(nameof(progress));
            }
        }
    }
}
