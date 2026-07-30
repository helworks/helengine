namespace helengine {
    /// <summary>
    /// Evaluates aggressive whole-island sleeping and propagates explicit contact-driven wakes through fixed indexed islands.
    /// </summary>
    sealed class HelPhysicsIslandSleeper3D {
        /// <summary>
        /// Stores the minimum supported fixed body capacity.
        /// </summary>
        const int MinimumCapacity = 1;

        /// <summary>
        /// Stores the largest body capacity representable by the body-handle index contract.
        /// </summary>
        const int MaximumBodyCapacity = 65534;

        /// <summary>
        /// Marks body slots belonging to an island that received a wake condition during the current fixed step.
        /// </summary>
        readonly bool[] WakeOccurredThisStep;

        /// <summary>
        /// Stores one initiating reason for every actual asleep-to-awake island transition in the current step.
        /// </summary>
        readonly HelPhysicsWakeReason3D[] WakeEventReasons;

        /// <summary>
        /// Stores how many leading wake-reason entries contain current-step events.
        /// </summary>
        int WakeEventCountValue;

        /// <summary>
        /// Stores current-step asleep-to-awake island transitions initiated by explicit force.
        /// </summary>
        int ExplicitForceWakeCount;

        /// <summary>
        /// Stores current-step asleep-to-awake island transitions initiated by explicit impulse.
        /// </summary>
        int ExplicitImpulseWakeCount;

        /// <summary>
        /// Stores current-step asleep-to-awake island transitions initiated by meaningful new candidate contact.
        /// </summary>
        int NewCandidateContactWakeCount;

        /// <summary>
        /// Stores current-step asleep-to-awake island transitions initiated by moving kinematic contact.
        /// </summary>
        int MovingKinematicContactWakeCount;

        /// <summary>
        /// Initializes fixed transient wake flags and diagnostic event storage for one exact body-pool capacity.
        /// </summary>
        /// <param name="bodyCapacity">Fixed body-slot count addressed by wake and sleep loops.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when body capacity cannot be represented by valid body handles.</exception>
        public HelPhysicsIslandSleeper3D(int bodyCapacity) {
            if (bodyCapacity < MinimumCapacity || bodyCapacity > MaximumBodyCapacity) {
                throw new ArgumentOutOfRangeException(nameof(bodyCapacity), "Island sleeper body capacity must be between 1 and 65,534 inclusive.");
            }

            WakeOccurredThisStep = new bool[bodyCapacity];
            WakeEventReasons = new HelPhysicsWakeReason3D[bodyCapacity];
        }

        /// <summary>
        /// Gets the fixed body-slot count addressed by transient wake flags.
        /// </summary>
        public int BodyCapacity => WakeOccurredThisStep.Length;

        /// <summary>
        /// Gets the number of allocation-free indexed wake events recorded in the current step.
        /// </summary>
        public int WakeEventCount => WakeEventCountValue;

        /// <summary>
        /// Clears every transient wake flag and current-step diagnostic counter before wake processing begins.
        /// </summary>
        public void BeginStep() {
            for (int bodyIndex = 0; bodyIndex < WakeOccurredThisStep.Length; bodyIndex++) {
                WakeOccurredThisStep[bodyIndex] = false;
            }

            WakeEventCountValue = 0;
            ExplicitForceWakeCount = 0;
            ExplicitImpulseWakeCount = 0;
            NewCandidateContactWakeCount = 0;
            MovingKinematicContactWakeCount = 0;
        }

        /// <summary>
        /// Evaluates every published island, synchronizes quiet counters, and atomically sleeps qualifying members.
        /// </summary>
        /// <param name="bodies">Fixed body pool containing every published dynamic member.</param>
        /// <param name="islands">Current deterministic island ranges and lookup.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required pool or builder is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when fixed capacities do not match this sleeper.</exception>
        /// <exception cref="InvalidOperationException">Thrown when published membership or cold sleep settings are invalid.</exception>
        public void EvaluateSleep(HelPhysicsBodyPool3D bodies, HelPhysicsIslandBuilder3D islands) {
            ValidateFixedInputs(bodies, islands);
            ValidatePublishedIslands(bodies, islands);

            for (int islandIndex = 0; islandIndex < islands.IslandCount; islandIndex++) {
                EvaluateIsland(bodies, islands, islandIndex);
            }
        }

        /// <summary>
        /// Wakes and resets the complete published dynamic island targeted by an explicitly applied force.
        /// </summary>
        /// <param name="bodyIndex">Occupied dynamic body receiving the external force.</param>
        /// <param name="bodies">Fixed body pool containing the target.</param>
        /// <param name="islands">Prior or current published dynamic islands used for propagation.</param>
        public void WakeForExplicitForce(
            int bodyIndex,
            HelPhysicsBodyPool3D bodies,
            HelPhysicsIslandBuilder3D islands) {
            ValidateWakeTarget(bodyIndex, bodies, islands);
            WakeExplicitInput(bodyIndex, HelPhysicsWakeReason3D.ExplicitForce, bodies, islands);
        }

        /// <summary>
        /// Wakes and resets the complete published dynamic island targeted by an explicitly applied impulse.
        /// </summary>
        /// <param name="bodyIndex">Occupied dynamic body receiving the external impulse.</param>
        /// <param name="bodies">Fixed body pool containing the target.</param>
        /// <param name="islands">Prior or current published dynamic islands used for propagation.</param>
        public void WakeForExplicitImpulse(
            int bodyIndex,
            HelPhysicsBodyPool3D bodies,
            HelPhysicsIslandBuilder3D islands) {
            ValidateWakeTarget(bodyIndex, bodies, islands);
            WakeExplicitInput(bodyIndex, HelPhysicsWakeReason3D.ExplicitImpulse, bodies, islands);
        }

        /// <summary>
        /// Wakes every dynamic participant's prior island when a caller identifies a meaningful new candidate touching a sleeping body.
        /// </summary>
        /// <param name="candidate">Canonical new broadphase candidate selected by the world or manifold lifecycle.</param>
        /// <param name="bodies">Fixed body pool containing both candidate participants.</param>
        /// <param name="islands">Prior published islands used before current manifolds are rebuilt.</param>
        public void WakeForNewCandidateContact(
            HelPhysicsCandidatePair3D candidate,
            HelPhysicsBodyPool3D bodies,
            HelPhysicsIslandBuilder3D islands) {
            ValidateCandidate(candidate, bodies, islands);
            bool touchesSleepingDynamic = IsSleepingDynamic(candidate.FirstBodyIndex, bodies) ||
                IsSleepingDynamic(candidate.SecondBodyIndex, bodies);
            if (!touchesSleepingDynamic) {
                return;
            }

            ValidateDynamicParticipantLookup(candidate.FirstBodyIndex, bodies, islands);
            ValidateDynamicParticipantLookup(candidate.SecondBodyIndex, bodies, islands);
            WakeDynamicParticipant(
                candidate.FirstBodyIndex,
                HelPhysicsWakeReason3D.NewCandidateContact,
                bodies,
                islands);
            WakeDynamicParticipant(
                candidate.SecondBodyIndex,
                HelPhysicsWakeReason3D.NewCandidateContact,
                bodies,
                islands);
        }

        /// <summary>
        /// Wakes the dynamic participant's current island when an active manifold touches a kinematic body with nonzero velocity.
        /// </summary>
        /// <param name="pair">Canonical body pair owning the active manifold.</param>
        /// <param name="manifold">Active one-through-four-contact manifold proving current contact.</param>
        /// <param name="bodies">Fixed body pool containing both contact participants.</param>
        /// <param name="islands">Prior or current published islands used for propagation.</param>
        public void WakeForMovingKinematicContact(
            HelPhysicsPairKey3D pair,
            in HelPhysicsContactManifold3D manifold,
            HelPhysicsBodyPool3D bodies,
            HelPhysicsIslandBuilder3D islands) {
            ValidateContactPair(pair, in manifold, bodies, islands);
            ref HelPhysicsBodyColdState3D coldStateA = ref bodies.GetRequiredColdStateByIndex(pair.FirstBodyIndex);
            ref HelPhysicsBodyColdState3D coldStateB = ref bodies.GetRequiredColdStateByIndex(pair.SecondBodyIndex);
            int dynamicBodyIndex;
            int kinematicBodyIndex;
            if (coldStateA.BodyKind == BodyKind3D.Dynamic && coldStateB.BodyKind == BodyKind3D.Kinematic) {
                dynamicBodyIndex = pair.FirstBodyIndex;
                kinematicBodyIndex = pair.SecondBodyIndex;
            } else if (coldStateA.BodyKind == BodyKind3D.Kinematic && coldStateB.BodyKind == BodyKind3D.Dynamic) {
                dynamicBodyIndex = pair.SecondBodyIndex;
                kinematicBodyIndex = pair.FirstBodyIndex;
            } else {
                return;
            }

            ref HelPhysicsBodyState3D kinematicState = ref bodies.GetRequiredStateByIndex(kinematicBodyIndex);
            if (kinematicState.LinearVelocity.LengthSquared() == PhysicsScalar.Zero &&
                kinematicState.AngularVelocity.LengthSquared() == PhysicsScalar.Zero) {
                return;
            }

            WakeConnectedDynamicBody(
                dynamicBodyIndex,
                HelPhysicsWakeReason3D.MovingKinematicContact,
                bodies,
                islands);
        }

        /// <summary>
        /// Returns the initiating reason stored for one current-step asleep-to-awake island event.
        /// </summary>
        /// <param name="wakeEventIndex">Indexed event position below <see cref="WakeEventCount"/>.</param>
        /// <returns>The explicit initiating reason recorded once for the selected transition.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the event index is outside current events.</exception>
        public HelPhysicsWakeReason3D GetWakeEventReason(int wakeEventIndex) {
            if (wakeEventIndex < 0 || wakeEventIndex >= WakeEventCountValue) {
                throw new ArgumentOutOfRangeException(nameof(wakeEventIndex), "The wake event index is outside current-step events.");
            }

            return WakeEventReasons[wakeEventIndex];
        }

        /// <summary>
        /// Returns the current-step asleep-to-awake island count for one explicit diagnostic reason.
        /// </summary>
        /// <param name="reason">Explicit wake reason whose counter is requested.</param>
        /// <returns>The number of current-step events initiated by <paramref name="reason"/>; <see cref="HelPhysicsWakeReason3D.None"/> always returns zero.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="reason"/> is not a defined diagnostic value.</exception>
        public int GetWakeCount(HelPhysicsWakeReason3D reason) {
            if (reason == HelPhysicsWakeReason3D.None) {
                return 0;
            } else if (reason == HelPhysicsWakeReason3D.ExplicitForce) {
                return ExplicitForceWakeCount;
            } else if (reason == HelPhysicsWakeReason3D.ExplicitImpulse) {
                return ExplicitImpulseWakeCount;
            } else if (reason == HelPhysicsWakeReason3D.NewCandidateContact) {
                return NewCandidateContactWakeCount;
            } else if (reason == HelPhysicsWakeReason3D.MovingKinematicContact) {
                return MovingKinematicContactWakeCount;
            }

            throw new ArgumentOutOfRangeException(nameof(reason), "Wake counters require one defined wake reason.");
        }

        /// <summary>
        /// Evaluates one already validated island without allocations or mutation before its complete qualification decision.
        /// </summary>
        /// <param name="bodies">Body pool containing all members.</param>
        /// <param name="islands">Current island ranges.</param>
        /// <param name="islandIndex">Island range to evaluate.</param>
        void EvaluateIsland(
            HelPhysicsBodyPool3D bodies,
            HelPhysicsIslandBuilder3D islands,
            int islandIndex) {
            HelPhysicsIsland3D island = islands.GetIsland(islandIndex);
            bool wakeOccurred = false;
            bool isQuiet = true;
            bool allSleeping = true;
            ushort sharedQuietCount = ushort.MaxValue;
            ushort requiredSleepTicks = 1;
            for (int memberOffset = 0; memberOffset < island.BodyCount; memberOffset++) {
                int bodyIndex = islands.GetBodyIndex(island.BodyStartIndex + memberOffset);
                ref HelPhysicsBodyState3D state = ref bodies.GetRequiredStateByIndex(bodyIndex);
                ref HelPhysicsBodyColdState3D coldState = ref bodies.GetRequiredColdStateByIndex(bodyIndex);
                wakeOccurred = wakeOccurred || WakeOccurredThisStep[bodyIndex];
                allSleeping = allSleeping && !state.IsAwake;
                if (state.LowMotionStepCount < sharedQuietCount) {
                    sharedQuietCount = state.LowMotionStepCount;
                }

                if (coldState.SleepTicks > requiredSleepTicks) {
                    requiredSleepTicks = coldState.SleepTicks;
                }

                if (state.LinearVelocity.LengthSquared() > coldState.LinearSleepThresholdSquared ||
                    state.AngularVelocity.LengthSquared() > coldState.AngularSleepThresholdSquared) {
                    isQuiet = false;
                }
            }

            if (allSleeping) {
                return;
            }

            if (wakeOccurred || !isQuiet) {
                SetIslandQuietCount(bodies, islands, in island, 0);
                return;
            }

            ushort nextQuietCount = sharedQuietCount;
            if (nextQuietCount < ushort.MaxValue) {
                nextQuietCount++;
            }

            SetIslandQuietCount(bodies, islands, in island, nextQuietCount);
            if (nextQuietCount >= requiredSleepTicks) {
                SleepIsland(bodies, islands, in island);
            }
        }

        /// <summary>
        /// Writes one synchronized quiet duration to every body in a published island range.
        /// </summary>
        /// <param name="bodies">Body pool containing all members.</param>
        /// <param name="islands">Current flat member array.</param>
        /// <param name="island">Contiguous range to update.</param>
        /// <param name="quietCount">Shared quiet duration to assign.</param>
        static void SetIslandQuietCount(
            HelPhysicsBodyPool3D bodies,
            HelPhysicsIslandBuilder3D islands,
            in HelPhysicsIsland3D island,
            ushort quietCount) {
            for (int memberOffset = 0; memberOffset < island.BodyCount; memberOffset++) {
                int bodyIndex = islands.GetBodyIndex(island.BodyStartIndex + memberOffset);
                bodies.GetRequiredStateByIndex(bodyIndex).LowMotionStepCount = quietCount;
            }
        }

        /// <summary>
        /// Atomically marks every island member asleep and clears velocity, force, and torque state.
        /// </summary>
        /// <param name="bodies">Body pool containing all members.</param>
        /// <param name="islands">Current flat member array.</param>
        /// <param name="island">Contiguous range to transition.</param>
        static void SleepIsland(
            HelPhysicsBodyPool3D bodies,
            HelPhysicsIslandBuilder3D islands,
            in HelPhysicsIsland3D island) {
            for (int memberOffset = 0; memberOffset < island.BodyCount; memberOffset++) {
                int bodyIndex = islands.GetBodyIndex(island.BodyStartIndex + memberOffset);
                ref HelPhysicsBodyState3D state = ref bodies.GetRequiredStateByIndex(bodyIndex);
                state.LinearVelocity = PhysicsVector3.Zero;
                state.AngularVelocity = PhysicsVector3.Zero;
                state.AccumulatedForce = PhysicsVector3.Zero;
                state.AccumulatedTorque = PhysicsVector3.Zero;
                state.IsAwake = false;
            }
        }

        /// <summary>
        /// Propagates an explicit input through a published island or marks an awake first-step target until current islands are built.
        /// </summary>
        /// <param name="bodyIndex">Validated occupied dynamic input target.</param>
        /// <param name="reason">Explicit force or impulse diagnostic reason.</param>
        /// <param name="bodies">Body pool containing the target.</param>
        /// <param name="islands">Prior island publication, which may be empty before the first build.</param>
        void WakeExplicitInput(
            int bodyIndex,
            HelPhysicsWakeReason3D reason,
            HelPhysicsBodyPool3D bodies,
            HelPhysicsIslandBuilder3D islands) {
            if (islands.GetIslandIndexForBody(bodyIndex) >= 0) {
                WakeConnectedDynamicBody(bodyIndex, reason, bodies, islands);
                return;
            }

            ref HelPhysicsBodyState3D state = ref bodies.GetRequiredStateByIndex(bodyIndex);
            if (!state.IsAwake) {
                throw new InvalidOperationException("A sleeping explicit wake target requires a prior or current island for complete propagation.");
            }

            state.LowMotionStepCount = 0;
            WakeOccurredThisStep[bodyIndex] = true;
        }

        /// <summary>
        /// Wakes one participant only when it is dynamic, allowing candidate processing to ignore static and kinematic counterparts.
        /// </summary>
        /// <param name="bodyIndex">Occupied candidate body index.</param>
        /// <param name="reason">Initiating reason shared by candidate participants.</param>
        /// <param name="bodies">Body pool containing the participant.</param>
        /// <param name="islands">Prior published islands.</param>
        void WakeDynamicParticipant(
            int bodyIndex,
            HelPhysicsWakeReason3D reason,
            HelPhysicsBodyPool3D bodies,
            HelPhysicsIslandBuilder3D islands) {
            if (bodies.GetRequiredColdStateByIndex(bodyIndex).BodyKind == BodyKind3D.Dynamic) {
                WakeConnectedDynamicBody(bodyIndex, reason, bodies, islands);
            }
        }

        /// <summary>
        /// Wakes and resets every member of one published island and records a reason only when any member was asleep.
        /// </summary>
        /// <param name="bodyIndex">Dynamic member identifying the island.</param>
        /// <param name="reason">Initiating wake reason for a possible transition event.</param>
        /// <param name="bodies">Body pool containing all island members.</param>
        /// <param name="islands">Published island range and lookup.</param>
        void WakeConnectedDynamicBody(
            int bodyIndex,
            HelPhysicsWakeReason3D reason,
            HelPhysicsBodyPool3D bodies,
            HelPhysicsIslandBuilder3D islands) {
            int islandIndex = islands.GetIslandIndexForBody(bodyIndex);
            if (islandIndex < 0) {
                throw new InvalidOperationException("Every occupied dynamic wake target must belong to a published island.");
            }

            HelPhysicsIsland3D island = islands.GetIsland(islandIndex);
            bool wasAsleep = false;
            for (int memberOffset = 0; memberOffset < island.BodyCount; memberOffset++) {
                int memberBodyIndex = islands.GetBodyIndex(island.BodyStartIndex + memberOffset);
                ref HelPhysicsBodyState3D state = ref bodies.GetRequiredStateByIndex(memberBodyIndex);
                if (!state.IsAwake) {
                    wasAsleep = true;
                }
            }

            for (int memberOffset = 0; memberOffset < island.BodyCount; memberOffset++) {
                int memberBodyIndex = islands.GetBodyIndex(island.BodyStartIndex + memberOffset);
                ref HelPhysicsBodyState3D state = ref bodies.GetRequiredStateByIndex(memberBodyIndex);
                state.LowMotionStepCount = 0;
                state.IsAwake = true;
                WakeOccurredThisStep[memberBodyIndex] = true;
            }

            if (wasAsleep) {
                RecordWakeEvent(reason);
            }
        }

        /// <summary>
        /// Appends one current-step wake reason and increments its exact profiler-facing counter.
        /// </summary>
        /// <param name="reason">Non-none reason that initiated an actual island transition.</param>
        /// <exception cref="HelPhysicsCapacityExceededException">Thrown if impossible duplicate transitions exceed fixed body-count storage.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the reason is not a wake-producing value.</exception>
        void RecordWakeEvent(HelPhysicsWakeReason3D reason) {
            if (WakeEventCountValue == WakeEventReasons.Length) {
                throw new HelPhysicsCapacityExceededException("wake event", WakeEventReasons.Length);
            }

            WakeEventReasons[WakeEventCountValue++] = reason;
            if (reason == HelPhysicsWakeReason3D.ExplicitForce) {
                ExplicitForceWakeCount++;
            } else if (reason == HelPhysicsWakeReason3D.ExplicitImpulse) {
                ExplicitImpulseWakeCount++;
            } else if (reason == HelPhysicsWakeReason3D.NewCandidateContact) {
                NewCandidateContactWakeCount++;
            } else if (reason == HelPhysicsWakeReason3D.MovingKinematicContact) {
                MovingKinematicContactWakeCount++;
            } else {
                throw new ArgumentOutOfRangeException(nameof(reason), "Wake events require one explicit initiating reason.");
            }
        }

        /// <summary>
        /// Determines whether one occupied candidate participant is a sleeping dynamic body.
        /// </summary>
        /// <param name="bodyIndex">Occupied body index to inspect.</param>
        /// <param name="bodies">Body pool containing the participant.</param>
        /// <returns><see langword="true"/> when the participant is dynamic and asleep.</returns>
        static bool IsSleepingDynamic(int bodyIndex, HelPhysicsBodyPool3D bodies) {
            return bodies.GetRequiredColdStateByIndex(bodyIndex).BodyKind == BodyKind3D.Dynamic &&
                !bodies.GetRequiredStateByIndex(bodyIndex).IsAwake;
        }

        /// <summary>
        /// Validates exact body capacities shared by the sleeper, body pool, and island builder.
        /// </summary>
        /// <param name="bodies">Body pool to validate.</param>
        /// <param name="islands">Island builder to validate.</param>
        void ValidateFixedInputs(HelPhysicsBodyPool3D bodies, HelPhysicsIslandBuilder3D islands) {
            if (bodies == null) {
                throw new ArgumentNullException(nameof(bodies));
            } else if (islands == null) {
                throw new ArgumentNullException(nameof(islands));
            }

            if (bodies.Capacity != WakeOccurredThisStep.Length || islands.BodyCapacity != WakeOccurredThisStep.Length) {
                throw new ArgumentException("Sleeper, body pool, and island builder body capacities must match exactly.");
            }
        }

        /// <summary>
        /// Validates all published ranges, inverse lookups, dynamic membership, awake-state atomicity, and sleep settings before evaluation mutates any body.
        /// </summary>
        /// <param name="bodies">Body pool containing published members.</param>
        /// <param name="islands">Current island publication to validate.</param>
        static void ValidatePublishedIslands(
            HelPhysicsBodyPool3D bodies,
            HelPhysicsIslandBuilder3D islands) {
            for (int islandIndex = 0; islandIndex < islands.IslandCount; islandIndex++) {
                HelPhysicsIsland3D island = islands.GetIsland(islandIndex);
                bool firstMemberIsAwake = false;
                for (int memberOffset = 0; memberOffset < island.BodyCount; memberOffset++) {
                    int bodyIndex = islands.GetBodyIndex(island.BodyStartIndex + memberOffset);
                    if (!bodies.IsOccupied(bodyIndex)) {
                        throw new InvalidOperationException("Published island members must remain occupied through sleep evaluation.");
                    }

                    ref HelPhysicsBodyState3D state = ref bodies.GetRequiredStateByIndex(bodyIndex);
                    ref HelPhysicsBodyColdState3D coldState = ref bodies.GetRequiredColdStateByIndex(bodyIndex);
                    if (coldState.BodyKind != BodyKind3D.Dynamic) {
                        throw new InvalidOperationException("Published islands may contain only dynamic bodies.");
                    }

                    if (islands.GetIslandIndexForBody(bodyIndex) != islandIndex) {
                        throw new InvalidOperationException("Published body-to-island lookup must match every member range.");
                    }

                    ValidateSleepConfiguration(in coldState);
                    state.LinearVelocity.LengthSquared();
                    state.AngularVelocity.LengthSquared();
                    if (memberOffset == 0) {
                        firstMemberIsAwake = state.IsAwake;
                    } else if (state.IsAwake != firstMemberIsAwake) {
                        throw new InvalidOperationException("Every published island must enter sleep evaluation with one atomic awake state.");
                    }
                }
            }
        }

        /// <summary>
        /// Validates one stored cold sleep configuration, including defaults that bypass value-type constructors.
        /// </summary>
        /// <param name="coldState">Cold body metadata to validate before evaluation.</param>
        /// <exception cref="InvalidOperationException">Thrown when thresholds are invalid or sleep ticks are zero.</exception>
        static void ValidateSleepConfiguration(in HelPhysicsBodyColdState3D coldState) {
            double linearThreshold = coldState.LinearSleepThresholdSquared.ToFloat();
            double angularThreshold = coldState.AngularSleepThresholdSquared.ToFloat();
            if (double.IsNaN(linearThreshold) ||
                double.IsInfinity(linearThreshold) ||
                linearThreshold < 0d ||
                double.IsNaN(angularThreshold) ||
                double.IsInfinity(angularThreshold) ||
                angularThreshold < 0d ||
                coldState.SleepTicks == 0) {
                throw new InvalidOperationException("Every dynamic island member requires finite non-negative squared sleep thresholds and positive sleep ticks.");
            }
        }

        /// <summary>
        /// Validates an explicit dynamic wake target and exact fixed capacities before any propagation mutation.
        /// </summary>
        /// <param name="bodyIndex">Body index to validate.</param>
        /// <param name="bodies">Body pool containing the target.</param>
        /// <param name="islands">Published islands containing the target.</param>
        void ValidateWakeTarget(
            int bodyIndex,
            HelPhysicsBodyPool3D bodies,
            HelPhysicsIslandBuilder3D islands) {
            ValidateFixedInputs(bodies, islands);
            if (bodyIndex < 0 || bodyIndex >= bodies.Capacity) {
                throw new ArgumentOutOfRangeException(nameof(bodyIndex), "Wake targets must address one fixed body slot.");
            }

            if (!bodies.IsOccupied(bodyIndex)) {
                throw new InvalidOperationException("Wake targets must address an occupied body slot.");
            }

            if (bodies.GetRequiredColdStateByIndex(bodyIndex).BodyKind != BodyKind3D.Dynamic) {
                throw new InvalidOperationException("Explicit force and impulse wake targets must be dynamic bodies.");
            }

        }

        /// <summary>
        /// Validates one canonical occupied candidate against exact sleeper capacities and published dynamic lookup.
        /// </summary>
        /// <param name="candidate">Candidate pair to validate.</param>
        /// <param name="bodies">Body pool containing both participants.</param>
        /// <param name="islands">Prior island publication used for dynamic propagation.</param>
        void ValidateCandidate(
            HelPhysicsCandidatePair3D candidate,
            HelPhysicsBodyPool3D bodies,
            HelPhysicsIslandBuilder3D islands) {
            ValidateFixedInputs(bodies, islands);
            if (candidate.FirstBodyIndex < 0 || candidate.SecondBodyIndex <= candidate.FirstBodyIndex) {
                throw new ArgumentOutOfRangeException(nameof(candidate), "Wake candidates must contain ascending distinct body indices.");
            }

            if (candidate.SecondBodyIndex >= bodies.Capacity) {
                throw new ArgumentOutOfRangeException(nameof(candidate), "Wake candidates must address body indices within pool capacity.");
            }

            if (!bodies.IsOccupied(candidate.FirstBodyIndex) || !bodies.IsOccupied(candidate.SecondBodyIndex)) {
                throw new InvalidOperationException("Wake candidates must address two occupied body slots.");
            }

        }

        /// <summary>
        /// Validates one active canonical contact pair before moving-kinematic wake detection.
        /// </summary>
        /// <param name="pair">Canonical pair to validate.</param>
        /// <param name="manifold">Manifold that must be active.</param>
        /// <param name="bodies">Body pool containing both participants.</param>
        /// <param name="islands">Published islands used for dynamic propagation.</param>
        void ValidateContactPair(
            HelPhysicsPairKey3D pair,
            in HelPhysicsContactManifold3D manifold,
            HelPhysicsBodyPool3D bodies,
            HelPhysicsIslandBuilder3D islands) {
            ValidateFixedInputs(bodies, islands);
            if (pair.FirstBodyIndex < 0 || pair.SecondBodyIndex <= pair.FirstBodyIndex) {
                throw new ArgumentOutOfRangeException(nameof(pair), "Wake contact pairs must contain ascending distinct body indices.");
            }

            if (pair.SecondBodyIndex >= bodies.Capacity) {
                throw new ArgumentOutOfRangeException(nameof(pair), "Wake contact pairs must address body indices within pool capacity.");
            }

            if (!bodies.IsOccupied(pair.FirstBodyIndex) || !bodies.IsOccupied(pair.SecondBodyIndex)) {
                throw new InvalidOperationException("Wake contact pairs must address two occupied body slots.");
            }

            if (manifold.ContactCount <= 0 || manifold.ContactCount > 4) {
                throw new InvalidOperationException("Moving kinematic wake contacts require an active one-through-four-contact manifold.");
            }

            ValidateDynamicParticipantLookup(pair.FirstBodyIndex, bodies, islands);
            ValidateDynamicParticipantLookup(pair.SecondBodyIndex, bodies, islands);
        }

        /// <summary>
        /// Ensures an occupied dynamic pair participant belongs to the supplied prior or current island publication.
        /// </summary>
        /// <param name="bodyIndex">Occupied pair participant.</param>
        /// <param name="bodies">Body pool containing its mode.</param>
        /// <param name="islands">Island lookup to validate when the participant is dynamic.</param>
        static void ValidateDynamicParticipantLookup(
            int bodyIndex,
            HelPhysicsBodyPool3D bodies,
            HelPhysicsIslandBuilder3D islands) {
            if (bodies.GetRequiredColdStateByIndex(bodyIndex).BodyKind == BodyKind3D.Dynamic &&
                islands.GetIslandIndexForBody(bodyIndex) < 0) {
                throw new InvalidOperationException("Every dynamic wake participant must belong to the supplied island publication.");
            }
        }
    }
}
