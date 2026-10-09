using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Built-in scene arrangements: named divisions of the frame into regions that a scene's layers and overlays claim.
    /// Every region has a rectangle per frame orientation (portrait, square, landscape), normalized to the caption-free
    /// safe frame: the part of the frame between a small margin and the estimated caption band, so picture and graphic
    /// regions never reach into the captions, while a background (and the single region of <c>full</c>) fills the frame
    /// behind everything. <c>stack</c> and <c>split</c> keep their region names on every frame
    /// but swap geometry: a stack on a wide frame puts its graphic beside the picture, a split on a tall frame puts it
    /// above. Products call <see cref="Resolve(string, VideoEdit)"/> (or the format-only overload) to size generated media to
    /// the region it fills.
    /// </summary>
    public static class VideoArrangementPresets {
        /// <summary>
        /// Orientation of frames clearly taller than wide.
        /// </summary>
        public const string Portrait = "portrait";

        /// <summary>
        /// Orientation of frames close to square.
        /// </summary>
        public const string Square = "square";

        /// <summary>
        /// Orientation of frames clearly wider than tall.
        /// </summary>
        public const string Landscape = "landscape";

        /// <summary>
        /// Arrangement used when a scene declares none: one full-frame region.
        /// </summary>
        public const string Full = "full";

        /// <summary>
        /// Graphic band above a picture on tall frames; beside it on wide frames.
        /// </summary>
        public const string Stack = "stack";

        /// <summary>
        /// Picture and graphic side by side on wide frames; stacked on tall frames.
        /// </summary>
        public const string Split = "split";

        /// <summary>
        /// Full-frame take with a graphic band clear of the speaker's face and the captions.
        /// </summary>
        public const string TakeWithGraphic = "take_with_graphic";

        /// <summary>
        /// One large graphic and no picture.
        /// </summary>
        public const string GraphicOnly = "graphic_only";

        /// <summary>
        /// Role of a region meant for an image or video layer.
        /// </summary>
        public const string PictureRole = "picture";

        /// <summary>
        /// Role of a region meant for a graphic or text overlay.
        /// </summary>
        public const string GraphicRole = "graphic";

        /// <summary>
        /// Role of a full-frame region drawn behind everything, meant for the take.
        /// </summary>
        public const string BackgroundRole = "background";

        /// <summary>
        /// Aspect ratio beyond which a frame counts as portrait (height over width) or landscape (width over height).
        /// </summary>
        const double OrientationThreshold = 1.15;

        /// <summary>
        /// Margin kept between the safe frame and the frame edge it does not share with the captions, as a fraction of
        /// the frame height.
        /// </summary>
        public const double SafeMargin = 0.04;

        /// <summary>
        /// Bottom of the safe frame assumed when no edit is known: captions in the conventional lower band.
        /// </summary>
        public const double DefaultSafeBottom = 0.7;

        /// <summary>
        /// Smallest safe frame height kept when the captions would leave less; regions then reach into the caption band.
        /// </summary>
        const double MinimumSafeHeight = 0.3;

        /// <summary>
        /// Rectangle of the whole frame.
        /// </summary>
        static readonly VideoViewport Frame = Box(0, 0, 1, 1);

        /// <summary>
        /// Graphic band of a portrait stack: the top third of the safe frame.
        /// </summary>
        static readonly VideoViewport PortraitTop = Box(0.05, 0, 0.9, 0.33);

        /// <summary>
        /// Picture region of a portrait stack: the lower two thirds of the safe frame.
        /// </summary>
        static readonly VideoViewport PortraitMain = Box(0.04, 0.36, 0.92, 0.64);

        /// <summary>
        /// Graphic band of a square stack.
        /// </summary>
        static readonly VideoViewport SquareTop = Box(0.05, 0, 0.9, 0.33);

        /// <summary>
        /// Picture region of a square stack.
        /// </summary>
        static readonly VideoViewport SquareMain = Box(0.05, 0.36, 0.9, 0.64);

        /// <summary>
        /// Picture column of a landscape split: the left half of the safe frame.
        /// </summary>
        static readonly VideoViewport LandscapeLeft = Box(0.03, 0, 0.5, 1);

        /// <summary>
        /// Graphic column of a landscape split: the right part of the safe frame.
        /// </summary>
        static readonly VideoViewport LandscapeRight = Box(0.56, 0.06, 0.4, 0.88);

        /// <summary>
        /// Every built-in arrangement in catalog order.
        /// </summary>
        static readonly IReadOnlyList<VideoArrangementPreset> Presets = [
            new VideoArrangementPreset(Full,
                "One region covering the whole frame; the default when a scene declares no arrangement. Use it for a single full-frame picture or take, with overlays placed by their text style.",
                [new VideoArrangementPresetRegion("main", PictureRole, "The whole frame.", Frame, Frame, Frame, true)]),
            new VideoArrangementPreset(Stack,
                "A graphic band above a picture, both clear of the captions. Use it when a structured graphic (a graphic template overlay) and a picture share the scene on a tall frame; on wide frames the same regions sit side by side like split.",
                [
                    new VideoArrangementPresetRegion("top", GraphicRole, "Graphic band across the top of the frame (about a third of the space above the captions); on landscape frames the right-hand column.", PortraitTop, SquareTop, LandscapeRight, false),
                    new VideoArrangementPresetRegion("main", PictureRole, "Picture area below the graphic band and above the captions (about two thirds of it); on landscape frames the left-hand column.", PortraitMain, SquareMain, LandscapeLeft, false)
                ]),
            new VideoArrangementPreset(Split,
                "A picture and a graphic side by side, both clear of the captions. Use it when a structured graphic and a picture share the scene on a wide frame; on tall frames the same regions stack like stack (graphic above, picture below).",
                [
                    new VideoArrangementPresetRegion("left", PictureRole, "Picture column on the left; on portrait and square frames the picture area below the graphic band.", PortraitMain, SquareMain, LandscapeLeft, false),
                    new VideoArrangementPresetRegion("right", GraphicRole, "Graphic column on the right; on portrait and square frames the graphic band across the top.", PortraitTop, SquareTop, LandscapeRight, false)
                ]),
            new VideoArrangementPreset(TakeWithGraphic,
                "The speaker's take fills the frame and a graphic sits in a band at the top, clear of the captions. Use it when a take and a graphic share the scene.",
                [
                    new VideoArrangementPresetRegion("background", BackgroundRole, "The whole frame, behind everything; meant for the take.", Frame, Frame, Frame, true),
                    new VideoArrangementPresetRegion("band", GraphicRole, "Graphic band across the top of the frame, above the speaker's face and the captions, on every orientation.", Box(0.05, 0, 0.9, 0.22), Box(0.06, 0, 0.88, 0.24), Box(0.1, 0, 0.8, 0.26), false)
                ]),
            new VideoArrangementPreset(GraphicOnly,
                "One large graphic centered in the frame above the captions, and no picture. Use it when the scene is carried by a graphic template overlay alone.",
                [new VideoArrangementPresetRegion("main", GraphicRole, "Most of the frame above the captions, centered, on every orientation.", Box(0.06, 0.06, 0.88, 0.88), Box(0.08, 0.05, 0.84, 0.9), Box(0.1, 0.05, 0.8, 0.9), false)])
        ];

        /// <summary>
        /// Gets every built-in arrangement in catalog order.
        /// </summary>
        public static IReadOnlyList<VideoArrangementPreset> All {
            get {
                return Presets;
            }
        }

        /// <summary>
        /// Gets the ids of every built-in arrangement.
        /// </summary>
        public static string[] Ids {
            get {
                return Presets.Select(preset => preset.Id).ToArray();
            }
        }

        /// <summary>
        /// Gets every distinct region name used by any arrangement.
        /// </summary>
        public static string[] RegionNames {
            get {
                return Presets.SelectMany(preset => preset.Regions).Select(region => region.Name).Distinct(StringComparer.Ordinal).ToArray();
            }
        }

        /// <summary>
        /// Finds a built-in arrangement.
        /// </summary>
        /// <param name="id">Preset id.</param>
        /// <returns>Preset, or null when the id is unknown.</returns>
        public static VideoArrangementPreset Find(string id) {
            return Presets.FirstOrDefault(preset => preset.Id == id);
        }

        /// <summary>
        /// Names the arrangement a scene uses: its declared preset, or <see cref="Full"/> when it declares none.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <returns>Preset id.</returns>
        public static string Of(VideoScene scene) {
            return scene.Arrangement == null ? Full : scene.Arrangement.Preset;
        }

        /// <summary>
        /// Classifies a frame as portrait, square or landscape.
        /// </summary>
        /// <param name="width">Frame width in pixels.</param>
        /// <param name="height">Frame height in pixels.</param>
        /// <returns>Orientation name.</returns>
        public static string Orientation(double width, double height) {
            if (width <= 0 || height <= 0) {
                throw new ArgumentException("A frame needs a positive size.");
            }
            if (height / width > OrientationThreshold) {
                return Portrait;
            } else if (width / height > OrientationThreshold) {
                return Landscape;
            }
            return Square;
        }

        /// <summary>
        /// Resolves an arrangement for an output format alone, assuming captions in the conventional lower band (the safe
        /// frame ends at <see cref="DefaultSafeBottom"/>). Use the edit overload when the edit is known.
        /// </summary>
        /// <param name="preset">Preset id.</param>
        /// <param name="format">Output format.</param>
        /// <returns>Resolved arrangement.</returns>
        /// <exception cref="InvalidDataException">The preset is unknown.</exception>
        public static VideoResolvedArrangement Resolve(string preset, VideoFormat format) {
            if (format == null) {
                throw new ArgumentNullException(nameof(format));
            }
            return Resolve(preset, format.Width, format.Height, SafeMargin, DefaultSafeBottom);
        }

        /// <summary>
        /// Resolves an arrangement for an edit: its output format, with the safe frame ending above (or starting below)
        /// the edit's estimated caption band (see <see cref="VideoCaptionBand"/>), or spanning the frame between margins
        /// when the edit draws no captions. This is the geometry the compiler uses.
        /// </summary>
        /// <param name="preset">Preset id.</param>
        /// <param name="edit">Edit supplying the format, captions and text styles.</param>
        /// <returns>Resolved arrangement.</returns>
        /// <exception cref="InvalidDataException">The preset is unknown.</exception>
        public static VideoResolvedArrangement Resolve(string preset, VideoEdit edit) {
            if (edit?.Format == null) {
                throw new ArgumentException("An edit with a format is required.", nameof(edit));
            }
            double top = SafeMargin, bottom = 1 - SafeMargin;
            VideoCaptionBand captions = VideoCaptionBand.Estimate(edit);
            bool below = captions != null && captions.Center >= 0.5;
            if (below) {
                bottom = Math.Min(bottom, captions.Top - VideoCaptionBand.Gap);
            } else if (captions != null) {
                top = Math.Max(top, captions.Bottom + VideoCaptionBand.Gap);
            }
            if (bottom - top < MinimumSafeHeight) {
                if (below) {
                    bottom = top + MinimumSafeHeight;
                } else {
                    top = bottom - MinimumSafeHeight;
                }
            }
            return Resolve(preset, edit.Format.Width, edit.Format.Height, top, bottom);
        }

        /// <summary>
        /// Resolves an arrangement for an output frame size and a safe frame: every safe-frame region is mapped into the
        /// band between the safe top and bottom, and full-frame regions cover the frame.
        /// </summary>
        /// <param name="preset">Preset id.</param>
        /// <param name="width">Frame width in pixels.</param>
        /// <param name="height">Frame height in pixels.</param>
        /// <param name="safeTop">Top of the safe frame as a fraction of the frame height.</param>
        /// <param name="safeBottom">Bottom of the safe frame as a fraction of the frame height.</param>
        /// <returns>Resolved arrangement.</returns>
        /// <exception cref="InvalidDataException">The preset is unknown.</exception>
        public static VideoResolvedArrangement Resolve(string preset, int width, int height, double safeTop, double safeBottom) {
            VideoArrangementPreset definition = Find(preset);
            if (definition == null) {
                throw new InvalidDataException($"'{preset}' is not a built-in arrangement; use one of {string.Join(", ", Ids)}.");
            }
            if (!double.IsFinite(safeTop) || !double.IsFinite(safeBottom) || safeTop < 0 || safeBottom > 1 || safeBottom <= safeTop) {
                throw new ArgumentException($"The safe frame must lie inside the frame with its bottom below its top (got {safeTop} to {safeBottom}).");
            }
            string orientation = Orientation(width, height);
            double safeHeight = safeBottom - safeTop;
            List<VideoArrangementRegion> regions = new List<VideoArrangementRegion>();
            foreach (VideoArrangementPresetRegion region in definition.Regions) {
                if (region.FillsFrame) {
                    regions.Add(new VideoArrangementRegion(region.Name, region.Role, 0, 0, 1, 1, width, height));
                } else {
                    VideoViewport box = region.For(orientation);
                    regions.Add(new VideoArrangementRegion(region.Name, region.Role, box.X, safeTop + box.Y * safeHeight, box.Width, box.Height * safeHeight, width, height));
                }
            }
            return new VideoResolvedArrangement(definition.Id, orientation, safeTop, safeBottom, regions);
        }

        /// <summary>
        /// Resolves the region a scene's layer or overlay claims, for the edit's format and captions.
        /// </summary>
        /// <param name="edit">Edit supplying the format, captions and text styles.</param>
        /// <param name="scene">Scene declaring the arrangement (or none, meaning <see cref="Full"/>).</param>
        /// <param name="region">Region name.</param>
        /// <returns>Resolved region.</returns>
        public static VideoArrangementRegion Region(VideoEdit edit, VideoScene scene, string region) {
            return Resolve(Of(scene), edit).Find(region);
        }

        /// <summary>
        /// Describes every arrangement for the capability catalog.
        /// </summary>
        /// <returns>Descriptors in catalog order.</returns>
        public static List<MediaArrangementDescriptor> Describe() {
            return Presets.Select(preset => new MediaArrangementDescriptor {
                Id = preset.Id,
                Description = preset.Description,
                Regions = preset.Regions.Select(region => new MediaArrangementRegionDescriptor { Name = region.Name, Role = region.Role, Description = region.Description }).ToList()
            }).ToList();
        }

        /// <summary>
        /// Publishes every arrangement into a capability catalog, replacing any arrangements it listed.
        /// </summary>
        /// <param name="capabilities">Capability catalog to fill.</param>
        public static void Publish(MediaCapabilities capabilities) {
            if (capabilities == null) {
                throw new ArgumentNullException(nameof(capabilities));
            }
            capabilities.Arrangements = Describe();
        }

        /// <summary>
        /// Builds one normalized rectangle.
        /// </summary>
        /// <param name="x">Left edge.</param>
        /// <param name="y">Top edge.</param>
        /// <param name="width">Width.</param>
        /// <param name="height">Height.</param>
        /// <returns>Rectangle.</returns>
        static VideoViewport Box(double x, double y, double width, double height) {
            return new VideoViewport { X = x, Y = y, Width = width, Height = height };
        }
    }
}
