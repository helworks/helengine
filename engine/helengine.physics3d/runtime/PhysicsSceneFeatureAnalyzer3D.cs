namespace helengine {
    /// <summary>
    /// Analyzes scene entities to determine which 3D physics systems and interaction resolvers can possibly be required.
    /// </summary>
    public static class PhysicsSceneFeatureAnalyzer3D {
        /// <summary>
        /// Stable serialized component id for 3D rigid bodies.
        /// </summary>
        const string RigidBody3DComponentTypeId = "helengine.RigidBody3DComponent";

        /// <summary>
        /// Current rigid-body component payload version accepted during serialized scene analysis.
        /// </summary>
        const byte CurrentRigidBodyPayloadVersion = 2;

        /// <summary>
        /// Automatic reflected rigid-body payload member count used after built-in component persistence moved onto the shared reflected path.
        /// </summary>
        const int AutomaticRigidBodyMemberCount = 6;

        /// <summary>
        /// Legacy box-collider payload version that only serialized the collider size.
        /// </summary>
        const byte LegacyBoxColliderPayloadVersion = 1;

        /// <summary>
        /// Current box-collider payload version that also serializes layer, mask, and trigger state.
        /// </summary>
        const byte CurrentBoxColliderPayloadVersion = 2;

        /// <summary>
        /// Stable serialized component id for 3D box colliders.
        /// </summary>
        const string BoxCollider3DComponentTypeId = "helengine.BoxCollider3DComponent";

        /// <summary>
        /// Automatic reflected box-collider payload member count used after built-in component persistence moved onto the shared reflected path.
        /// </summary>
        const int AutomaticBoxColliderMemberCount = 7;

        /// <summary>
        /// Stable serialized component id for 3D sphere colliders.
        /// </summary>
        const string SphereCollider3DComponentTypeId = "helengine.SphereCollider3DComponent";

        /// <summary>
        /// Automatic reflected sphere-collider payload member count used after built-in component persistence moved onto the shared reflected path.
        /// </summary>
        const int AutomaticSphereColliderMemberCount = 7;

        /// <summary>
        /// Stable serialized component id for 3D capsule colliders.
        /// </summary>
        const string CapsuleCollider3DComponentTypeId = "helengine.CapsuleCollider3DComponent";

        /// <summary>
        /// Automatic reflected capsule-collider payload member count used after built-in component persistence moved onto the shared reflected path.
        /// </summary>
        const int AutomaticCapsuleColliderMemberCount = 8;

        /// <summary>
        /// Stable serialized component id for 3D cooked static-mesh colliders.
        /// </summary>
        const string StaticMeshCollider3DComponentTypeId = "helengine.StaticMeshCollider3DComponent";

        /// <summary>
        /// Automatic reflected static-mesh-collider payload member count used after built-in component persistence moved onto the shared reflected path.
        /// </summary>
        const int AutomaticStaticMeshColliderMemberCount = 8;

        /// <summary>
        /// Stable serialized component id for 3D character controllers.
        /// </summary>
        const string CharacterController3DComponentTypeId = "helengine.CharacterController3DComponent";

        /// <summary>
        /// Stable serialized component id for 3D kinematic-motion paths.
        /// </summary>
        const string KinematicMotion3DComponentTypeId = "helengine.KinematicMotion3DComponent";

        /// <summary>
        /// Analyzes one serialized scene asset and returns the required 3D physics feature flags.
        /// </summary>
        /// <param name="sceneAsset">Serialized scene asset to analyze.</param>
        /// <returns>Required scene feature flags inferred from the serialized scene records.</returns>
        public static PhysicsSceneFeatureFlags3D Analyze(SceneAsset sceneAsset) {
            if (sceneAsset == null) {
                throw new ArgumentNullException(nameof(sceneAsset));
            }

            PhysicsSceneFeatureCounterState3D counterState = new PhysicsSceneFeatureCounterState3D();
            SceneEntityAsset[] rootEntityAssets = sceneAsset.RootEntities ?? Array.Empty<SceneEntityAsset>();
            for (int index = 0; index < rootEntityAssets.Length; index++) {
                AccumulateEntityAssetFeatures(rootEntityAssets[index], counterState);
            }

            return BuildFeatureFlags(counterState);
        }

        /// <summary>
        /// Analyzes one serialized scene hierarchy and returns the required 3D physics feature flags.
        /// </summary>
        /// <param name="rootEntityAssets">Serialized root entities that own the authored scene hierarchy.</param>
        /// <returns>Required scene feature flags inferred from the serialized scene records.</returns>
        public static PhysicsSceneFeatureFlags3D Analyze(IReadOnlyList<SceneEntityAsset> rootEntityAssets) {
            if (rootEntityAssets == null) {
                throw new ArgumentNullException(nameof(rootEntityAssets));
            }

            PhysicsSceneFeatureCounterState3D counterState = new PhysicsSceneFeatureCounterState3D();
            for (int index = 0; index < rootEntityAssets.Count; index++) {
                AccumulateEntityAssetFeatures(rootEntityAssets[index], counterState);
            }

            return BuildFeatureFlags(counterState);
        }

        /// <summary>
        /// Analyzes one scene hierarchy and returns the required 3D physics feature flags.
        /// </summary>
        /// <param name="rootEntities">Root entities that own the active scene hierarchy.</param>
        /// <returns>Required scene feature flags inferred from the authored components.</returns>
        public static PhysicsSceneFeatureFlags3D Analyze(IReadOnlyList<Entity> rootEntities) {
            if (rootEntities == null) {
                throw new ArgumentNullException(nameof(rootEntities));
            }

            PhysicsSceneFeatureCounterState3D counterState = new PhysicsSceneFeatureCounterState3D();
            for (int index = 0; index < rootEntities.Count; index++) {
                AccumulateEntityFeatures(rootEntities[index], counterState);
            }

            return BuildFeatureFlags(counterState);
        }

        /// <summary>
        /// Walks one serialized entity subtree and accumulates feature counts from supported serialized physics component records.
        /// </summary>
        /// <param name="entityAsset">Current serialized entity being analyzed.</param>
        /// <param name="counterState">Mutable counter state that collects scene capabilities.</param>
        static void AccumulateEntityAssetFeatures(SceneEntityAsset entityAsset, PhysicsSceneFeatureCounterState3D counterState) {
            if (entityAsset == null) {
                throw new ArgumentNullException(nameof(entityAsset));
            }
            if (counterState == null) {
                throw new ArgumentNullException(nameof(counterState));
            }

            bool hasRigidBody = false;
            BodyKind3D bodyKind = BodyKind3D.Static;
            bool hasBoxCollider = false;
            bool boxIsTrigger = false;
            bool hasSphereCollider = false;
            bool sphereIsTrigger = false;
            bool hasCapsuleCollider = false;
            bool capsuleIsTrigger = false;
            bool hasStaticMeshCollider = false;
            bool staticMeshIsTrigger = false;
            bool hasCharacterController = false;
            bool hasKinematicMotion = false;

            SceneComponentAssetRecord[] componentRecords = entityAsset.Components ?? Array.Empty<SceneComponentAssetRecord>();
            for (int index = 0; index < componentRecords.Length; index++) {
                SceneComponentAssetRecord record = componentRecords[index];
                if (TryReadRigidBodyKind(record, out BodyKind3D readBodyKind)) {
                    hasRigidBody = true;
                    bodyKind = readBodyKind;
                } else if (IsSceneComponentType(record, BoxCollider3DComponentTypeId)) {
                    hasBoxCollider = true;
                    boxIsTrigger = ReadColliderIsTrigger(record);
                } else if (IsSceneComponentType(record, SphereCollider3DComponentTypeId)) {
                    hasSphereCollider = true;
                    sphereIsTrigger = ReadColliderIsTrigger(record);
                } else if (IsSceneComponentType(record, CapsuleCollider3DComponentTypeId)) {
                    hasCapsuleCollider = true;
                    capsuleIsTrigger = ReadColliderIsTrigger(record);
                } else if (IsSceneComponentType(record, StaticMeshCollider3DComponentTypeId)) {
                    hasStaticMeshCollider = true;
                    staticMeshIsTrigger = ReadColliderIsTrigger(record);
                } else if (IsSceneComponentType(record, CharacterController3DComponentTypeId)) {
                    hasCharacterController = true;
                } else if (IsSceneComponentType(record, KinematicMotion3DComponentTypeId)) {
                    hasKinematicMotion = true;
                }
            }

            if (hasKinematicMotion) {
                counterState.HasKinematicMotion = true;
            }
            if (boxIsTrigger || sphereIsTrigger || capsuleIsTrigger || staticMeshIsTrigger) {
                counterState.HasTriggerCollider = true;
            }
            if (hasCharacterController) {
                counterState.HasCharacterController = true;
            }

            if (hasRigidBody && hasBoxCollider) {
                AccumulateSerializedBoxBodyFeatures(bodyKind, boxIsTrigger, counterState);
            } else if (hasRigidBody && hasSphereCollider) {
                AccumulateSerializedSphereBodyFeatures(bodyKind, sphereIsTrigger, counterState);
            } else if (hasRigidBody && hasCapsuleCollider) {
                AccumulateSerializedCapsuleBodyFeatures(bodyKind, capsuleIsTrigger, counterState);
            } else if (hasRigidBody && hasStaticMeshCollider) {
                AccumulateSerializedStaticMeshBodyFeatures(staticMeshIsTrigger, counterState);
            }

            SceneEntityAsset[] childEntityAssets = entityAsset.Children ?? Array.Empty<SceneEntityAsset>();
            for (int index = 0; index < childEntityAssets.Length; index++) {
                AccumulateEntityAssetFeatures(childEntityAssets[index], counterState);
            }
        }

        /// <summary>
        /// Walks one entity subtree and accumulates feature counts from supported physics components.
        /// </summary>
        /// <param name="entity">Current entity being analyzed.</param>
        /// <param name="counterState">Mutable counter state that collects scene capabilities.</param>
        static void AccumulateEntityFeatures(Entity entity, PhysicsSceneFeatureCounterState3D counterState) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (counterState == null) {
                throw new ArgumentNullException(nameof(counterState));
            }

            RigidBody3DComponent rigidBody = FindRigidBody(entity);
            BoxCollider3DComponent boxCollider = FindBoxCollider(entity);
            SphereCollider3DComponent sphereCollider = FindSphereCollider(entity);
            CapsuleCollider3DComponent capsuleCollider = FindCapsuleCollider(entity);
            StaticMeshCollider3DComponent staticMeshCollider = FindStaticMeshCollider(entity);
            CharacterController3DComponent characterController = FindCharacterController(entity);
            KinematicMotion3DComponent kinematicMotion = FindKinematicMotion(entity);

            if (kinematicMotion != null) {
                counterState.HasKinematicMotion = true;
            }
            if (boxCollider != null && boxCollider.IsTrigger) {
                counterState.HasTriggerCollider = true;
            }
            if (sphereCollider != null && sphereCollider.IsTrigger) {
                counterState.HasTriggerCollider = true;
            }
            if (capsuleCollider != null && capsuleCollider.IsTrigger) {
                counterState.HasTriggerCollider = true;
            }
            if (staticMeshCollider != null && staticMeshCollider.IsTrigger) {
                counterState.HasTriggerCollider = true;
            }
            if (characterController != null) {
                counterState.HasCharacterController = true;
            }

            if (rigidBody != null && boxCollider != null) {
                AccumulateBoxBodyFeatures(rigidBody, boxCollider, counterState);
            } else if (rigidBody != null && sphereCollider != null) {
                AccumulateSphereBodyFeatures(rigidBody, sphereCollider, counterState);
            } else if (rigidBody != null && capsuleCollider != null) {
                AccumulateCapsuleBodyFeatures(rigidBody, capsuleCollider, counterState);
            } else if (rigidBody != null && staticMeshCollider != null) {
                AccumulateStaticMeshBodyFeatures(rigidBody, staticMeshCollider, counterState);
            }

            if (entity.Children == null) {
                return;
            }

            for (int index = 0; index < entity.Children.Count; index++) {
                AccumulateEntityFeatures(entity.Children[index], counterState);
            }
        }

        /// <summary>
        /// Accumulates feature counters for one rigid body that uses a box collider.
        /// </summary>
        /// <param name="rigidBody">Authored rigid body component.</param>
        /// <param name="boxCollider">Authored box collider component.</param>
        /// <param name="counterState">Mutable counter state.</param>
        static void AccumulateBoxBodyFeatures(RigidBody3DComponent rigidBody, BoxCollider3DComponent boxCollider, PhysicsSceneFeatureCounterState3D counterState) {
            if (rigidBody == null) {
                throw new ArgumentNullException(nameof(rigidBody));
            }
            if (boxCollider == null) {
                throw new ArgumentNullException(nameof(boxCollider));
            }
            if (counterState == null) {
                throw new ArgumentNullException(nameof(counterState));
            }

            if (!boxCollider.IsTrigger) {
                counterState.SolidBoxCount = counterState.SolidBoxCount + 1;
                if (rigidBody.BodyKind == BodyKind3D.Static || rigidBody.BodyKind == BodyKind3D.Kinematic) {
                    counterState.CharacterControllerBodySupportCount = counterState.CharacterControllerBodySupportCount + 1;
                }
            }
            if (rigidBody.BodyKind == BodyKind3D.Dynamic) {
                counterState.DynamicBoxCount = counterState.DynamicBoxCount + 1;
            }
        }

        /// <summary>
        /// Accumulates feature counters for one rigid body that uses a sphere collider.
        /// </summary>
        /// <param name="rigidBody">Authored rigid body component.</param>
        /// <param name="sphereCollider">Authored sphere collider component.</param>
        /// <param name="counterState">Mutable counter state.</param>
        static void AccumulateSphereBodyFeatures(RigidBody3DComponent rigidBody, SphereCollider3DComponent sphereCollider, PhysicsSceneFeatureCounterState3D counterState) {
            if (rigidBody == null) {
                throw new ArgumentNullException(nameof(rigidBody));
            }
            if (sphereCollider == null) {
                throw new ArgumentNullException(nameof(sphereCollider));
            }
            if (counterState == null) {
                throw new ArgumentNullException(nameof(counterState));
            }

            if (!sphereCollider.IsTrigger) {
                counterState.SolidSphereCount = counterState.SolidSphereCount + 1;
            }
            if (rigidBody.BodyKind == BodyKind3D.Dynamic) {
                counterState.DynamicSphereCount = counterState.DynamicSphereCount + 1;
            }
        }

        /// <summary>
        /// Accumulates feature counters for one rigid body that uses a capsule collider.
        /// </summary>
        /// <param name="rigidBody">Authored rigid body component.</param>
        /// <param name="capsuleCollider">Authored capsule collider component.</param>
        /// <param name="counterState">Mutable counter state.</param>
        static void AccumulateCapsuleBodyFeatures(RigidBody3DComponent rigidBody, CapsuleCollider3DComponent capsuleCollider, PhysicsSceneFeatureCounterState3D counterState) {
            if (rigidBody == null) {
                throw new ArgumentNullException(nameof(rigidBody));
            }
            if (capsuleCollider == null) {
                throw new ArgumentNullException(nameof(capsuleCollider));
            }
            if (counterState == null) {
                throw new ArgumentNullException(nameof(counterState));
            }

            if (!capsuleCollider.IsTrigger) {
                counterState.SolidCapsuleCount = counterState.SolidCapsuleCount + 1;
            }
            if (rigidBody.BodyKind == BodyKind3D.Dynamic) {
                counterState.DynamicCapsuleCount = counterState.DynamicCapsuleCount + 1;
            }
        }

        /// <summary>
        /// Accumulates feature counters for one rigid body that uses a cooked static-mesh collider.
        /// </summary>
        /// <param name="rigidBody">Authored rigid body component.</param>
        /// <param name="staticMeshCollider">Authored static-mesh collider component.</param>
        /// <param name="counterState">Mutable counter state.</param>
        static void AccumulateStaticMeshBodyFeatures(RigidBody3DComponent rigidBody, StaticMeshCollider3DComponent staticMeshCollider, PhysicsSceneFeatureCounterState3D counterState) {
            if (rigidBody == null) {
                throw new ArgumentNullException(nameof(rigidBody));
            }
            if (staticMeshCollider == null) {
                throw new ArgumentNullException(nameof(staticMeshCollider));
            }
            if (counterState == null) {
                throw new ArgumentNullException(nameof(counterState));
            }

            if (!staticMeshCollider.IsTrigger) {
                counterState.SolidStaticMeshCount = counterState.SolidStaticMeshCount + 1;
                counterState.CharacterControllerStaticMeshSupportCount = counterState.CharacterControllerStaticMeshSupportCount + 1;
            }
        }

        /// <summary>
        /// Accumulates feature counters for one serialized rigid body that uses a box collider.
        /// </summary>
        /// <param name="bodyKind">Serialized rigid-body participation mode.</param>
        /// <param name="isTrigger">True when the collider is configured as a trigger.</param>
        /// <param name="counterState">Mutable counter state.</param>
        static void AccumulateSerializedBoxBodyFeatures(BodyKind3D bodyKind, bool isTrigger, PhysicsSceneFeatureCounterState3D counterState) {
            if (counterState == null) {
                throw new ArgumentNullException(nameof(counterState));
            }

            if (!isTrigger) {
                counterState.SolidBoxCount = counterState.SolidBoxCount + 1;
                if (bodyKind == BodyKind3D.Static || bodyKind == BodyKind3D.Kinematic) {
                    counterState.CharacterControllerBodySupportCount = counterState.CharacterControllerBodySupportCount + 1;
                }
            }
            if (bodyKind == BodyKind3D.Dynamic) {
                counterState.DynamicBoxCount = counterState.DynamicBoxCount + 1;
            }
        }

        /// <summary>
        /// Accumulates feature counters for one serialized rigid body that uses a sphere collider.
        /// </summary>
        /// <param name="bodyKind">Serialized rigid-body participation mode.</param>
        /// <param name="isTrigger">True when the collider is configured as a trigger.</param>
        /// <param name="counterState">Mutable counter state.</param>
        static void AccumulateSerializedSphereBodyFeatures(BodyKind3D bodyKind, bool isTrigger, PhysicsSceneFeatureCounterState3D counterState) {
            if (counterState == null) {
                throw new ArgumentNullException(nameof(counterState));
            }

            if (!isTrigger) {
                counterState.SolidSphereCount = counterState.SolidSphereCount + 1;
            }
            if (bodyKind == BodyKind3D.Dynamic) {
                counterState.DynamicSphereCount = counterState.DynamicSphereCount + 1;
            }
        }

        /// <summary>
        /// Accumulates feature counters for one serialized rigid body that uses a capsule collider.
        /// </summary>
        /// <param name="bodyKind">Serialized rigid-body participation mode.</param>
        /// <param name="isTrigger">True when the collider is configured as a trigger.</param>
        /// <param name="counterState">Mutable counter state.</param>
        static void AccumulateSerializedCapsuleBodyFeatures(BodyKind3D bodyKind, bool isTrigger, PhysicsSceneFeatureCounterState3D counterState) {
            if (counterState == null) {
                throw new ArgumentNullException(nameof(counterState));
            }

            if (!isTrigger) {
                counterState.SolidCapsuleCount = counterState.SolidCapsuleCount + 1;
            }
            if (bodyKind == BodyKind3D.Dynamic) {
                counterState.DynamicCapsuleCount = counterState.DynamicCapsuleCount + 1;
            }
        }

        /// <summary>
        /// Accumulates feature counters for one serialized rigid body that uses a cooked static-mesh collider.
        /// </summary>
        /// <param name="isTrigger">True when the collider is configured as a trigger.</param>
        /// <param name="counterState">Mutable counter state.</param>
        static void AccumulateSerializedStaticMeshBodyFeatures(bool isTrigger, PhysicsSceneFeatureCounterState3D counterState) {
            if (counterState == null) {
                throw new ArgumentNullException(nameof(counterState));
            }

            if (!isTrigger) {
                counterState.SolidStaticMeshCount = counterState.SolidStaticMeshCount + 1;
                counterState.CharacterControllerStaticMeshSupportCount = counterState.CharacterControllerStaticMeshSupportCount + 1;
            }
        }

        /// <summary>
        /// Builds the final feature flags from one accumulated counter state.
        /// </summary>
        /// <param name="counterState">Intermediate counter state.</param>
        /// <returns>Required scene feature flags.</returns>
        static PhysicsSceneFeatureFlags3D BuildFeatureFlags(PhysicsSceneFeatureCounterState3D counterState) {
            if (counterState == null) {
                throw new ArgumentNullException(nameof(counterState));
            }

            PhysicsSceneFeatureFlags3D flags = PhysicsSceneFeatureFlags3D.None;
            if (counterState.HasKinematicMotion) {
                flags = AddFeatureFlag(flags, PhysicsSceneFeatureFlags3D.KinematicMotion);
            }
            if (counterState.HasTriggerCollider) {
                flags = AddFeatureFlag(flags, PhysicsSceneFeatureFlags3D.TriggerEvents);
            }
            if (counterState.HasCharacterController) {
                flags = AddFeatureFlag(flags, PhysicsSceneFeatureFlags3D.CharacterController);
            }
            if (counterState.DynamicBoxCount > 0 && counterState.SolidBoxCount > 1) {
                flags = AddFeatureFlag(flags, PhysicsSceneFeatureFlags3D.BoxBoxContact);
            }
            if (counterState.DynamicSphereCount > 0 && counterState.SolidSphereCount > 1) {
                flags = AddFeatureFlag(flags, PhysicsSceneFeatureFlags3D.SphereSphereContact);
            }
            if ((counterState.DynamicSphereCount > 0 && counterState.SolidBoxCount > 0) ||
                (counterState.DynamicBoxCount > 0 && counterState.SolidSphereCount > 0)) {
                flags = AddFeatureFlag(flags, PhysicsSceneFeatureFlags3D.SphereBoxContact);
            }
            if ((counterState.DynamicCapsuleCount > 0 && counterState.SolidBoxCount > 0) ||
                (counterState.DynamicBoxCount > 0 && counterState.SolidCapsuleCount > 0)) {
                flags = AddFeatureFlag(flags, PhysicsSceneFeatureFlags3D.CapsuleBoxContact);
            }
            if ((counterState.DynamicCapsuleCount > 0 && counterState.SolidSphereCount > 0) ||
                (counterState.DynamicSphereCount > 0 && counterState.SolidCapsuleCount > 0)) {
                flags = AddFeatureFlag(flags, PhysicsSceneFeatureFlags3D.CapsuleSphereContact);
            }
            if (counterState.DynamicCapsuleCount > 0 && counterState.SolidCapsuleCount > 1) {
                flags = AddFeatureFlag(flags, PhysicsSceneFeatureFlags3D.CapsuleCapsuleContact);
            }
            if (counterState.DynamicBoxCount > 0 && counterState.SolidStaticMeshCount > 0) {
                flags = AddFeatureFlag(flags, PhysicsSceneFeatureFlags3D.BoxStaticMeshContact);
            }
            if (counterState.DynamicSphereCount > 0 && counterState.SolidStaticMeshCount > 0) {
                flags = AddFeatureFlag(flags, PhysicsSceneFeatureFlags3D.SphereStaticMeshContact);
            }
            if (counterState.DynamicCapsuleCount > 0 && counterState.SolidStaticMeshCount > 0) {
                flags = AddFeatureFlag(flags, PhysicsSceneFeatureFlags3D.CapsuleStaticMeshContact);
            }
            if (counterState.HasCharacterController && counterState.CharacterControllerBodySupportCount > 0) {
                flags = AddFeatureFlag(flags, PhysicsSceneFeatureFlags3D.CharacterControllerBodySupport);
            }
            if (counterState.HasCharacterController && counterState.CharacterControllerStaticMeshSupportCount > 0) {
                flags = AddFeatureFlag(flags, PhysicsSceneFeatureFlags3D.CharacterControllerStaticMeshSupport);
            }

            return flags;
        }

        /// <summary>
        /// Adds a single feature bit to a flag set using numeric operations that are portable to generated native code.
        /// </summary>
        /// <param name="flags">Existing feature flag set.</param>
        /// <param name="feature">Feature bit to add.</param>
        /// <returns>Feature flag set containing the requested bit.</returns>
        static PhysicsSceneFeatureFlags3D AddFeatureFlag(PhysicsSceneFeatureFlags3D flags, PhysicsSceneFeatureFlags3D feature) {
            return (PhysicsSceneFeatureFlags3D)((uint)flags + (uint)feature);
        }

        /// <summary>
        /// Resolves the rigid body component attached to one entity.
        /// </summary>
        /// <param name="entity">Entity whose rigid body should be found.</param>
        /// <returns>Attached rigid body when present; otherwise null.</returns>
        static RigidBody3DComponent FindRigidBody(Entity entity) {
            return FindComponent<RigidBody3DComponent>(entity);
        }

        /// <summary>
        /// Resolves the box collider component attached to one entity.
        /// </summary>
        /// <param name="entity">Entity whose box collider should be found.</param>
        /// <returns>Attached box collider when present; otherwise null.</returns>
        static BoxCollider3DComponent FindBoxCollider(Entity entity) {
            return FindComponent<BoxCollider3DComponent>(entity);
        }

        /// <summary>
        /// Resolves the sphere collider component attached to one entity.
        /// </summary>
        /// <param name="entity">Entity whose sphere collider should be found.</param>
        /// <returns>Attached sphere collider when present; otherwise null.</returns>
        static SphereCollider3DComponent FindSphereCollider(Entity entity) {
            return FindComponent<SphereCollider3DComponent>(entity);
        }

        /// <summary>
        /// Resolves the capsule collider component attached to one entity.
        /// </summary>
        /// <param name="entity">Entity whose capsule collider should be found.</param>
        /// <returns>Attached capsule collider when present; otherwise null.</returns>
        static CapsuleCollider3DComponent FindCapsuleCollider(Entity entity) {
            return FindComponent<CapsuleCollider3DComponent>(entity);
        }

        /// <summary>
        /// Resolves the static-mesh collider component attached to one entity.
        /// </summary>
        /// <param name="entity">Entity whose static-mesh collider should be found.</param>
        /// <returns>Attached static-mesh collider when present; otherwise null.</returns>
        static StaticMeshCollider3DComponent FindStaticMeshCollider(Entity entity) {
            return FindComponent<StaticMeshCollider3DComponent>(entity);
        }

        /// <summary>
        /// Resolves the character-controller component attached to one entity.
        /// </summary>
        /// <param name="entity">Entity whose character-controller component should be found.</param>
        /// <returns>Attached character-controller component when present; otherwise null.</returns>
        static CharacterController3DComponent FindCharacterController(Entity entity) {
            return FindComponent<CharacterController3DComponent>(entity);
        }

        /// <summary>
        /// Resolves the kinematic motion component attached to one entity.
        /// </summary>
        /// <param name="entity">Entity whose kinematic motion component should be found.</param>
        /// <returns>Attached kinematic motion component when present; otherwise null.</returns>
        static KinematicMotion3DComponent FindKinematicMotion(Entity entity) {
            return FindComponent<KinematicMotion3DComponent>(entity);
        }

        /// <summary>
        /// Finds one component of the requested type on the supplied entity.
        /// </summary>
        /// <typeparam name="TComponent">Requested component type.</typeparam>
        /// <param name="entity">Entity being queried.</param>
        /// <returns>Attached component when present; otherwise null.</returns>
        static TComponent FindComponent<TComponent>(Entity entity) where TComponent : Component {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (entity.Components == null) {
                return null;
            }

            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is TComponent component) {
                    return component;
                }
            }

            return null;
        }

        /// <summary>
        /// Attempts to read the rigid-body participation mode from one serialized scene component record.
        /// </summary>
        /// <param name="record">Serialized scene component record to inspect.</param>
        /// <param name="bodyKind">Decoded rigid-body participation mode when the record is a rigid body.</param>
        /// <returns>True when the record encodes a rigid body; otherwise false.</returns>
        static bool TryReadRigidBodyKind(SceneComponentAssetRecord record, out BodyKind3D bodyKind) {
            if (!IsSceneComponentType(record, RigidBody3DComponentTypeId)) {
                bodyKind = BodyKind3D.Static;
                return false;
            }

            byte[] payload = record.Payload ?? Array.Empty<byte>();
            using MemoryStream stream = new MemoryStream(payload, false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version == CurrentRigidBodyPayloadVersion) {
                bodyKind = (BodyKind3D)reader.ReadByte();
                return true;
            }
            if (version != 1) {
                throw new InvalidOperationException($"Unsupported rigid body component payload version '{version}'.");
            }

            if (TryReadAutomaticMemberCount(payload, AutomaticRigidBodyMemberCount)) {
                reader.ReadInt32();
                reader.ReadFloat3();
                bodyKind = (BodyKind3D)reader.ReadInt32();
                return true;
            }

            bodyKind = (BodyKind3D)reader.ReadByte();
            return true;
        }

        /// <summary>
        /// Reads the trigger flag from one serialized collider scene component record.
        /// </summary>
        /// <param name="record">Serialized collider scene component record to inspect.</param>
        /// <returns>True when the collider is configured as a trigger; otherwise false.</returns>
        static bool ReadColliderIsTrigger(SceneComponentAssetRecord record) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            if (IsSceneComponentType(record, BoxCollider3DComponentTypeId)) {
                return ReadBoxColliderIsTrigger(record);
            } else if (IsSceneComponentType(record, SphereCollider3DComponentTypeId)) {
                return ReadSphereColliderIsTrigger(record);
            } else if (IsSceneComponentType(record, CapsuleCollider3DComponentTypeId)) {
                return ReadCapsuleColliderIsTrigger(record);
            } else if (IsSceneComponentType(record, StaticMeshCollider3DComponentTypeId)) {
                return ReadStaticMeshColliderIsTrigger(record);
            }

            throw new InvalidOperationException($"Collider trigger analysis does not support component type '{record.ComponentTypeId}'.");
        }

        /// <summary>
        /// Reads the trigger flag from one serialized box-collider scene component record.
        /// </summary>
        /// <param name="record">Serialized box-collider scene component record.</param>
        /// <returns>True when the collider is configured as a trigger; otherwise false.</returns>
        static bool ReadBoxColliderIsTrigger(SceneComponentAssetRecord record) {
            byte[] payload = record.Payload ?? Array.Empty<byte>();
            using MemoryStream stream = new MemoryStream(payload, false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version == CurrentBoxColliderPayloadVersion) {
                reader.ReadFloat3();
                reader.ReadUInt16();
                reader.ReadUInt16();
                return reader.ReadByte() != 0;
            }
            if (version == LegacyBoxColliderPayloadVersion && TryReadAutomaticMemberCount(payload, AutomaticBoxColliderMemberCount)) {
                reader.ReadInt32();
                reader.ReadUInt16();
                reader.ReadUInt16();
                reader.ReadDouble();
                return reader.ReadByte() != 0;
            }
            if (version != LegacyBoxColliderPayloadVersion) {
                throw new InvalidOperationException($"Unsupported box collider component payload version '{version}'.");
            }

            reader.ReadFloat3();
            return false;
        }

        /// <summary>
        /// Reads the trigger flag from one serialized sphere-collider scene component record.
        /// </summary>
        /// <param name="record">Serialized sphere-collider scene component record.</param>
        /// <returns>True when the collider is configured as a trigger; otherwise false.</returns>
        static bool ReadSphereColliderIsTrigger(SceneComponentAssetRecord record) {
            byte[] payload = record.Payload ?? Array.Empty<byte>();
            using MemoryStream stream = new MemoryStream(payload, false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != 1) {
                throw new InvalidOperationException($"Unsupported sphere collider component payload version '{version}'.");
            }

            if (TryReadAutomaticMemberCount(payload, AutomaticSphereColliderMemberCount)) {
                reader.ReadInt32();
                reader.ReadUInt16();
                reader.ReadUInt16();
                reader.ReadDouble();
                return reader.ReadByte() != 0;
            }

            reader.ReadSingle();
            return false;
        }

        /// <summary>
        /// Reads the trigger flag from one serialized capsule-collider scene component record.
        /// </summary>
        /// <param name="record">Serialized capsule-collider scene component record.</param>
        /// <returns>True when the collider is configured as a trigger; otherwise false.</returns>
        static bool ReadCapsuleColliderIsTrigger(SceneComponentAssetRecord record) {
            byte[] payload = record.Payload ?? Array.Empty<byte>();
            using MemoryStream stream = new MemoryStream(payload, false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != 1) {
                throw new InvalidOperationException($"Unsupported capsule collider component payload version '{version}'.");
            }

            if (TryReadAutomaticMemberCount(payload, AutomaticCapsuleColliderMemberCount)) {
                reader.ReadInt32();
                reader.ReadUInt16();
                reader.ReadUInt16();
                reader.ReadDouble();
                reader.ReadSingle();
                return reader.ReadByte() != 0;
            }

            reader.ReadSingle();
            reader.ReadSingle();
            return false;
        }

        /// <summary>
        /// Attempts to read the automatic reflected member-count header that follows one legacy-compatible version byte.
        /// </summary>
        /// <param name="payload">Serialized component payload whose leading member-count header should be inspected.</param>
        /// <param name="expectedMemberCount">Expected reflected member count for the automatic payload layout.</param>
        /// <returns>True when the payload uses the automatic reflected layout; otherwise false.</returns>
        static bool TryReadAutomaticMemberCount(byte[] payload, int expectedMemberCount) {
            if (payload == null) {
                throw new ArgumentNullException(nameof(payload));
            }
            if (payload.Length < 5) {
                return false;
            }

            int memberCount =
                payload[1]
                | (payload[2] << 8)
                | (payload[3] << 16)
                | (payload[4] << 24);
            return memberCount == expectedMemberCount;
        }

        /// <summary>
        /// Reads the trigger flag from one serialized static-mesh-collider scene component record.
        /// </summary>
        /// <param name="record">Serialized static-mesh-collider scene component record.</param>
        /// <returns>True when the collider is configured as a trigger; otherwise false.</returns>
        static bool ReadStaticMeshColliderIsTrigger(SceneComponentAssetRecord record) {
            byte[] payload = record.Payload ?? Array.Empty<byte>();
            using MemoryStream stream = new MemoryStream(payload, false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != 1) {
                throw new InvalidOperationException($"Unsupported static mesh collider component payload version '{version}'.");
            }

            if (TryReadAutomaticMemberCount(payload, AutomaticStaticMeshColliderMemberCount)) {
                reader.ReadInt32();
                SkipAutomaticStaticMeshCollisionData(reader);
                reader.ReadUInt16();
                reader.ReadUInt16();
                SkipAutomaticStaticMeshCookedRuntimeData(reader);
                reader.ReadDouble();
                return reader.ReadByte() != 0;
            }

            int vertexCount = reader.ReadInt32();
            for (int index = 0; index < vertexCount; index++) {
                reader.ReadFloat3();
            }

            int indexCount = reader.ReadInt32();
            for (int index = 0; index < indexCount; index++) {
                reader.ReadInt32();
            }

            return false;
        }

        /// <summary>
        /// Skips one reflected static-mesh collision-data payload written through automatic component persistence.
        /// </summary>
        /// <param name="reader">Reader positioned at the collision-data member payload.</param>
        static void SkipAutomaticStaticMeshCollisionData(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            if (reader.ReadByte() == 0) {
                return;
            }

            SkipAutomaticIntArray(reader);
            SkipAutomaticFloat3Array(reader);
        }

        /// <summary>
        /// Skips one reflected optional cooked static-mesh runtime payload written through automatic component persistence.
        /// </summary>
        /// <param name="reader">Reader positioned at the cooked-runtime-data member payload.</param>
        static void SkipAutomaticStaticMeshCookedRuntimeData(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            if (reader.ReadByte() == 0) {
                return;
            }

            SkipAutomaticEngineSerializedPayload(reader);
        }

        /// <summary>
        /// Skips one engine-owned serialized payload written through automatic component persistence.
        /// </summary>
        /// <param name="reader">Reader positioned at the serialized-payload member payload.</param>
        static void SkipAutomaticEngineSerializedPayload(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            if (reader.ReadByte() == 0) {
                return;
            }

            reader.ReadString();
            SkipAutomaticByteArray(reader);
        }

        /// <summary>
        /// Skips one reflected integer array payload written through automatic component persistence.
        /// </summary>
        /// <param name="reader">Reader positioned at the integer-array payload.</param>
        static void SkipAutomaticIntArray(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            int length = reader.ReadInt32();
            if (length == -1) {
                return;
            }
            if (length < -1) {
                throw new InvalidOperationException("Array length cannot be negative.");
            }

            for (int index = 0; index < length; index++) {
                reader.ReadInt32();
            }
        }

        /// <summary>
        /// Skips one reflected byte array payload written through automatic component persistence.
        /// </summary>
        /// <param name="reader">Reader positioned at the byte-array payload.</param>
        static void SkipAutomaticByteArray(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            int length = reader.ReadInt32();
            if (length == -1) {
                return;
            }
            if (length < -1) {
                throw new InvalidOperationException("Array length cannot be negative.");
            }

            for (int index = 0; index < length; index++) {
                reader.ReadByte();
            }
        }

        /// <summary>
        /// Skips one reflected <see cref="float3"/> array payload written through automatic component persistence.
        /// </summary>
        /// <param name="reader">Reader positioned at the <see cref="float3"/> array payload.</param>
        static void SkipAutomaticFloat3Array(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            int length = reader.ReadInt32();
            if (length == -1) {
                return;
            }
            if (length < -1) {
                throw new InvalidOperationException("Array length cannot be negative.");
            }

            for (int index = 0; index < length; index++) {
                reader.ReadFloat3();
            }
        }

        /// <summary>
        /// Determines whether one serialized component record matches the supplied stable type id.
        /// </summary>
        /// <param name="record">Serialized component record to inspect.</param>
        /// <param name="componentTypeId">Stable serialized type id to compare against.</param>
        /// <returns>True when the record matches the supplied type id; otherwise false.</returns>
        static bool IsSceneComponentType(SceneComponentAssetRecord record, string componentTypeId) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                throw new ArgumentException("Component type id must be provided.", nameof(componentTypeId));
            }

            return string.Equals(record.ComponentTypeId, componentTypeId, StringComparison.OrdinalIgnoreCase);
        }
    }
}
