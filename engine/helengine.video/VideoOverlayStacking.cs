using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Turns plain overlays that would be drawn on top of each other into one stacked list. Plain overlays of a scene that
    /// share a text style and region and are on screen at the same time are compiled as one <c>list_build</c> graphic:
    /// measured, stacked in reading order, each item appearing at its own moment. Groups the list template cannot hold
    /// (more than its item count or longer texts) are left as they are.
    /// </summary>
    public static class VideoOverlayStacking {
        /// <summary>
        /// Catalog template used for stacked overlays.
        /// </summary>
        public const string Template = "list_build";

        /// <summary>
        /// Lists the overlays to compile for a scene, replacing each group of simultaneous plain overlays by one list
        /// graphic overlay.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        /// <param name="span">Scene span.</param>
        /// <param name="path">Scene JSON path.</param>
        /// <returns>Overlays to compile, in scene order, with their JSON paths.</returns>
        public static List<VideoOverlayEntry> Arrange(VideoCompileState state, VideoSceneSpan span, string path) {
            List<VideoOverlay> overlays = span.Scene.Overlays;
            List<VideoOverlayEntry> result = new List<VideoOverlayEntry>();
            for (int index = 0; index < overlays.Count; index++) {
                result.Add(new VideoOverlayEntry(overlays[index], $"{path}.overlays[{index}]"));
            }
            GraphicTemplateAsset template = state.Context.GraphicTemplates?.Find(Template, 1);
            if (template == null) {
                return result;
            }
            GraphicTemplateSlotAsset items = GraphicTemplateValidator.FindSlot(template, GraphicTemplateValidator.ItemsSlot);
            List<VideoOverlayTiming> plain = new List<VideoOverlayTiming>();
            foreach (VideoOverlayEntry entry in result) {
                VideoOverlay overlay = entry.Overlay;
                if (overlay.Graphic != null || overlay.Timeline != null || string.IsNullOrWhiteSpace(overlay.Text)) {
                    continue;
                }
                MediaTime start = state.Global(span, overlay.At, entry.Path + ".at");
                MediaTime end = overlay.Until == null ? span.End : state.Global(span, overlay.Until, entry.Path + ".until");
                if (end > start) {
                    plain.Add(new VideoOverlayTiming(entry, start, end));
                }
            }
            foreach (IGrouping<string, VideoOverlayTiming> group in plain.GroupBy(timing => (timing.Entry.Overlay.Style ?? "") + "|" + (timing.Entry.Overlay.Region ?? ""))) {
                List<VideoOverlayTiming> ordered = group.OrderBy(timing => timing.Start).ToList();
                int first = 0;
                while (first < ordered.Count) {
                    MediaTime reach = ordered[first].End;
                    int last = first;
                    while (last + 1 < ordered.Count && ordered[last + 1].Start < reach) {
                        last++;
                        if (ordered[last].End > reach) {
                            reach = ordered[last].End;
                        }
                    }
                    List<VideoOverlayTiming> cluster = ordered.GetRange(first, last - first + 1);
                    if (cluster.Count >= Math.Max(2, items.MinCount) && cluster.Count <= items.MaxCount && cluster.All(timing => timing.Entry.Overlay.Text.Trim().Length <= items.MaxChars)) {
                        Stack(state, span, result, cluster);
                    }
                    first = last + 1;
                }
            }
            return result;
        }

        /// <summary>
        /// Replaces a cluster of simultaneous overlays by one list graphic at the position of its first overlay.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        /// <param name="span">Scene span.</param>
        /// <param name="result">Overlays to compile, edited in place.</param>
        /// <param name="cluster">Simultaneous overlays in start order.</param>
        static void Stack(VideoCompileState state, VideoSceneSpan span, List<VideoOverlayEntry> result, List<VideoOverlayTiming> cluster) {
            VideoOverlay lead = cluster[0].Entry.Overlay;
            VideoOverlayTiming longest = cluster.OrderByDescending(timing => timing.End).First();
            VideoOverlay stacked = new VideoOverlay {
                Id = lead.Id + "-stack",
                Text = string.Join(" / ", cluster.Select(timing => timing.Entry.Overlay.Text.Trim())),
                Style = lead.Style,
                Region = lead.Region,
                Until = longest.Entry.Overlay.Until,
                Graphic = new VideoGraphic {
                    Template = Template,
                    Version = 1,
                    Items = cluster.Select(timing => timing.Entry.Overlay.Text.Trim()).ToList(),
                    At = cluster.Select(timing => timing.Entry.Overlay.At ?? new VideoMoment { Sec = 0 }).ToList()
                }
            };
            int position = result.FindIndex(entry => entry.Overlay == lead);
            string path = result[position].Path;
            foreach (VideoOverlayTiming timing in cluster) {
                result.RemoveAll(entry => entry.Overlay == timing.Entry.Overlay);
            }
            result.Insert(Math.Min(position, result.Count), new VideoOverlayEntry(stacked, path));
            state.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Info, "overlays_stacked", span.Scene.Id, path,
                $"{cluster.Count} overlays shown at the same time were stacked as one list instead of being drawn on top of each other."));
        }
    }
}
