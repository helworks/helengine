namespace helengine.timeline.runtime {
    /// <summary>
    /// Combines the transform tracks of one player into local transforms. Every evaluated frame the player clears it,
    /// writes the value of each transform track that has one, and applies the result: per component, an absolute value
    /// replaces the bound component (otherwise the component the entity had when the player bound it is used) and offsets
    /// are added (position, rotation degrees) or multiplied (scale). Only the groups (position, rotation, scale) some track
    /// drives are written, and they are written every frame, so the result depends only on the tick. All storage is
    /// allocated once at bind time.
    /// </summary>
    sealed class TimelineTransformComposer {
        /// <summary>
        /// Number of scalar transform components per slot.
        /// </summary>
        const int ComponentsPerSlot = 9;

        /// <summary>
        /// Degrees-to-radians factor for Euler rotation channels.
        /// </summary>
        const double DegreesToRadians = Math.PI / 180.0;

        /// <summary>
        /// Slot entities, borrowed from the player.
        /// </summary>
        readonly Entity[] Entities;

        /// <summary>
        /// Per slot and group (slot * 3 + group, groups position, rotation, scale), whether any track drives the group.
        /// </summary>
        [NativeOwnedMember]
        bool[] DrivenGroups;

        /// <summary>
        /// Local positions captured at bind time.
        /// </summary>
        [NativeOwnedMember]
        float3[] BasePositions;

        /// <summary>
        /// Local scales captured at bind time.
        /// </summary>
        [NativeOwnedMember]
        float3[] BaseScales;

        /// <summary>
        /// Local orientations captured at bind time.
        /// </summary>
        [NativeOwnedMember]
        float4[] BaseOrientations;

        /// <summary>
        /// Bound orientations as Euler degrees (slot * 3 + axis), used when only some rotation axes are driven.
        /// </summary>
        [NativeOwnedMember]
        double[] BaseEulerDegrees;

        /// <summary>
        /// Absolute values written this frame (slot * 9 + channel).
        /// </summary>
        [NativeOwnedMember]
        double[] AbsoluteValues;

        /// <summary>
        /// Whether an absolute value was written this frame (slot * 9 + channel).
        /// </summary>
        [NativeOwnedMember]
        bool[] HasAbsolute;

        /// <summary>
        /// Accumulated offsets this frame (slot * 9 + channel); 0 for position and rotation, 1 for scale when none.
        /// </summary>
        [NativeOwnedMember]
        double[] OffsetValues;

        /// <summary>
        /// Whether an offset was written this frame (slot * 9 + channel).
        /// </summary>
        [NativeOwnedMember]
        bool[] HasOffset;

        /// <summary>
        /// Captures the bound transforms and marks the groups the timeline's transform tracks drive.
        /// </summary>
        /// <param name="entities">Slot entities in slot order; entries for slots without transform tracks may be null.</param>
        /// <param name="timeline">Cooked timeline being bound.</param>
        internal TimelineTransformComposer([NativeRetainsBorrow] Entity[] entities, CookedTimelineAsset timeline) {
            Entities = entities;
            int slotCount = entities.Length;
            DrivenGroups = new bool[slotCount * 3];
            BasePositions = new float3[slotCount];
            BaseScales = new float3[slotCount];
            BaseOrientations = new float4[slotCount];
            BaseEulerDegrees = new double[slotCount * 3];
            AbsoluteValues = new double[slotCount * ComponentsPerSlot];
            HasAbsolute = new bool[slotCount * ComponentsPerSlot];
            OffsetValues = new double[slotCount * ComponentsPerSlot];
            HasOffset = new bool[slotCount * ComponentsPerSlot];
            for (int index = 0; index < timeline.Tracks.Length; index++) {
                CookedTimelineTrack track = timeline.Tracks[index];
                if (track.Kind == CookedTimelineTrackKind.Transform) {
                    DrivenGroups[track.SlotIndex * 3 + track.ChannelIndex / 3] = true;
                }
            }

            for (int slot = 0; slot < slotCount; slot++) {
                Entity entity = entities[slot];
                if (entity == null) {
                    continue;
                }

                BasePositions[slot] = entity.LocalPosition;
                BaseScales[slot] = entity.LocalScale;
                BaseOrientations[slot] = entity.LocalOrientation;
                double pitch;
                double yaw;
                double roll;
                entity.LocalOrientation.ToEulerDegrees(out pitch, out yaw, out roll);
                BaseEulerDegrees[slot * 3] = pitch;
                BaseEulerDegrees[slot * 3 + 1] = yaw;
                BaseEulerDegrees[slot * 3 + 2] = roll;
            }
        }

        /// <summary>
        /// Clears the values written by the previous frame.
        /// </summary>
        internal void BeginFrame() {
            for (int index = 0; index < HasAbsolute.Length; index++) {
                HasAbsolute[index] = false;
                HasOffset[index] = false;
                OffsetValues[index] = index % ComponentsPerSlot >= (int)CookedTimelineTransformChannel.ScaleX ? 1.0 : 0.0;
            }
        }

        /// <summary>
        /// Records the value of one transform track for this frame.
        /// </summary>
        /// <param name="slot">Slot index of the track.</param>
        /// <param name="channel">Transform channel of the track.</param>
        /// <param name="mode">How the value combines with the bound transform.</param>
        /// <param name="value">Track value at the evaluated tick.</param>
        internal void Write(int slot, int channel, CookedTimelineTransformMode mode, double value) {
            int index = slot * ComponentsPerSlot + channel;
            if (mode == CookedTimelineTransformMode.Absolute) {
                AbsoluteValues[index] = value;
                HasAbsolute[index] = true;
            } else if (channel >= (int)CookedTimelineTransformChannel.ScaleX) {
                OffsetValues[index] *= value;
                HasOffset[index] = true;
            } else {
                OffsetValues[index] += value;
                HasOffset[index] = true;
            }
        }

        /// <summary>
        /// Writes the composed local transform of every driven group to its entity.
        /// </summary>
        internal void Apply() {
            for (int slot = 0; slot < Entities.Length; slot++) {
                Entity entity = Entities[slot];
                if (entity == null) {
                    continue;
                }

                int baseIndex = slot * ComponentsPerSlot;
                if (DrivenGroups[slot * 3]) {
                    float3 basePosition = BasePositions[slot];
                    entity.LocalPosition = new float3(
                        (float)(Resolve(baseIndex, basePosition.X) + OffsetValues[baseIndex]),
                        (float)(Resolve(baseIndex + 1, basePosition.Y) + OffsetValues[baseIndex + 1]),
                        (float)(Resolve(baseIndex + 2, basePosition.Z) + OffsetValues[baseIndex + 2]));
                }
                if (DrivenGroups[slot * 3 + 1]) {
                    entity.LocalOrientation = ComposeOrientation(slot, baseIndex + (int)CookedTimelineTransformChannel.RotationX);
                }
                if (DrivenGroups[slot * 3 + 2]) {
                    float3 baseScale = BaseScales[slot];
                    int scaleIndex = baseIndex + (int)CookedTimelineTransformChannel.ScaleX;
                    entity.LocalScale = new float3(
                        (float)(Resolve(scaleIndex, baseScale.X) * OffsetValues[scaleIndex]),
                        (float)(Resolve(scaleIndex + 1, baseScale.Y) * OffsetValues[scaleIndex + 1]),
                        (float)(Resolve(scaleIndex + 2, baseScale.Z) * OffsetValues[scaleIndex + 2]));
                }
            }
        }

        /// <summary>
        /// Releases the buffers allocated at bind time.
        /// </summary>
        internal void Release() {
            NativeOwnership.Release(ref DrivenGroups);
            NativeOwnership.Release(ref BasePositions);
            NativeOwnership.Release(ref BaseScales);
            NativeOwnership.Release(ref BaseOrientations);
            NativeOwnership.Release(ref BaseEulerDegrees);
            NativeOwnership.Release(ref AbsoluteValues);
            NativeOwnership.Release(ref HasAbsolute);
            NativeOwnership.Release(ref OffsetValues);
            NativeOwnership.Release(ref HasOffset);
        }

        /// <summary>
        /// Returns the absolute value written for a component this frame, or the bound value when none was written.
        /// </summary>
        /// <param name="index">Component index (slot * 9 + channel).</param>
        /// <param name="boundValue">Component value captured at bind time.</param>
        /// <returns>The value the offsets apply to.</returns>
        double Resolve(int index, double boundValue) {
            return HasAbsolute[index] ? AbsoluteValues[index] : boundValue;
        }

        /// <summary>
        /// Builds the orientation of one slot. Without any rotation value this frame the bound orientation is returned
        /// unchanged; otherwise the Euler angles (absolute or bound, plus offsets) are converted to a quaternion.
        /// </summary>
        /// <param name="slot">Slot index.</param>
        /// <param name="rotationIndex">Index of the slot's rotation X component.</param>
        /// <returns>The local orientation to apply.</returns>
        float4 ComposeOrientation(int slot, int rotationIndex) {
            bool anyValue = false;
            for (int axis = 0; axis < 3; axis++) {
                if (HasAbsolute[rotationIndex + axis] || HasOffset[rotationIndex + axis]) {
                    anyValue = true;
                }
            }
            if (!anyValue) {
                return BaseOrientations[slot];
            }

            double pitch = Resolve(rotationIndex, BaseEulerDegrees[slot * 3]) + OffsetValues[rotationIndex];
            double yaw = Resolve(rotationIndex + 1, BaseEulerDegrees[slot * 3 + 1]) + OffsetValues[rotationIndex + 1];
            double roll = Resolve(rotationIndex + 2, BaseEulerDegrees[slot * 3 + 2]) + OffsetValues[rotationIndex + 2];
            float4 orientation;
            float4.CreateFromYawPitchRoll((float)(yaw * DegreesToRadians), (float)(pitch * DegreesToRadians), (float)(roll * DegreesToRadians), out orientation);
            return orientation;
        }
    }
}
