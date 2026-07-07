namespace helengine {
    /// <summary>
    /// Maps 3D physics scene feature flags to stable preprocessor symbols used by generated-code stripping.
    /// </summary>
    public static class PhysicsSceneFeatureSymbolCatalog3D {
        /// <summary>
        /// Master symbol that switches the 3D physics runtime into scene-feature stripping mode.
        /// </summary>
        public const string SceneFeatureStrippingSymbol = "HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES";

        /// <summary>
        /// Stable generic runtime feature id used when kinematic motion is required.
        /// </summary>
        public const string KinematicMotionFeatureId = "physics3d.kinematic_motion";

        /// <summary>
        /// Stable generic runtime feature id used when trigger events are required.
        /// </summary>
        public const string TriggerEventsFeatureId = "physics3d.trigger_events";

        /// <summary>
        /// Stable generic runtime feature id used when character controller support is required.
        /// </summary>
        public const string CharacterControllerFeatureId = "physics3d.character_controller";

        /// <summary>
        /// Stable generic runtime feature id used when box-to-box contact is required.
        /// </summary>
        public const string BoxBoxContactFeatureId = "physics3d.box_box_contact";

        /// <summary>
        /// Stable generic runtime feature id used when sphere-to-sphere contact is required.
        /// </summary>
        public const string SphereSphereContactFeatureId = "physics3d.sphere_sphere_contact";

        /// <summary>
        /// Stable generic runtime feature id used when sphere-to-box contact is required.
        /// </summary>
        public const string SphereBoxContactFeatureId = "physics3d.sphere_box_contact";

        /// <summary>
        /// Stable generic runtime feature id used when capsule-to-box contact is required.
        /// </summary>
        public const string CapsuleBoxContactFeatureId = "physics3d.capsule_box_contact";

        /// <summary>
        /// Stable generic runtime feature id used when capsule-to-sphere contact is required.
        /// </summary>
        public const string CapsuleSphereContactFeatureId = "physics3d.capsule_sphere_contact";

        /// <summary>
        /// Stable generic runtime feature id used when capsule-to-capsule contact is required.
        /// </summary>
        public const string CapsuleCapsuleContactFeatureId = "physics3d.capsule_capsule_contact";

        /// <summary>
        /// Stable generic runtime feature id used when box-to-static-mesh contact is required.
        /// </summary>
        public const string BoxStaticMeshContactFeatureId = "physics3d.box_static_mesh_contact";

        /// <summary>
        /// Stable generic runtime feature id used when sphere-to-static-mesh contact is required.
        /// </summary>
        public const string SphereStaticMeshContactFeatureId = "physics3d.sphere_static_mesh_contact";

        /// <summary>
        /// Stable generic runtime feature id used when capsule-to-static-mesh contact is required.
        /// </summary>
        public const string CapsuleStaticMeshContactFeatureId = "physics3d.capsule_static_mesh_contact";

        /// <summary>
        /// Stable generic runtime feature id used when character controllers require rigid-body support queries.
        /// </summary>
        public const string CharacterControllerBodySupportFeatureId = "physics3d.character_controller_body_support";

        /// <summary>
        /// Stable generic runtime feature id used when character controllers require cooked static-mesh support queries.
        /// </summary>
        public const string CharacterControllerStaticMeshSupportFeatureId = "physics3d.character_controller_static_mesh_support";

        /// <summary>
        /// Symbol used when kinematic motion is required.
        /// </summary>
        public const string KinematicMotionSymbol = "HELENGINE_PHYSICS3D_FEATURE_KINEMATIC_MOTION";

        /// <summary>
        /// Symbol used when trigger events are required.
        /// </summary>
        public const string TriggerEventsSymbol = "HELENGINE_PHYSICS3D_FEATURE_TRIGGER_EVENTS";

        /// <summary>
        /// Symbol used when character controller support is required.
        /// </summary>
        public const string CharacterControllerSymbol = "HELENGINE_PHYSICS3D_FEATURE_CHARACTER_CONTROLLER";

        /// <summary>
        /// Symbol used when box-to-box contact is required.
        /// </summary>
        public const string BoxBoxContactSymbol = "HELENGINE_PHYSICS3D_FEATURE_BOX_BOX_CONTACT";

        /// <summary>
        /// Symbol used when sphere-to-sphere contact is required.
        /// </summary>
        public const string SphereSphereContactSymbol = "HELENGINE_PHYSICS3D_FEATURE_SPHERE_SPHERE_CONTACT";

        /// <summary>
        /// Symbol used when sphere-to-box contact is required.
        /// </summary>
        public const string SphereBoxContactSymbol = "HELENGINE_PHYSICS3D_FEATURE_SPHERE_BOX_CONTACT";

        /// <summary>
        /// Symbol used when capsule-to-box contact is required.
        /// </summary>
        public const string CapsuleBoxContactSymbol = "HELENGINE_PHYSICS3D_FEATURE_CAPSULE_BOX_CONTACT";

        /// <summary>
        /// Symbol used when capsule-to-sphere contact is required.
        /// </summary>
        public const string CapsuleSphereContactSymbol = "HELENGINE_PHYSICS3D_FEATURE_CAPSULE_SPHERE_CONTACT";

        /// <summary>
        /// Symbol used when capsule-to-capsule contact is required.
        /// </summary>
        public const string CapsuleCapsuleContactSymbol = "HELENGINE_PHYSICS3D_FEATURE_CAPSULE_CAPSULE_CONTACT";

        /// <summary>
        /// Symbol used when box-to-static-mesh contact is required.
        /// </summary>
        public const string BoxStaticMeshContactSymbol = "HELENGINE_PHYSICS3D_FEATURE_BOX_STATIC_MESH_CONTACT";

        /// <summary>
        /// Symbol used when sphere-to-static-mesh contact is required.
        /// </summary>
        public const string SphereStaticMeshContactSymbol = "HELENGINE_PHYSICS3D_FEATURE_SPHERE_STATIC_MESH_CONTACT";

        /// <summary>
        /// Symbol used when capsule-to-static-mesh contact is required.
        /// </summary>
        public const string CapsuleStaticMeshContactSymbol = "HELENGINE_PHYSICS3D_FEATURE_CAPSULE_STATIC_MESH_CONTACT";

        /// <summary>
        /// Symbol used when character controllers require rigid-body support queries.
        /// </summary>
        public const string CharacterControllerBodySupportSymbol = "HELENGINE_PHYSICS3D_FEATURE_CHARACTER_CONTROLLER_BODY_SUPPORT";

        /// <summary>
        /// Symbol used when character controllers require cooked static-mesh support queries.
        /// </summary>
        public const string CharacterControllerStaticMeshSupportSymbol = "HELENGINE_PHYSICS3D_FEATURE_CHARACTER_CONTROLLER_STATIC_MESH_SUPPORT";

        /// <summary>
        /// Builds the ordered preprocessor symbol list required to strip the runtime down to the supplied scene feature set.
        /// </summary>
        /// <param name="featureFlags">Unioned scene feature flags that describe the required runtime behavior.</param>
        /// <returns>Ordered unique preprocessor symbols for the requested feature set.</returns>
        public static IReadOnlyList<string> BuildSymbols(PhysicsSceneFeatureFlags3D featureFlags) {
            List<string> symbols = new List<string> {
                SceneFeatureStrippingSymbol
            };

            AddSymbolIfEnabled(symbols, featureFlags, PhysicsSceneFeatureFlags3D.KinematicMotion, KinematicMotionSymbol);
            AddSymbolIfEnabled(symbols, featureFlags, PhysicsSceneFeatureFlags3D.TriggerEvents, TriggerEventsSymbol);
            AddSymbolIfEnabled(symbols, featureFlags, PhysicsSceneFeatureFlags3D.CharacterController, CharacterControllerSymbol);
            AddSymbolIfEnabled(symbols, featureFlags, PhysicsSceneFeatureFlags3D.BoxBoxContact, BoxBoxContactSymbol);
            AddSymbolIfEnabled(symbols, featureFlags, PhysicsSceneFeatureFlags3D.SphereSphereContact, SphereSphereContactSymbol);
            AddSymbolIfEnabled(symbols, featureFlags, PhysicsSceneFeatureFlags3D.SphereBoxContact, SphereBoxContactSymbol);
            AddSymbolIfEnabled(symbols, featureFlags, PhysicsSceneFeatureFlags3D.CapsuleBoxContact, CapsuleBoxContactSymbol);
            AddSymbolIfEnabled(symbols, featureFlags, PhysicsSceneFeatureFlags3D.CapsuleSphereContact, CapsuleSphereContactSymbol);
            AddSymbolIfEnabled(symbols, featureFlags, PhysicsSceneFeatureFlags3D.CapsuleCapsuleContact, CapsuleCapsuleContactSymbol);
            AddSymbolIfEnabled(symbols, featureFlags, PhysicsSceneFeatureFlags3D.BoxStaticMeshContact, BoxStaticMeshContactSymbol);
            AddSymbolIfEnabled(symbols, featureFlags, PhysicsSceneFeatureFlags3D.SphereStaticMeshContact, SphereStaticMeshContactSymbol);
            AddSymbolIfEnabled(symbols, featureFlags, PhysicsSceneFeatureFlags3D.CapsuleStaticMeshContact, CapsuleStaticMeshContactSymbol);
            AddSymbolIfEnabled(symbols, featureFlags, PhysicsSceneFeatureFlags3D.CharacterControllerBodySupport, CharacterControllerBodySupportSymbol);
            AddSymbolIfEnabled(symbols, featureFlags, PhysicsSceneFeatureFlags3D.CharacterControllerStaticMeshSupport, CharacterControllerStaticMeshSupportSymbol);

            return symbols;
        }

        /// <summary>
        /// Builds the ordered generic runtime feature ids required by the supplied scene feature set.
        /// </summary>
        /// <param name="featureFlags">Unioned scene feature flags that describe the required runtime behavior.</param>
        /// <returns>Ordered generic runtime feature ids for the requested feature set.</returns>
        public static IReadOnlyList<string> BuildRuntimeFeatureIds(PhysicsSceneFeatureFlags3D featureFlags) {
            List<string> featureIds = [];

            AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.KinematicMotion, KinematicMotionFeatureId);
            AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.TriggerEvents, TriggerEventsFeatureId);
            AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.CharacterController, CharacterControllerFeatureId);
            AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.BoxBoxContact, BoxBoxContactFeatureId);
            AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.SphereSphereContact, SphereSphereContactFeatureId);
            AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.SphereBoxContact, SphereBoxContactFeatureId);
            AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.CapsuleBoxContact, CapsuleBoxContactFeatureId);
            AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.CapsuleSphereContact, CapsuleSphereContactFeatureId);
            AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.CapsuleCapsuleContact, CapsuleCapsuleContactFeatureId);
            AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.BoxStaticMeshContact, BoxStaticMeshContactFeatureId);
            AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.SphereStaticMeshContact, SphereStaticMeshContactFeatureId);
            AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.CapsuleStaticMeshContact, CapsuleStaticMeshContactFeatureId);
            AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.CharacterControllerBodySupport, CharacterControllerBodySupportFeatureId);
            AddFeatureIdIfEnabled(featureIds, featureFlags, PhysicsSceneFeatureFlags3D.CharacterControllerStaticMeshSupport, CharacterControllerStaticMeshSupportFeatureId);

            return featureIds;
        }

        /// <summary>
        /// Adds one symbol to the output list when the supplied feature bit is enabled.
        /// </summary>
        /// <param name="symbols">Mutable output symbol list.</param>
        /// <param name="featureFlags">Feature flags being translated.</param>
        /// <param name="requiredFeature">Feature bit that controls the supplied symbol.</param>
        /// <param name="symbol">Stable preprocessor symbol to add.</param>
        static void AddSymbolIfEnabled(
            List<string> symbols,
            PhysicsSceneFeatureFlags3D featureFlags,
            PhysicsSceneFeatureFlags3D requiredFeature,
            string symbol) {
            if (symbols == null) {
                throw new ArgumentNullException(nameof(symbols));
            }
            if (string.IsNullOrWhiteSpace(symbol)) {
                throw new ArgumentException("Symbol must be provided.", nameof(symbol));
            }

            if (((uint)featureFlags & (uint)requiredFeature) == 0u) {
                return;
            }

            symbols.Add(symbol);
        }

        /// <summary>
        /// Adds one generic runtime feature id to the output list when the supplied feature bit is enabled.
        /// </summary>
        /// <param name="featureIds">Mutable output runtime feature id list.</param>
        /// <param name="featureFlags">Feature flags being translated.</param>
        /// <param name="requiredFeature">Feature bit that controls the supplied runtime feature id.</param>
        /// <param name="featureId">Stable generic runtime feature id to add.</param>
        static void AddFeatureIdIfEnabled(
            List<string> featureIds,
            PhysicsSceneFeatureFlags3D featureFlags,
            PhysicsSceneFeatureFlags3D requiredFeature,
            string featureId) {
            if (featureIds == null) {
                throw new ArgumentNullException(nameof(featureIds));
            }
            if (string.IsNullOrWhiteSpace(featureId)) {
                throw new ArgumentException("Feature id must be provided.", nameof(featureId));
            }

            if (((uint)featureFlags & (uint)requiredFeature) == 0u) {
                return;
            }

            featureIds.Add(featureId);
        }
    }
}
