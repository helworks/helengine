using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies level-order validation, path-shape checks and relocation planning.
    /// </summary>
    public sealed class EditorOverrideLevelOrderTests {
        static readonly EditorOverrideScopeStep Handheld = new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Group, "handheld");
        static readonly EditorOverrideScopeStep Portable = new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Group, "portable");
        static readonly EditorOverrideScopeStep Ds = new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Platform, "ds");
        static readonly EditorOverrideScopeStep Debug = new EditorOverrideScopeStep(SceneOverrideScopeStepKind.BuildConfig, "debug");

        [Fact]
        public void Default_IsPlatformThenBuildConfig() {
            Assert.Equal(new[] { SceneOverrideScopeStepKind.Platform, SceneOverrideScopeStepKind.BuildConfig }, EditorOverrideLevelOrder.Default);
        }

        [Fact]
        public void Validate_RejectsARepeatedKind() {
            Assert.Throws<InvalidOperationException>(() => EditorOverrideLevelOrder.Validate(new[] { SceneOverrideScopeStepKind.Platform, SceneOverrideScopeStepKind.Platform }));
            EditorOverrideLevelOrder.Validate(Array.Empty<SceneOverrideScopeStepKind>());
        }

        [Fact]
        public void IsValidPath_AcceptsPrefixesAndGroupChainsAndSkipsAnEmptyGroupLevel() {
            SceneOverrideScopeStepKind[] order = { SceneOverrideScopeStepKind.Group, SceneOverrideScopeStepKind.Platform, SceneOverrideScopeStepKind.BuildConfig };

            Assert.True(EditorOverrideLevelOrder.IsValidPath(order, EditorOverrideScope.Common));
            Assert.True(EditorOverrideLevelOrder.IsValidPath(order, EditorOverrideScope.Common.Append(Handheld).Append(Portable).Append(Ds).Append(Debug)));
            Assert.True(EditorOverrideLevelOrder.IsValidPath(order, EditorOverrideScope.Common.Append(Ds)));
            Assert.False(EditorOverrideLevelOrder.IsValidPath(order, EditorOverrideScope.Common.Append(Debug)));
            Assert.False(EditorOverrideLevelOrder.IsValidPath(order, EditorOverrideScope.Common.Append(Ds).Append(Handheld)));
            Assert.False(EditorOverrideLevelOrder.IsValidPath(Array.Empty<SceneOverrideScopeStepKind>(), EditorOverrideScope.Common.Append(Ds)));
        }

        [Fact]
        public void Plan_RelocatesReorderedStepsAndDropsPathsWhoseKindWasRemoved() {
            SceneOverrideScopeStepKind[] newOrder = { SceneOverrideScopeStepKind.BuildConfig, SceneOverrideScopeStepKind.Platform };
            EditorOverrideScope dsDebug = EditorOverrideScope.Common.Append(Ds).Append(Debug);
            EditorOverrideScope handheldDs = EditorOverrideScope.Common.Append(Handheld).Append(Ds);
            EditorOverrideScope dsOnly = EditorOverrideScope.Common.Append(Ds);

            EditorOverrideScopeRelocationPlan plan = EditorOverrideScopeRelocationPlanner.Plan(newOrder, new[] { dsDebug, handheldDs, dsOnly, EditorOverrideScope.Common });

            EditorOverrideScopeRelocation relocated = Assert.Single(plan.Relocated, item => item.Source == dsDebug);
            Assert.Equal(EditorOverrideScope.Common.Append(Debug).Append(Ds), relocated.Target);
            Assert.Contains(plan.Dropped, item => item == handheldDs);
            Assert.Contains(plan.Dropped, item => item == dsOnly);
            Assert.DoesNotContain(plan.Dropped, item => item.IsCommon);
            Assert.True(plan.HasDrops);
        }

        [Fact]
        public void Apply_MovesExistenceTransformAndComponentEntriesAndDeletesDrops() {
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            EditorOverrideScope dsDebug = EditorOverrideScope.Common.Append(Ds).Append(Debug);
            EditorOverrideScope debugDs = EditorOverrideScope.Common.Append(Debug).Append(Ds);
            EditorOverrideScope handheldDs = EditorOverrideScope.Common.Append(Handheld).Append(Ds);
            saveComponent.SetExistencePlatformOverride(dsDebug, new SceneEntityPlatformExistenceOverrideAsset { Exists = false });
            saveComponent.SetTransformPlatformOverride(handheldDs, new SceneEntityPlatformTransformOverrideAsset { HasLocalScaleOverride = true, LocalScale = float3.One });
            saveComponent.GetOrCreateComponentPlatformOverride(dsDebug).MarkComponentRemoved("k");
            EditorOverrideScopeRelocationPlan plan = EditorOverrideScopeRelocationPlanner.Plan(
                new[] { SceneOverrideScopeStepKind.BuildConfig, SceneOverrideScopeStepKind.Platform },
                new[] { dsDebug, handheldDs });

            EditorOverrideScopeRelocationPlanner.Apply(saveComponent, plan);

            Assert.False(saveComponent.TryGetExistencePlatformOverride(dsDebug, out _));
            Assert.True(saveComponent.TryGetExistencePlatformOverride(debugDs, out SceneEntityPlatformExistenceOverrideAsset moved));
            Assert.Equal("buildconfig:debug/platform:ds", SceneOverrideScopePath.Format(moved.Scope));
            Assert.False(saveComponent.TryGetTransformPlatformOverride(handheldDs, out _));
            Assert.True(saveComponent.TryGetComponentPlatformOverride(debugDs, out EntityPlatformComponentOverrideState component));
            Assert.Equal(debugDs, component.Scope);
            Assert.Equal(new[] { SceneOverrideScopeStepKind.BuildConfig, SceneOverrideScopeStepKind.Platform }, saveComponent.OverrideLevelOrder);
        }
    }
}
