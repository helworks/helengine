namespace helengine.editor {
    /// <summary>
    /// Defines the stable scene ids used by exported physics validation scenes.
    /// </summary>
    public static class PhysicsValidationSceneCatalog {
        /// <summary>
        /// Stable scene id for the character slope validation scene.
        /// </summary>
        public const string CharacterSlopeSceneId = "Scenes/physics/character-slope.helen";
        /// <summary>
        /// Stable scene id for the character steps validation scene.
        /// </summary>
        public const string CharacterStepsSceneId = "Scenes/physics/character-steps.helen";
        /// <summary>
        /// Stable scene id for the moving-platform character validation scene.
        /// </summary>
        public const string CharacterMovingPlatformSceneId = "Scenes/physics/character-moving-platform.helen";
        /// <summary>
        /// Stable scene id for the dynamic box-stack validation scene.
        /// </summary>
        public const string DynamicStackBoxesSceneId = "Scenes/physics/dynamic-stack-boxes.helen";
        /// <summary>
        /// Stable scene id for the dynamic sphere ramp validation scene.
        /// </summary>
        public const string DynamicSphereRampSceneId = "Scenes/physics/dynamic-sphere-ramp.helen";
        /// <summary>
        /// Stable scene id for the kinematic push validation scene.
        /// </summary>
        public const string KinematicPushSceneId = "Scenes/physics/kinematic-push.helen";
        /// <summary>
        /// Stable scene id for the mesh-ground stability validation scene.
        /// </summary>
        public const string MeshGroundStabilitySceneId = "Scenes/physics/mesh-ground-stability.helen";
        /// <summary>
        /// Stable scene id for the trigger-volume validation scene.
        /// </summary>
        public const string TriggerVolumeSceneId = "Scenes/physics/trigger-volume.helen";

        /// <summary>
        /// Returns every stable physics validation scene id in export order.
        /// </summary>
        /// <returns>Ordered physics validation scene ids.</returns>
        public static string[] GetSceneIds() {
            return new[] {
                CharacterSlopeSceneId,
                CharacterStepsSceneId,
                CharacterMovingPlatformSceneId,
                DynamicStackBoxesSceneId,
                DynamicSphereRampSceneId,
                KinematicPushSceneId,
                MeshGroundStabilitySceneId,
                TriggerVolumeSceneId
            };
        }
    }
}
