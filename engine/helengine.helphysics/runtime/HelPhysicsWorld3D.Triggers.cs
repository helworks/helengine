namespace helengine {
    /// <summary>Owns fixed trigger overlap state and owner checked scene mutations for HelPhysicsWorld3D.</summary>
    public sealed partial class HelPhysicsWorld3D {
        /// <summary>Stores current directional trigger pairs in fixed storage.</summary>
        HelPhysicsTriggerPairKey3D[] CurrentTriggerPairs;
        /// <summary>Stores the prior completed directional trigger pair batch.</summary>
        HelPhysicsTriggerPairKey3D[] PreviousTriggerPairs;
        /// <summary>Stores published trigger events in a valid prefix.</summary>
        List<HelPhysicsTriggerEvent3D> TriggerEventsValue;
        /// <summary>Provides the reusable public view over published trigger events.</summary>
        IReadOnlyList<HelPhysicsTriggerEvent3D> TriggerEventsView;
        /// <summary>Stores the current directional pair count.</summary>
        int CurrentTriggerPairCount;
        /// <summary>Stores the prior directional pair count.</summary>
        int PreviousTriggerPairCount;
        /// <summary>Stores the published event count.</summary>
        int TriggerEventCountValue;
        /// <summary>Stores the fixed maximum number of trigger events published per step.</summary>
        int TriggerEventCapacity;

        /// <summary>Allocates fixed trigger storage sized from the world's candidate profile.</summary>
        void InitializeTriggerStorage() {
            CurrentTriggerPairs = new HelPhysicsTriggerPairKey3D[Settings.CandidatePairCapacity * 2];
            PreviousTriggerPairs = new HelPhysicsTriggerPairKey3D[Settings.CandidatePairCapacity * 2];
            TriggerEventCapacity = Settings.CandidatePairCapacity * 4;
            TriggerEventsValue = new List<HelPhysicsTriggerEvent3D>(TriggerEventCapacity);
            TriggerEventsView = TriggerEventsValue.AsReadOnly();
        }

        /// <summary>Gets valid trigger transitions from the most recent completed step.</summary>
        public IReadOnlyList<HelPhysicsTriggerEvent3D> TriggerEvents =>
            TriggerEventsView;

        /// <summary>Gets the number of valid trigger transitions.</summary>
        public int TriggerEventCount => TriggerEventCountValue;

        /// <summary>Begins one trigger transition batch.</summary>
        void BeginTriggerStep() {
            PreviousTriggerPairCount = CurrentTriggerPairCount;
            for (int index = 0; index < CurrentTriggerPairCount; index++) {
                PreviousTriggerPairs[index] = CurrentTriggerPairs[index];
            }

            CurrentTriggerPairCount = 0;
            TriggerEventCountValue = 0;
            TriggerEventsValue.Clear();
        }

        /// <summary>Records directional trigger sides for one overlapping candidate.</summary>
        void RecordTriggerOverlap(
            int firstBodyIndex,
            int secondBodyIndex,
            in HelPhysicsBodyColdState3D firstCold,
            in HelPhysicsBodyColdState3D secondCold) {
            HelPhysicsBodyHandle3D first = Bodies.GetRequiredHandleByIndex(firstBodyIndex);
            HelPhysicsBodyHandle3D second = Bodies.GetRequiredHandleByIndex(secondBodyIndex);
            if (firstCold.IsTrigger) {
                AppendTriggerPair(new HelPhysicsTriggerPairKey3D(first, second));
            }

            if (secondCold.IsTrigger) {
                AppendTriggerPair(new HelPhysicsTriggerPairKey3D(second, first));
            }
        }

        /// <summary>Appends one unique current trigger pair.</summary>
        void AppendTriggerPair(HelPhysicsTriggerPairKey3D pair) {
            for (int index = 0; index < CurrentTriggerPairCount; index++) {
                if (CurrentTriggerPairs[index] == pair) {
                    return;
                }
            }

            if (CurrentTriggerPairCount == CurrentTriggerPairs.Length) {
                throw new HelPhysicsCapacityExceededException("trigger pair", CurrentTriggerPairs.Length);
            }

            CurrentTriggerPairs[CurrentTriggerPairCount++] = pair;
        }

        /// <summary>Publishes deterministic enter, stay, and exit events.</summary>
        void PublishTriggerEvents() {
            SortTriggerPairs(CurrentTriggerPairs, CurrentTriggerPairCount);
            SortTriggerPairs(PreviousTriggerPairs, PreviousTriggerPairCount);
            int current = 0;
            int previous = 0;
            while (current < CurrentTriggerPairCount || previous < PreviousTriggerPairCount) {
                if (current == CurrentTriggerPairCount) {
                    AppendTriggerEvent(
                        HelPhysicsTriggerEventKind3D.Exit,
                        PreviousTriggerPairs[previous++]);
                } else if (previous == PreviousTriggerPairCount) {
                    AppendTriggerEvent(
                        HelPhysicsTriggerEventKind3D.Enter,
                        CurrentTriggerPairs[current++]);
                } else {
                    int order = CompareTriggerPairs(
                        CurrentTriggerPairs[current],
                        PreviousTriggerPairs[previous]);
                    if (order < 0) {
                        AppendTriggerEvent(
                            HelPhysicsTriggerEventKind3D.Enter,
                            CurrentTriggerPairs[current++]);
                    } else if (order > 0) {
                        AppendTriggerEvent(
                            HelPhysicsTriggerEventKind3D.Exit,
                            PreviousTriggerPairs[previous++]);
                    } else {
                        AppendTriggerEvent(
                            HelPhysicsTriggerEventKind3D.Stay,
                            CurrentTriggerPairs[current++]);
                        previous++;
                    }
                }
            }
        }

        /// <summary>Appends one transition into the valid fixed event prefix.</summary>
        void AppendTriggerEvent(HelPhysicsTriggerEventKind3D kind, HelPhysicsTriggerPairKey3D pair) {
            if (TriggerEventCountValue == TriggerEventCapacity) {
                throw new HelPhysicsCapacityExceededException("trigger event", TriggerEventCapacity);
            }

            TriggerEventsValue.Add(new HelPhysicsTriggerEvent3D(
                kind,
                CreatePublicHandle(pair.TriggerBody),
                CreatePublicHandle(pair.OtherBody)));
            TriggerEventCountValue++;
        }

        /// <summary>Sorts a trigger pair prefix.</summary>
        static void SortTriggerPairs(HelPhysicsTriggerPairKey3D[] pairs, int count) {
            for (int index = 1; index < count; index++) {
                HelPhysicsTriggerPairKey3D candidate = pairs[index];
                int insertion = index - 1;
                while (insertion >= 0 && CompareTriggerPairs(candidate, pairs[insertion]) < 0) {
                    pairs[insertion + 1] = pairs[insertion];
                    insertion--;
                }

                pairs[insertion + 1] = candidate;
            }
        }

        /// <summary>Compares trigger pairs by stable generational identity.</summary>
        static int CompareTriggerPairs(
            HelPhysicsTriggerPairKey3D first,
            HelPhysicsTriggerPairKey3D second) {
            int result = first.TriggerBody.Index.CompareTo(second.TriggerBody.Index);
            if (result == 0) {
                result = first.TriggerBody.Generation.CompareTo(second.TriggerBody.Generation);
            }

            if (result == 0) {
                result = first.OtherBody.Index.CompareTo(second.OtherBody.Index);
            }

            if (result == 0) {
                result = first.OtherBody.Generation.CompareTo(second.OtherBody.Generation);
            }

            return result;
        }

        /// <summary>Sets dynamic authored pose and velocity for the exact scene owner.</summary>
        internal void SetDynamicStateForSceneBinder(
            HelPhysicsSceneBinder3D owner,
            HelPhysicsBodyHandle3D handle,
            PhysicsVector3 position,
            PhysicsQuaternion orientation,
            PhysicsVector3 linearVelocity,
            PhysicsVector3 angularVelocity) {
            ValidateSceneBinderOwner(owner);
            ThrowIfFaulted();
            HelPhysicsBodyHandle3D internalHandle = GetRequiredDynamicInputHandle(handle);
            ValidateNormalizedOrientation(orientation);
            linearVelocity.LengthSquared();
            angularVelocity.LengthSquared();
            ref HelPhysicsBodyState3D state = ref Bodies.GetRequiredState(internalHandle);
            state.Position = position;
            state.Orientation = orientation;
            ManifoldCache.RemoveBody(internalHandle.Index);
            state.LinearVelocity = linearVelocity;
            state.AngularVelocity = angularVelocity;
            state.LowMotionStepCount = 0;
            ProxyIsDirty[internalHandle.Index] = true;
            if (BodyIsActive[internalHandle.Index]) {
                state.IsAwake = true;
                IslandSleeper.WakeForExplicitForce(internalHandle.Index, Bodies, IslandBuilder);
            }
        }

        /// <summary>Sets dynamic authored velocity for the exact scene owner.</summary>
        internal void SetDynamicVelocityForSceneBinder(
            HelPhysicsSceneBinder3D owner,
            HelPhysicsBodyHandle3D handle,
            PhysicsVector3 linearVelocity,
            PhysicsVector3 angularVelocity) {
            ValidateSceneBinderOwner(owner);
            ThrowIfFaulted();
            HelPhysicsBodyHandle3D internalHandle = GetRequiredDynamicInputHandle(handle);
            linearVelocity.LengthSquared();
            angularVelocity.LengthSquared();
            ref HelPhysicsBodyState3D state = ref Bodies.GetRequiredState(internalHandle);
            state.LinearVelocity = linearVelocity;
            state.AngularVelocity = angularVelocity;
            state.LowMotionStepCount = 0;
            ProxyIsDirty[internalHandle.Index] = true;
            if (BodyIsActive[internalHandle.Index]) {
                state.IsAwake = true;
                IslandSleeper.WakeForExplicitForce(internalHandle.Index, Bodies, IslandBuilder);
            }
        }

        /// <summary>Releases all bodies owned by a scene without stepping.</summary>
        internal void DisposeForSceneBinder(HelPhysicsSceneBinder3D owner) {
            ValidateSceneBinderOwner(owner);
            DeferredCommandCount = 0;
            DeferredRemovalCount = 0;
            for (int index = 0; index < Bodies.Capacity; index++) {
                if (!Bodies.IsOccupied(index)) {
                    continue;
                }

                HelPhysicsBodyHandle3D handle = Bodies.GetRequiredHandleByIndex(index);
                ref HelPhysicsBodyColdState3D cold = ref Bodies.GetRequiredColdState(handle);
                if (ProxyIsRegistered[index]) {
                    Broadphase.RemoveProxy(index);
                }

                ManifoldCache.RemoveBody(index);
                Shapes.Release(cold.ShapeHandle);
                Bodies.Release(handle);
                BodyIsActive[index] = false;
                ProxyIsDirty[index] = false;
                ProxyIsRegistered[index] = false;
            }

            CurrentTriggerPairCount = 0;
            PreviousTriggerPairCount = 0;
            TriggerEventCountValue = 0;
            TriggerEventsValue.Clear();
            SceneBinderOwner = null;
        }
        /// <summary>
        /// Determines whether either participant is a trigger and must bypass physical wake routing.
        /// </summary>
        bool IsTriggerCandidate(HelPhysicsCandidatePair3D candidate) {
            return Bodies.GetRequiredColdStateByIndex(candidate.FirstBodyIndex).IsTrigger ||
                Bodies.GetRequiredColdStateByIndex(candidate.SecondBodyIndex).IsTrigger;
        }
    }
}
