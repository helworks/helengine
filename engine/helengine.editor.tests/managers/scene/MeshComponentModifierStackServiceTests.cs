namespace helengine.editor.tests {
    /// <summary>
    /// Verifies MeshComponent modifier stacks round-trip, inherit from the common scope, and honor legacy tessellation members.
    /// </summary>
    public sealed class MeshComponentModifierStackServiceTests {
        /// <summary>
        /// Ensures an authored stack round-trips through one scope with its per-entry parameters intact.
        /// </summary>
        [Fact]
        public void SetStack_WithTessellateEntry_RoundTripsThroughScope() {
            MeshComponentModifierStackService service = new MeshComponentModifierStackService();
            EntityComponentSaveState saveState = new EntityComponentSaveState();

            service.SetStack(saveState, "ps2", [
                new MeshComponentModifier(MeshComponentModifier.TessellateKind) {
                    MaxEdgeLength = 0.5,
                    AtCookTime = true
                }
            ]);

            List<MeshComponentModifier> stack = service.TryGetAuthoredStack(saveState, "ps2");
            Assert.NotNull(stack);
            MeshComponentModifier modifier = Assert.Single(stack);
            Assert.Equal(MeshComponentModifier.TessellateKind, modifier.Kind);
            Assert.Equal(0.5, modifier.MaxEdgeLength);
            Assert.True(modifier.AtCookTime);
        }

        /// <summary>
        /// Ensures platforms without an authored stack inherit the common-scope stack.
        /// </summary>
        [Fact]
        public void ResolveEffectiveStack_WithCommonStackOnly_InheritsOnEveryPlatform() {
            MeshComponentModifierStackService service = new MeshComponentModifierStackService();
            EntityComponentSaveState saveState = new EntityComponentSaveState();

            service.SetStack(saveState, ComponentPlatformEditingService.CommonPlatformId, [
                new MeshComponentModifier(MeshComponentModifier.TessellateKind) { MaxEdgeLength = 2.0 }
            ]);

            List<MeshComponentModifier> effectiveStack = service.ResolveEffectiveStack(saveState, "gamecube");
            MeshComponentModifier modifier = Assert.Single(effectiveStack);
            Assert.Equal(MeshComponentModifier.TessellateKind, modifier.Kind);
            Assert.Equal(2.0, modifier.MaxEdgeLength);
        }

        /// <summary>
        /// Ensures platform-authored additions append after the inherited common-scope stack.
        /// </summary>
        [Fact]
        public void ResolveEffectiveStack_WithCommonStackAndPlatformAdditions_AppendsPlatformEntries() {
            MeshComponentModifierStackService service = new MeshComponentModifierStackService();
            EntityComponentSaveState saveState = new EntityComponentSaveState();

            service.SetStack(saveState, ComponentPlatformEditingService.CommonPlatformId, [
                new MeshComponentModifier(MeshComponentModifier.UvwMapKind) { UvwMode = ModelUvwMapProcessor.WorldMode, UvwAxisX = ModelUvwMapProcessor.AxisX, UvwAxisY = ModelUvwMapProcessor.AxisZ, UvwScaleX = 2.0, UvwScaleY = 3.0 }
            ]);
            service.SetStack(saveState, "ps2", [
                new MeshComponentModifier(MeshComponentModifier.TessellateKind) { MaxEdgeLength = 1.0 }
            ]);

            List<MeshComponentModifier> ps2Stack = service.ResolveEffectiveStack(saveState, "ps2");
            Assert.Equal(2, ps2Stack.Count);
            Assert.Equal(MeshComponentModifier.UvwMapKind, ps2Stack[0].Kind);
            Assert.Equal(MeshComponentModifier.TessellateKind, ps2Stack[1].Kind);

            List<MeshComponentModifier> windowsStack = service.ResolveEffectiveStack(saveState, "windows");
            MeshComponentModifier windowsModifier = Assert.Single(windowsStack);
            Assert.Equal(MeshComponentModifier.UvwMapKind, windowsModifier.Kind);
            Assert.Equal(2.0, windowsModifier.UvwScaleX);
            Assert.Equal(3.0, windowsModifier.UvwScaleY);
            Assert.Equal(ModelUvwMapProcessor.AxisX, windowsModifier.UvwAxisX);
            Assert.Equal(ModelUvwMapProcessor.AxisZ, windowsModifier.UvwAxisY);
        }

        /// <summary>
        /// Ensures legacy per-platform tessellation members read as one platform-authored tessellation modifier.
        /// </summary>
        [Fact]
        public void ResolveEffectiveStack_WithLegacyTessellationMembers_MapsToTessellateModifier() {
            MeshComponentModifierStackService service = new MeshComponentModifierStackService();
            MeshComponentTessellationSettingsService legacyService = new MeshComponentTessellationSettingsService();
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            legacyService.SetForPlatform(saveState, "ps2", new MeshComponentTessellationSettings(true, 0.75));

            List<MeshComponentModifier> effectiveStack = service.ResolveEffectiveStack(saveState, "ps2");
            MeshComponentModifier modifier = Assert.Single(effectiveStack);
            Assert.Equal(MeshComponentModifier.TessellateKind, modifier.Kind);
            Assert.Equal(0.75, modifier.MaxEdgeLength);
        }

        /// <summary>
        /// Ensures a common-scope tessellation modifier lowers into legacy-compatible tessellation settings for cooking.
        /// </summary>
        [Fact]
        public void TryResolveTessellationSettings_WithCommonStack_LowersToLegacySettings() {
            MeshComponentModifierStackService service = new MeshComponentModifierStackService();
            EntityComponentSaveState saveState = new EntityComponentSaveState();

            service.SetStack(saveState, ComponentPlatformEditingService.CommonPlatformId, [
                new MeshComponentModifier(MeshComponentModifier.TessellateKind) { MaxEdgeLength = 1.25, AtCookTime = true }
            ]);

            MeshComponentTessellationSettings settings = service.TryResolveTessellationSettings(saveState, "psp");
            Assert.NotNull(settings);
            Assert.True(settings.Tessellate);
            Assert.Equal(1.25, settings.TessellationMaxEdgeLength);
            Assert.True(settings.TessellateAtCookTime);
        }

        /// <summary>
        /// Ensures platforms without any authored or inherited modifiers resolve no tessellation settings.
        /// </summary>
        [Fact]
        public void TryResolveTessellationSettings_WithoutModifiers_ReturnsNull() {
            MeshComponentModifierStackService service = new MeshComponentModifierStackService();
            Assert.Null(service.TryResolveTessellationSettings(new EntityComponentSaveState(), "ps2"));
        }
    }
}
