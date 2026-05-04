namespace helengine.editor {
    /// <summary>
    /// Enumerates the exportable physics validation scenes authored for end-to-end runtime testing.
    /// </summary>
    public static class PhysicsValidationSceneCatalog {
        /// <summary>
        /// Relative scene id for the character slope validation scene.
        /// </summary>
        public const string CharacterSlopeSceneId = "scenes/physics/test_scene_character_slope.helen";

        /// <summary>
        /// Relative scene id for the character steps validation scene.
        /// </summary>
        public const string CharacterStepsSceneId = "scenes/physics/test_scene_character_steps.helen";

        /// <summary>
        /// Relative scene id for the character moving-platform validation scene.
        /// </summary>
        public const string CharacterMovingPlatformSceneId = "scenes/physics/test_scene_character_moving_platform.helen";

        /// <summary>
        /// Relative scene id for the stacked dynamic-body validation scene.
        /// </summary>
        public const string DynamicStackBoxesSceneId = "scenes/physics/test_scene_dynamic_stack_boxes.helen";

        /// <summary>
        /// Relative scene id for the sphere-ramp validation scene.
        /// </summary>
        public const string DynamicSphereRampSceneId = "scenes/physics/test_scene_dynamic_sphere_ramp.helen";

        /// <summary>
        /// Relative scene id for the kinematic push validation scene.
        /// </summary>
        public const string KinematicPushSceneId = "scenes/physics/test_scene_kinematic_push.helen";

        /// <summary>
        /// Relative scene id for the static-mesh ground stability validation scene.
        /// </summary>
        public const string MeshGroundStabilitySceneId = "scenes/physics/test_scene_mesh_ground_stability.helen";

        /// <summary>
        /// Relative scene id for the trigger-volume validation scene.
        /// </summary>
        public const string TriggerVolumeSceneId = "scenes/physics/test_scene_trigger_volume.helen";

        /// <summary>
        /// Stable ordered list of authored physics validation scene ids.
        /// </summary>
        static readonly string[] SceneIds = new[] {
            CharacterSlopeSceneId,
            CharacterStepsSceneId,
            CharacterMovingPlatformSceneId,
            DynamicStackBoxesSceneId,
            DynamicSphereRampSceneId,
            KinematicPushSceneId,
            MeshGroundStabilitySceneId,
            TriggerVolumeSceneId
        };

        /// <summary>
        /// Gets the stable ordered list of exportable physics validation scene ids.
        /// </summary>
        /// <returns>Ordered scene ids used by validation tooling and generated demo content.</returns>
        public static string[] GetSceneIds() {
            string[] copy = new string[SceneIds.Length];
            Array.Copy(SceneIds, copy, SceneIds.Length);
            return copy;
        }
    }
}
