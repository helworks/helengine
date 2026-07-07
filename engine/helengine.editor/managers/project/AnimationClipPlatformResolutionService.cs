namespace helengine.editor {
    /// <summary>
    /// Resolves one editor-authored animation clip into the flat platform-specific clip consumed by runtime playback.
    /// </summary>
    public sealed class AnimationClipPlatformResolutionService {
        /// <summary>
        /// Resolves one clip for the supplied platform identifier.
        /// </summary>
        /// <param name="clip">Editor-authored clip to resolve.</param>
        /// <param name="platformId">Platform identifier whose resolved timeline should be produced.</param>
        /// <returns>Flat runtime-ready clip for the requested platform.</returns>
        public AnimationClipAsset ResolveForPlatform(AnimationClipAsset clip, string platformId) {
            if (clip == null) {
                throw new ArgumentNullException(nameof(clip));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            AnimationClipPlatformOverrideAsset platformOverride = ResolvePlatformOverride(clip, platformId);
            if (platformOverride == null || platformOverride.Mode == AnimationClipPlatformOverrideMode.InheritBase) {
                return CloneClipPreservingFrameIds(clip);
            } else if (platformOverride.Mode == AnimationClipPlatformOverrideMode.ReplaceWholeClip) {
                return ResolveReplaceWholeClip(clip, platformOverride);
            }

            return ResolveOverrideFrames(clip, platformOverride);
        }

        /// <summary>
        /// Resolves the override payload that belongs to the requested platform when one exists.
        /// </summary>
        /// <param name="clip">Clip whose platform overrides should be inspected.</param>
        /// <param name="platformId">Platform identifier to locate.</param>
        /// <returns>Matching override payload, or null when the platform inherits the base clip.</returns>
        AnimationClipPlatformOverrideAsset ResolvePlatformOverride(AnimationClipAsset clip, string platformId) {
            if (clip.PlatformOverrides == null) {
                return null;
            }

            for (int index = 0; index < clip.PlatformOverrides.Length; index++) {
                AnimationClipPlatformOverrideAsset currentOverride = clip.PlatformOverrides[index];
                if (currentOverride == null || !string.Equals(currentOverride.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                return currentOverride;
            }

            return null;
        }

        /// <summary>
        /// Clones one clip while keeping its editor-only frame identifiers intact.
        /// </summary>
        /// <param name="clip">Source clip to clone.</param>
        /// <returns>Detached clone of the source clip.</returns>
        AnimationClipAsset CloneClipPreservingFrameIds(AnimationClipAsset clip) {
            return new AnimationClipAsset {
                Id = clip.Id,
                Duration = clip.Duration,
                PositionTracks = ClonePositionTracks(clip.PositionTracks, true),
                PositionOffsetTracks = ClonePositionOffsetTracks(clip.PositionOffsetTracks, true),
                ScaleTracks = CloneScaleTracks(clip.ScaleTracks, true),
                RotationTracks = CloneRotationTracks(clip.RotationTracks, true),
                PlatformOverrides = Array.Empty<AnimationClipPlatformOverrideAsset>()
            };
        }

        /// <summary>
        /// Resolves one full replacement override into the flat runtime clip shape.
        /// </summary>
        /// <param name="clip">Base clip whose identity and duration should be preserved.</param>
        /// <param name="platformOverride">Platform override supplying the replacement tracks.</param>
        /// <returns>Resolved clip containing only the replacement platform tracks.</returns>
        AnimationClipAsset ResolveReplaceWholeClip(AnimationClipAsset clip, AnimationClipPlatformOverrideAsset platformOverride) {
            return new AnimationClipAsset {
                Id = clip.Id,
                Duration = clip.Duration,
                PositionTracks = ConvertPlatformPositionTracks(platformOverride.PositionTracks),
                PositionOffsetTracks = ConvertPlatformPositionOffsetTracks(platformOverride.PositionOffsetTracks),
                ScaleTracks = ConvertPlatformScaleTracks(platformOverride.ScaleTracks),
                RotationTracks = ConvertPlatformRotationTracks(platformOverride.RotationTracks),
                PlatformOverrides = Array.Empty<AnimationClipPlatformOverrideAsset>()
            };
        }

        /// <summary>
        /// Resolves one frame-override payload by merging base frames, targeted replacements, and inserted platform-only frames.
        /// </summary>
        /// <param name="clip">Base clip whose tracks provide the default timeline.</param>
        /// <param name="platformOverride">Platform payload containing targeted frame replacements and insertions.</param>
        /// <returns>Resolved clip with merged flat tracks and stripped editor-only override metadata.</returns>
        AnimationClipAsset ResolveOverrideFrames(AnimationClipAsset clip, AnimationClipPlatformOverrideAsset platformOverride) {
            return new AnimationClipAsset {
                Id = clip.Id,
                Duration = clip.Duration,
                PositionTracks = ResolveOverridePositionTracks(clip.PositionTracks, platformOverride.PositionTracks),
                PositionOffsetTracks = ResolveOverridePositionOffsetTracks(clip.PositionOffsetTracks, platformOverride.PositionOffsetTracks),
                ScaleTracks = ResolveOverrideScaleTracks(clip.ScaleTracks, platformOverride.ScaleTracks),
                RotationTracks = ResolveOverrideRotationTracks(clip.RotationTracks, platformOverride.RotationTracks),
                PlatformOverrides = Array.Empty<AnimationClipPlatformOverrideAsset>()
            };
        }

        /// <summary>
        /// Clones one array of absolute position tracks.
        /// </summary>
        /// <param name="tracks">Tracks to clone.</param>
        /// <param name="preserveFrameIds">True to keep editor-only frame identifiers; otherwise false.</param>
        /// <returns>Cloned track array.</returns>
        PositionKeyframeTrackAsset[] ClonePositionTracks(PositionKeyframeTrackAsset[] tracks, bool preserveFrameIds) {
            if (tracks == null || tracks.Length == 0) {
                return Array.Empty<PositionKeyframeTrackAsset>();
            }

            PositionKeyframeTrackAsset[] clonedTracks = new PositionKeyframeTrackAsset[tracks.Length];
            for (int index = 0; index < tracks.Length; index++) {
                PositionKeyframeTrackAsset sourceTrack = tracks[index];
                clonedTracks[index] = new PositionKeyframeTrackAsset {
                    Keyframes = ClonePositionKeyframes(sourceTrack?.Keyframes, preserveFrameIds)
                };
            }

            return clonedTracks;
        }

        /// <summary>
        /// Clones one array of additive position-offset tracks.
        /// </summary>
        /// <param name="tracks">Tracks to clone.</param>
        /// <param name="preserveFrameIds">True to keep editor-only frame identifiers; otherwise false.</param>
        /// <returns>Cloned track array.</returns>
        PositionOffsetKeyframeTrackAsset[] ClonePositionOffsetTracks(PositionOffsetKeyframeTrackAsset[] tracks, bool preserveFrameIds) {
            if (tracks == null || tracks.Length == 0) {
                return Array.Empty<PositionOffsetKeyframeTrackAsset>();
            }

            PositionOffsetKeyframeTrackAsset[] clonedTracks = new PositionOffsetKeyframeTrackAsset[tracks.Length];
            for (int index = 0; index < tracks.Length; index++) {
                PositionOffsetKeyframeTrackAsset sourceTrack = tracks[index];
                clonedTracks[index] = new PositionOffsetKeyframeTrackAsset {
                    Keyframes = ClonePositionKeyframes(sourceTrack?.Keyframes, preserveFrameIds)
                };
            }

            return clonedTracks;
        }

        /// <summary>
        /// Clones one array of scale tracks.
        /// </summary>
        /// <param name="tracks">Tracks to clone.</param>
        /// <param name="preserveFrameIds">True to keep editor-only frame identifiers; otherwise false.</param>
        /// <returns>Cloned track array.</returns>
        ScaleKeyframeTrackAsset[] CloneScaleTracks(ScaleKeyframeTrackAsset[] tracks, bool preserveFrameIds) {
            if (tracks == null || tracks.Length == 0) {
                return Array.Empty<ScaleKeyframeTrackAsset>();
            }

            ScaleKeyframeTrackAsset[] clonedTracks = new ScaleKeyframeTrackAsset[tracks.Length];
            for (int index = 0; index < tracks.Length; index++) {
                ScaleKeyframeTrackAsset sourceTrack = tracks[index];
                clonedTracks[index] = new ScaleKeyframeTrackAsset {
                    Keyframes = ClonePositionKeyframes(sourceTrack?.Keyframes, preserveFrameIds)
                };
            }

            return clonedTracks;
        }

        /// <summary>
        /// Clones one array of rotation tracks.
        /// </summary>
        /// <param name="tracks">Tracks to clone.</param>
        /// <param name="preserveFrameIds">True to keep editor-only frame identifiers; otherwise false.</param>
        /// <returns>Cloned track array.</returns>
        RotationKeyframeTrackAsset[] CloneRotationTracks(RotationKeyframeTrackAsset[] tracks, bool preserveFrameIds) {
            if (tracks == null || tracks.Length == 0) {
                return Array.Empty<RotationKeyframeTrackAsset>();
            }

            RotationKeyframeTrackAsset[] clonedTracks = new RotationKeyframeTrackAsset[tracks.Length];
            for (int index = 0; index < tracks.Length; index++) {
                RotationKeyframeTrackAsset sourceTrack = tracks[index];
                clonedTracks[index] = new RotationKeyframeTrackAsset {
                    Keyframes = CloneRotationKeyframes(sourceTrack?.Keyframes, preserveFrameIds)
                };
            }

            return clonedTracks;
        }

        /// <summary>
        /// Converts platform-authored position tracks into resolved runtime absolute-position tracks.
        /// </summary>
        /// <param name="tracks">Platform-authored position tracks.</param>
        /// <returns>Resolved track array.</returns>
        PositionKeyframeTrackAsset[] ConvertPlatformPositionTracks(PlatformPositionKeyframeTrackAsset[] tracks) {
            if (tracks == null || tracks.Length == 0) {
                return Array.Empty<PositionKeyframeTrackAsset>();
            }

            PositionKeyframeTrackAsset[] resolvedTracks = new PositionKeyframeTrackAsset[tracks.Length];
            for (int index = 0; index < tracks.Length; index++) {
                resolvedTracks[index] = new PositionKeyframeTrackAsset {
                    Keyframes = ClonePositionKeyframes(tracks[index]?.Keyframes, false)
                };
            }

            return resolvedTracks;
        }

        /// <summary>
        /// Converts platform-authored position-offset tracks into resolved runtime additive-position tracks.
        /// </summary>
        /// <param name="tracks">Platform-authored additive tracks.</param>
        /// <returns>Resolved track array.</returns>
        PositionOffsetKeyframeTrackAsset[] ConvertPlatformPositionOffsetTracks(PlatformPositionKeyframeTrackAsset[] tracks) {
            if (tracks == null || tracks.Length == 0) {
                return Array.Empty<PositionOffsetKeyframeTrackAsset>();
            }

            PositionOffsetKeyframeTrackAsset[] resolvedTracks = new PositionOffsetKeyframeTrackAsset[tracks.Length];
            for (int index = 0; index < tracks.Length; index++) {
                resolvedTracks[index] = new PositionOffsetKeyframeTrackAsset {
                    Keyframes = ClonePositionKeyframes(tracks[index]?.Keyframes, false)
                };
            }

            return resolvedTracks;
        }

        /// <summary>
        /// Converts platform-authored scale tracks into resolved runtime scale tracks.
        /// </summary>
        /// <param name="tracks">Platform-authored scale tracks.</param>
        /// <returns>Resolved track array.</returns>
        ScaleKeyframeTrackAsset[] ConvertPlatformScaleTracks(PlatformPositionKeyframeTrackAsset[] tracks) {
            if (tracks == null || tracks.Length == 0) {
                return Array.Empty<ScaleKeyframeTrackAsset>();
            }

            ScaleKeyframeTrackAsset[] resolvedTracks = new ScaleKeyframeTrackAsset[tracks.Length];
            for (int index = 0; index < tracks.Length; index++) {
                resolvedTracks[index] = new ScaleKeyframeTrackAsset {
                    Keyframes = ClonePositionKeyframes(tracks[index]?.Keyframes, false)
                };
            }

            return resolvedTracks;
        }

        /// <summary>
        /// Converts platform-authored rotation tracks into resolved runtime rotation tracks.
        /// </summary>
        /// <param name="tracks">Platform-authored rotation tracks.</param>
        /// <returns>Resolved track array.</returns>
        RotationKeyframeTrackAsset[] ConvertPlatformRotationTracks(PlatformRotationKeyframeTrackAsset[] tracks) {
            if (tracks == null || tracks.Length == 0) {
                return Array.Empty<RotationKeyframeTrackAsset>();
            }

            RotationKeyframeTrackAsset[] resolvedTracks = new RotationKeyframeTrackAsset[tracks.Length];
            for (int index = 0; index < tracks.Length; index++) {
                resolvedTracks[index] = new RotationKeyframeTrackAsset {
                    Keyframes = CloneRotationKeyframes(tracks[index]?.Keyframes, false)
                };
            }

            return resolvedTracks;
        }

        /// <summary>
        /// Resolves merged absolute-position tracks for override-frame mode.
        /// </summary>
        /// <param name="baseTracks">Base absolute-position tracks.</param>
        /// <param name="overrideTracks">Platform-authored override tracks.</param>
        /// <returns>Resolved track array.</returns>
        PositionKeyframeTrackAsset[] ResolveOverridePositionTracks(PositionKeyframeTrackAsset[] baseTracks, PlatformPositionKeyframeTrackAsset[] overrideTracks) {
            int trackCount = Math.Max(baseTracks?.Length ?? 0, overrideTracks?.Length ?? 0);
            if (trackCount == 0) {
                return Array.Empty<PositionKeyframeTrackAsset>();
            }

            PositionKeyframeTrackAsset[] resolvedTracks = new PositionKeyframeTrackAsset[trackCount];
            for (int index = 0; index < trackCount; index++) {
                PositionKeyframeAsset[] baseKeyframes = index < (baseTracks?.Length ?? 0) ? baseTracks[index]?.Keyframes : null;
                PositionKeyframeAsset[] overrideKeyframes = index < (overrideTracks?.Length ?? 0) ? overrideTracks[index]?.Keyframes : null;
                resolvedTracks[index] = new PositionKeyframeTrackAsset {
                    Keyframes = MergePositionKeyframes(baseKeyframes, overrideKeyframes)
                };
            }

            return resolvedTracks;
        }

        /// <summary>
        /// Resolves merged additive-position tracks for override-frame mode.
        /// </summary>
        /// <param name="baseTracks">Base additive-position tracks.</param>
        /// <param name="overrideTracks">Platform-authored override tracks.</param>
        /// <returns>Resolved track array.</returns>
        PositionOffsetKeyframeTrackAsset[] ResolveOverridePositionOffsetTracks(PositionOffsetKeyframeTrackAsset[] baseTracks, PlatformPositionKeyframeTrackAsset[] overrideTracks) {
            int trackCount = Math.Max(baseTracks?.Length ?? 0, overrideTracks?.Length ?? 0);
            if (trackCount == 0) {
                return Array.Empty<PositionOffsetKeyframeTrackAsset>();
            }

            PositionOffsetKeyframeTrackAsset[] resolvedTracks = new PositionOffsetKeyframeTrackAsset[trackCount];
            for (int index = 0; index < trackCount; index++) {
                PositionKeyframeAsset[] baseKeyframes = index < (baseTracks?.Length ?? 0) ? baseTracks[index]?.Keyframes : null;
                PositionKeyframeAsset[] overrideKeyframes = index < (overrideTracks?.Length ?? 0) ? overrideTracks[index]?.Keyframes : null;
                resolvedTracks[index] = new PositionOffsetKeyframeTrackAsset {
                    Keyframes = MergePositionKeyframes(baseKeyframes, overrideKeyframes)
                };
            }

            return resolvedTracks;
        }

        /// <summary>
        /// Resolves merged scale tracks for override-frame mode.
        /// </summary>
        /// <param name="baseTracks">Base scale tracks.</param>
        /// <param name="overrideTracks">Platform-authored override tracks.</param>
        /// <returns>Resolved track array.</returns>
        ScaleKeyframeTrackAsset[] ResolveOverrideScaleTracks(ScaleKeyframeTrackAsset[] baseTracks, PlatformPositionKeyframeTrackAsset[] overrideTracks) {
            int trackCount = Math.Max(baseTracks?.Length ?? 0, overrideTracks?.Length ?? 0);
            if (trackCount == 0) {
                return Array.Empty<ScaleKeyframeTrackAsset>();
            }

            ScaleKeyframeTrackAsset[] resolvedTracks = new ScaleKeyframeTrackAsset[trackCount];
            for (int index = 0; index < trackCount; index++) {
                PositionKeyframeAsset[] baseKeyframes = index < (baseTracks?.Length ?? 0) ? baseTracks[index]?.Keyframes : null;
                PositionKeyframeAsset[] overrideKeyframes = index < (overrideTracks?.Length ?? 0) ? overrideTracks[index]?.Keyframes : null;
                resolvedTracks[index] = new ScaleKeyframeTrackAsset {
                    Keyframes = MergePositionKeyframes(baseKeyframes, overrideKeyframes)
                };
            }

            return resolvedTracks;
        }

        /// <summary>
        /// Resolves merged rotation tracks for override-frame mode.
        /// </summary>
        /// <param name="baseTracks">Base rotation tracks.</param>
        /// <param name="overrideTracks">Platform-authored override tracks.</param>
        /// <returns>Resolved track array.</returns>
        RotationKeyframeTrackAsset[] ResolveOverrideRotationTracks(RotationKeyframeTrackAsset[] baseTracks, PlatformRotationKeyframeTrackAsset[] overrideTracks) {
            int trackCount = Math.Max(baseTracks?.Length ?? 0, overrideTracks?.Length ?? 0);
            if (trackCount == 0) {
                return Array.Empty<RotationKeyframeTrackAsset>();
            }

            RotationKeyframeTrackAsset[] resolvedTracks = new RotationKeyframeTrackAsset[trackCount];
            for (int index = 0; index < trackCount; index++) {
                RotationKeyframeAsset[] baseKeyframes = index < (baseTracks?.Length ?? 0) ? baseTracks[index]?.Keyframes : null;
                RotationKeyframeAsset[] overrideKeyframes = index < (overrideTracks?.Length ?? 0) ? overrideTracks[index]?.Keyframes : null;
                resolvedTracks[index] = new RotationKeyframeTrackAsset {
                    Keyframes = MergeRotationKeyframes(baseKeyframes, overrideKeyframes)
                };
            }

            return resolvedTracks;
        }

        /// <summary>
        /// Merges one position-style base timeline with one platform-authored override timeline.
        /// </summary>
        /// <param name="baseKeyframes">Base keyframes that provide the default timeline.</param>
        /// <param name="overrideKeyframes">Platform-authored replacements and insertions.</param>
        /// <returns>Resolved keyframe array with stripped editor-only frame identifiers.</returns>
        PositionKeyframeAsset[] MergePositionKeyframes(PositionKeyframeAsset[] baseKeyframes, PositionKeyframeAsset[] overrideKeyframes) {
            List<PositionKeyframeAsset> resolvedKeyframes = new List<PositionKeyframeAsset>();
            if (baseKeyframes != null) {
                for (int index = 0; index < baseKeyframes.Length; index++) {
                    resolvedKeyframes.Add(ClonePositionKeyframe(baseKeyframes[index], true));
                }
            }

            if (overrideKeyframes != null) {
                for (int index = 0; index < overrideKeyframes.Length; index++) {
                    PositionKeyframeAsset overrideKeyframe = overrideKeyframes[index];
                    if (!TryReplacePositionKeyframeByFrameId(resolvedKeyframes, overrideKeyframe)) {
                        InsertPositionKeyframeByTimestamp(resolvedKeyframes, ClonePositionKeyframe(overrideKeyframe, false));
                    }
                }
            }

            return ClonePositionKeyframes([.. resolvedKeyframes], false);
        }

        /// <summary>
        /// Merges one rotation base timeline with one platform-authored override timeline.
        /// </summary>
        /// <param name="baseKeyframes">Base keyframes that provide the default timeline.</param>
        /// <param name="overrideKeyframes">Platform-authored replacements and insertions.</param>
        /// <returns>Resolved keyframe array with stripped editor-only frame identifiers.</returns>
        RotationKeyframeAsset[] MergeRotationKeyframes(RotationKeyframeAsset[] baseKeyframes, RotationKeyframeAsset[] overrideKeyframes) {
            List<RotationKeyframeAsset> resolvedKeyframes = new List<RotationKeyframeAsset>();
            if (baseKeyframes != null) {
                for (int index = 0; index < baseKeyframes.Length; index++) {
                    resolvedKeyframes.Add(CloneRotationKeyframe(baseKeyframes[index], true));
                }
            }

            if (overrideKeyframes != null) {
                for (int index = 0; index < overrideKeyframes.Length; index++) {
                    RotationKeyframeAsset overrideKeyframe = overrideKeyframes[index];
                    if (!TryReplaceRotationKeyframeByFrameId(resolvedKeyframes, overrideKeyframe)) {
                        InsertRotationKeyframeByTimestamp(resolvedKeyframes, CloneRotationKeyframe(overrideKeyframe, false));
                    }
                }
            }

            return CloneRotationKeyframes([.. resolvedKeyframes], false);
        }

        /// <summary>
        /// Attempts to replace one resolved position-style keyframe by matching its editor-only frame identifier.
        /// </summary>
        /// <param name="resolvedKeyframes">Current resolved keyframe list.</param>
        /// <param name="overrideKeyframe">Platform-authored keyframe that may target one base frame.</param>
        /// <returns>True when a matching base frame was replaced; otherwise false.</returns>
        bool TryReplacePositionKeyframeByFrameId(List<PositionKeyframeAsset> resolvedKeyframes, PositionKeyframeAsset overrideKeyframe) {
            if (resolvedKeyframes == null || overrideKeyframe == null || string.IsNullOrWhiteSpace(overrideKeyframe.FrameId)) {
                return false;
            }

            for (int index = 0; index < resolvedKeyframes.Count; index++) {
                PositionKeyframeAsset currentKeyframe = resolvedKeyframes[index];
                if (!string.Equals(currentKeyframe.FrameId, overrideKeyframe.FrameId, StringComparison.Ordinal)) {
                    continue;
                }

                resolvedKeyframes[index] = ClonePositionKeyframe(overrideKeyframe, false);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to replace one resolved rotation keyframe by matching its editor-only frame identifier.
        /// </summary>
        /// <param name="resolvedKeyframes">Current resolved keyframe list.</param>
        /// <param name="overrideKeyframe">Platform-authored keyframe that may target one base frame.</param>
        /// <returns>True when a matching base frame was replaced; otherwise false.</returns>
        bool TryReplaceRotationKeyframeByFrameId(List<RotationKeyframeAsset> resolvedKeyframes, RotationKeyframeAsset overrideKeyframe) {
            if (resolvedKeyframes == null || overrideKeyframe == null || string.IsNullOrWhiteSpace(overrideKeyframe.FrameId)) {
                return false;
            }

            for (int index = 0; index < resolvedKeyframes.Count; index++) {
                RotationKeyframeAsset currentKeyframe = resolvedKeyframes[index];
                if (!string.Equals(currentKeyframe.FrameId, overrideKeyframe.FrameId, StringComparison.Ordinal)) {
                    continue;
                }

                resolvedKeyframes[index] = CloneRotationKeyframe(overrideKeyframe, false);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Inserts one position-style keyframe into the resolved list after all entries whose timestamp is less than or equal to the incoming frame.
        /// </summary>
        /// <param name="resolvedKeyframes">Current resolved keyframe list.</param>
        /// <param name="insertedKeyframe">Platform-authored inserted keyframe.</param>
        void InsertPositionKeyframeByTimestamp(List<PositionKeyframeAsset> resolvedKeyframes, PositionKeyframeAsset insertedKeyframe) {
            int insertIndex = resolvedKeyframes.Count;
            for (int index = 0; index < resolvedKeyframes.Count; index++) {
                if (resolvedKeyframes[index].Time <= insertedKeyframe.Time) {
                    continue;
                }

                insertIndex = index;
                break;
            }

            resolvedKeyframes.Insert(insertIndex, insertedKeyframe);
        }

        /// <summary>
        /// Inserts one rotation keyframe into the resolved list after all entries whose timestamp is less than or equal to the incoming frame.
        /// </summary>
        /// <param name="resolvedKeyframes">Current resolved keyframe list.</param>
        /// <param name="insertedKeyframe">Platform-authored inserted keyframe.</param>
        void InsertRotationKeyframeByTimestamp(List<RotationKeyframeAsset> resolvedKeyframes, RotationKeyframeAsset insertedKeyframe) {
            int insertIndex = resolvedKeyframes.Count;
            for (int index = 0; index < resolvedKeyframes.Count; index++) {
                if (resolvedKeyframes[index].Time <= insertedKeyframe.Time) {
                    continue;
                }

                insertIndex = index;
                break;
            }

            resolvedKeyframes.Insert(insertIndex, insertedKeyframe);
        }

        /// <summary>
        /// Clones one position-style keyframe.
        /// </summary>
        /// <param name="keyframe">Source keyframe to clone.</param>
        /// <param name="preserveFrameId">True to keep the editor-only frame identifier; otherwise false.</param>
        /// <returns>Cloned keyframe, or null when the source keyframe is null.</returns>
        PositionKeyframeAsset ClonePositionKeyframe(PositionKeyframeAsset keyframe, bool preserveFrameId) {
            if (keyframe == null) {
                return null;
            }

            return new PositionKeyframeAsset(keyframe.Time, keyframe.Value, keyframe.InterpolationMode) {
                FrameId = preserveFrameId ? keyframe.FrameId ?? string.Empty : string.Empty
            };
        }

        /// <summary>
        /// Clones one position-style keyframe array.
        /// </summary>
        /// <param name="keyframes">Source keyframe array to clone.</param>
        /// <param name="preserveFrameIds">True to keep editor-only frame identifiers; otherwise false.</param>
        /// <returns>Cloned keyframe array.</returns>
        PositionKeyframeAsset[] ClonePositionKeyframes(PositionKeyframeAsset[] keyframes, bool preserveFrameIds) {
            if (keyframes == null || keyframes.Length == 0) {
                return Array.Empty<PositionKeyframeAsset>();
            }

            PositionKeyframeAsset[] clonedKeyframes = new PositionKeyframeAsset[keyframes.Length];
            for (int index = 0; index < keyframes.Length; index++) {
                clonedKeyframes[index] = ClonePositionKeyframe(keyframes[index], preserveFrameIds);
            }

            return clonedKeyframes;
        }

        /// <summary>
        /// Clones one rotation keyframe.
        /// </summary>
        /// <param name="keyframe">Source keyframe to clone.</param>
        /// <param name="preserveFrameId">True to keep the editor-only frame identifier; otherwise false.</param>
        /// <returns>Cloned keyframe, or null when the source keyframe is null.</returns>
        RotationKeyframeAsset CloneRotationKeyframe(RotationKeyframeAsset keyframe, bool preserveFrameId) {
            if (keyframe == null) {
                return null;
            }

            return new RotationKeyframeAsset(keyframe.Time, keyframe.Value, keyframe.InterpolationMode) {
                FrameId = preserveFrameId ? keyframe.FrameId ?? string.Empty : string.Empty
            };
        }

        /// <summary>
        /// Clones one rotation keyframe array.
        /// </summary>
        /// <param name="keyframes">Source keyframe array to clone.</param>
        /// <param name="preserveFrameIds">True to keep editor-only frame identifiers; otherwise false.</param>
        /// <returns>Cloned keyframe array.</returns>
        RotationKeyframeAsset[] CloneRotationKeyframes(RotationKeyframeAsset[] keyframes, bool preserveFrameIds) {
            if (keyframes == null || keyframes.Length == 0) {
                return Array.Empty<RotationKeyframeAsset>();
            }

            RotationKeyframeAsset[] clonedKeyframes = new RotationKeyframeAsset[keyframes.Length];
            for (int index = 0; index < keyframes.Length; index++) {
                clonedKeyframes[index] = CloneRotationKeyframe(keyframes[index], preserveFrameIds);
            }

            return clonedKeyframes;
        }
    }
}
