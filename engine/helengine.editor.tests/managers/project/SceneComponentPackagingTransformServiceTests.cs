using helengine.baseplatform.Definitions;
using helengine.baseplatform.Profiles;
using helengine.baseplatform.Manifest;
using helengine.editor.tests.testing;
using System.Reflection;
using System.Reflection.Emit;

using System.Globalization;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies focused text-component packaging rewrites in the shared scene-component transform service.
    /// </summary>
    public sealed class SceneComponentPackagingTransformServiceTests : IDisposable {
        /// <summary>
        /// Temporary project root used by each transform-service test.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Temporary build root used by each transform-service test.
        /// </summary>
        readonly string BuildRootPath;

        /// <summary>
        /// Initializes one isolated workspace for transform-service verification.
        /// </summary>
        public SceneComponentPackagingTransformServiceTests() {
            string workspaceRootPath = Path.Combine(Path.GetTempPath(), "helengine-transform-service-tests", Guid.NewGuid().ToString("N"));
            ProjectRootPath = workspaceRootPath;
            BuildRootPath = Path.Combine(workspaceRootPath, "Build");
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "cache"));
            Directory.CreateDirectory(BuildRootPath);
        }

        /// <summary>
        /// Deletes the isolated workspace after the test completes.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures flagged text falls back to the normal runtime text payload when build-time sprite conversion is disabled.
        /// </summary>
        [Fact]
        public void TryTransform_WhenTextComponentIsFlagged_KeepsTextComponentPayloadWithoutCallingBakeService() {
            StubTextComponentSpriteBakeService bakeService = new StubTextComponentSpriteBakeService();
            SceneComponentPackagingTransformService service = CreateService(bakeService);
            SceneComponentAssetRecord record = CreateTextRecord(true);

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            Assert.Equal("helengine.TextComponent", transformedRecord.ComponentTypeId);
            Assert.False(bakeService.WasCalled);
        }

        /// <summary>
        /// Ensures unflagged text remains a runtime text component during packaging.
        /// </summary>
        [Fact]
        public void TryTransform_WhenTextComponentIsNotFlagged_KeepsTextComponentPayload() {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            SceneComponentAssetRecord record = CreateTextRecord(false);

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            Assert.Equal("helengine.TextComponent", transformedRecord.ComponentTypeId);
        }

        [Fact]
        public void TryTransform_Cpu_readable_model_reference_GeneratedCube_WritesExactModelCompanion() {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            SceneComponentAssetRecord record = CreateCpuReadableModelReferenceRecord(SceneAssetReferenceTestFactory.CreateEngineCubeModel());

            Assert.True(service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord));

            CpuReadableModelReferenceComponent transformedComponent = DeserializeCpuReadableModelReferenceComponent(transformedRecord);
            Assert.Equal("cooked/cpu-models/engine/cube.hasset", transformedComponent.ModelReference.RelativePath);
            string companionPath = Path.Combine(BuildRootPath, "cooked", "cpu-models", "engine", "cube.hasset");
            Assert.True(File.Exists(companionPath));
            using FileStream stream = new FileStream(companionPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            ModelAsset model = Assert.IsType<ModelAsset>(AssetSerializer.Deserialize(stream));
            Assert.NotEmpty(model.Positions);
            Assert.True((model.Indices16?.Length > 0) ^ (model.Indices32?.Length > 0));
            ModelSubmeshAsset submesh = Assert.Single(model.Submeshes);
            Assert.Equal(0, submesh.IndexStart);
            int activeIndexCount = model.Indices16?.Length > 0 ? model.Indices16.Length : model.Indices32.Length;
            Assert.Equal(activeIndexCount, submesh.IndexCount);
        }

        [Theory]
        [InlineData("plane", "Engine/Models/Plane", "cooked/cpu-models/engine/plane.hasset")]
        [InlineData("sphere", "Engine/Models/Sphere", "cooked/cpu-models/engine/sphere.hasset")]
        public void TryTransform_Cpu_readable_model_reference_GeneratedPrimitive_WritesGenericModelCompanion(
            string primitive,
            string authoredPath,
            string expectedPackagedPath) {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            SceneAssetReference reference = primitive == "plane"
                ? SceneAssetReferenceTestFactory.CreateEnginePlaneModel()
                : SceneAssetReferenceTestFactory.CreateEngineSphereModel();
            SceneComponentAssetRecord record = CreateCpuReadableModelReferenceRecord(reference);

            Assert.True(service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord));

            CpuReadableModelReferenceComponent transformedComponent = DeserializeCpuReadableModelReferenceComponent(transformedRecord);
            Assert.Equal(expectedPackagedPath, transformedComponent.ModelReference.RelativePath);
            Assert.Equal(authoredPath, reference.RelativePath);
            string companionPath = Path.Combine(BuildRootPath, expectedPackagedPath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(companionPath));
            using FileStream stream = new FileStream(companionPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            ModelAsset model = Assert.IsType<ModelAsset>(AssetSerializer.Deserialize(stream));
            Assert.NotEmpty(model.Positions);
            Assert.True((model.Indices16?.Length > 0) ^ (model.Indices32?.Length > 0));
            ModelSubmeshAsset submesh = Assert.Single(model.Submeshes);
            Assert.Equal(0, submesh.IndexStart);
            int activeIndexCount = model.Indices16?.Length > 0 ? model.Indices16.Length : model.Indices32.Length;
            Assert.Equal(activeIndexCount, submesh.IndexCount);
        }

        [Fact]
        public void TryTransform_Cpu_readable_model_reference_UnmarkedReference_WritesNoCpuCompanion() {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            SceneComponentAssetRecord record = CreateUnmarkedModelReferenceRecord(SceneAssetReferenceTestFactory.CreateEngineCubeModel());

            Assert.True(service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord));

            CpuReadableModelUnmarkedReferenceComponent transformedComponent = DeserializeUnmarkedModelReferenceComponent(transformedRecord);
            Assert.Equal("Engine/Models/Cube", transformedComponent.ModelReference.RelativePath);
            Assert.False(Directory.Exists(Path.Combine(BuildRootPath, "cooked", "cpu-models")));
        }

        [Fact]
        public void TryTransform_Cpu_readable_model_reference_InvalidMarkedString_FailsWithDeclaredType() {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            SceneComponentAssetRecord record = CreateInvalidCpuReadableModelReferenceRecord();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                service.TryTransform(record, BuildRootPath, out _));

            Assert.Contains("ModelPath", exception.Message, StringComparison.Ordinal);
            Assert.Contains("SceneAssetReference", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TryTransform_Cpu_readable_model_reference_NullMarkedReference_RemainsNull() {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            SceneComponentAssetRecord record = CreateCpuReadableModelReferenceRecord(null);

            Assert.True(service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord));

            CpuReadableModelReferenceComponent transformedComponent = DeserializeCpuReadableModelReferenceComponent(transformedRecord);
            Assert.Null(transformedComponent.ModelReference);
            Assert.False(Directory.Exists(Path.Combine(BuildRootPath, "cooked", "cpu-models")));
        }

        [Fact]
        public void TryTransform_Cpu_readable_model_reference_RepeatedIdenticalReference_WritesOneCompanion() {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            SceneAssetReference reference = SceneAssetReferenceTestFactory.CreateEngineCubeModel();

            Assert.True(service.TryTransform(CreateCpuReadableModelReferenceRecord(reference), BuildRootPath, out _));
            Assert.True(service.TryTransform(CreateCpuReadableModelReferenceRecord(reference), BuildRootPath, out _));

            string[] companions = Directory.GetFiles(Path.Combine(BuildRootPath, "cooked", "cpu-models"), "*.hasset", SearchOption.AllDirectories);
            Assert.Single(companions);
            Assert.Equal(Path.Combine(BuildRootPath, "cooked", "cpu-models", "engine", "cube.hasset"), companions[0]);
        }

        [Fact]
        public void TryTransform_Cpu_readable_model_reference_SameServiceAfterCompanionDeletion_RecreatesCompanion() {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            SceneAssetReference reference = SceneAssetReferenceTestFactory.CreateEngineCubeModel();
            string companionPath = Path.Combine(BuildRootPath, "cooked", "cpu-models", "engine", "cube.hasset");

            Assert.True(service.TryTransform(CreateCpuReadableModelReferenceRecord(reference), BuildRootPath, out _));
            Assert.True(File.Exists(companionPath));
            File.Delete(companionPath);

            Assert.True(service.TryTransform(CreateCpuReadableModelReferenceRecord(reference), BuildRootPath, out SceneComponentAssetRecord transformedRecord));

            Assert.Equal("cooked/cpu-models/engine/cube.hasset", DeserializeCpuReadableModelReferenceComponent(transformedRecord).ModelReference.RelativePath);
            Assert.True(File.Exists(companionPath));
            AssertValidCpuReadableModelCompanion(companionPath);
        }

        [Fact]
        public void TryTransform_Cpu_readable_model_reference_FileSystemUsesStableIdentityNames() {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService(), registerModelImporter: true);
            WriteSourceModel("Models/First/cube.obj");
            WriteSourceModel("Models/Second/cube.obj");
            SceneAssetReference firstReference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
                "11112222333344445555666677778888",
                "Models/First/cube.obj",
                "sha256:1111111111111111111111111111111111111111111111111111111111111111");
            SceneAssetReference secondReference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
                "9999aaaabbbbccccddddeeeeffff0000",
                "Models/Second/cube.obj",
                "sha256:2222222222222222222222222222222222222222222222222222222222222222");

            Assert.True(service.TryTransform(CreateCpuReadableModelReferenceRecord(firstReference), BuildRootPath, out SceneComponentAssetRecord firstRecord));
            Assert.True(service.TryTransform(CreateCpuReadableModelReferenceRecord(secondReference), BuildRootPath, out SceneComponentAssetRecord secondRecord));

            string firstPath = ReadPackagedCpuReadableModelReference(firstRecord).RelativePath;
            string secondPath = ReadPackagedCpuReadableModelReference(secondRecord).RelativePath;
            Assert.Equal("cooked/cpu-models/filesystem/11112222333344445555666677778888-1111111111111111111111111111111111111111111111111111111111111111.hasset", firstPath);
            Assert.Equal("cooked/cpu-models/filesystem/9999aaaabbbbccccddddeeeeffff0000-2222222222222222222222222222222222222222222222222222222222222222.hasset", secondPath);
            Assert.Equal(2, Directory.GetFiles(Path.Combine(BuildRootPath, "cooked", "cpu-models"), "*.hasset", SearchOption.AllDirectories).Length);
            AssertValidCpuReadableModelCompanion(Path.Combine(BuildRootPath, firstPath));
            AssertValidCpuReadableModelCompanion(Path.Combine(BuildRootPath, secondPath));
        }

        [Fact]
        public void TryTransform_Cpu_readable_model_reference_SameAssetIdDifferentHashes_UsesDistinctStableCompanionNames() {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService(), registerModelImporter: true);
            WriteSourceModel("Models/First/same-name.obj");
            WriteSourceModel("Models/Second/same-name.obj");
            const string assetId = "11112222333344445555666677778888";
            const string firstHash = "sha256:1111111111111111111111111111111111111111111111111111111111111111";
            const string secondHash = "sha256:2222222222222222222222222222222222222222222222222222222222222222";
            SceneAssetReference firstReference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(assetId, "Models/First/same-name.obj", firstHash);
            SceneAssetReference secondReference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(assetId, "Models/Second/same-name.obj", secondHash);

            Assert.True(service.TryTransform(CreateCpuReadableModelReferenceRecord(firstReference), BuildRootPath, out SceneComponentAssetRecord firstRecord));
            Assert.True(service.TryTransform(CreateCpuReadableModelReferenceRecord(secondReference), BuildRootPath, out SceneComponentAssetRecord secondRecord));

            string firstPath = ReadPackagedCpuReadableModelReference(firstRecord).RelativePath;
            string secondPath = ReadPackagedCpuReadableModelReference(secondRecord).RelativePath;
            Assert.Equal("cooked/cpu-models/filesystem/11112222333344445555666677778888-1111111111111111111111111111111111111111111111111111111111111111.hasset", firstPath);
            Assert.Equal("cooked/cpu-models/filesystem/11112222333344445555666677778888-2222222222222222222222222222222222222222222222222222222222222222.hasset", secondPath);
            Assert.NotEqual(firstPath, secondPath);
            AssertValidCpuReadableModelCompanion(Path.Combine(BuildRootPath, firstPath));
            AssertValidCpuReadableModelCompanion(Path.Combine(BuildRootPath, secondPath));
        }

        [Fact]
        public void TryTransform_Cpu_readable_model_reference_MalformedImportedModel_FailsBeforeWritingCompanion() {
            SceneComponentPackagingTransformService service = CreateService(
                new StubTextComponentSpriteBakeService(),
                registerModelImporter: true,
                modelImporter: new MalformedModelImporter());
            WriteSourceModel("Models/Malformed.obj");
            SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
                "11112222333344445555666677778888",
                "Models/Malformed.obj",
                "sha256:1111111111111111111111111111111111111111111111111111111111111111");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                service.TryTransform(CreateCpuReadableModelReferenceRecord(reference), BuildRootPath, out _));

            Assert.Contains("CPU-readable model companion", exception.Message, StringComparison.Ordinal);
            Assert.Contains("Positions", exception.Message, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(BuildRootPath, "cooked", "cpu-models")));
        }

        [Fact]
        public void TryTransform_Cpu_readable_model_reference_ImportedModelWithMultipleIndexWidths_FailsBeforeWritingCompanion() {
            SceneComponentPackagingTransformService service = CreateService(
                new StubTextComponentSpriteBakeService(),
                registerModelImporter: true,
                modelImporter: new MultipleIndexWidthModelImporter());
            WriteSourceModel("Models/MultipleIndexWidths.obj");
            SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
                "11112222333344445555666677778888",
                "Models/MultipleIndexWidths.obj",
                "sha256:1111111111111111111111111111111111111111111111111111111111111111");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                service.TryTransform(CreateCpuReadableModelReferenceRecord(reference), BuildRootPath, out _));

            Assert.Contains("CPU-readable model companion", exception.Message, StringComparison.Ordinal);
            Assert.Contains("exactly one populated index width", exception.Message, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(BuildRootPath, "cooked", "cpu-models")));
        }

        [Fact]
        public void TryTransform_Cpu_readable_model_reference_BuilderOwnedModelCook_EnqueuesNoPlatformCook() {
            List<PlatformCookWorkItem> workItems = new List<PlatformCookWorkItem>();
            SceneComponentPackagingTransformService service = CreateService(
                new StubTextComponentSpriteBakeService(),
                platformCookWorkItemSink: workItems.Add,
                platformDefinition: CreateBuilderOwnedModelPlatformDefinition());

            Assert.True(service.TryTransform(
                CreateCpuReadableModelReferenceRecord(SceneAssetReferenceTestFactory.CreateEngineCubeModel()),
                BuildRootPath,
                out SceneComponentAssetRecord transformedRecord));

            Assert.Empty(workItems);
            Assert.Equal(
                "cooked/cpu-models/engine/cube.hasset",
                DeserializeCpuReadableModelReferenceComponent(transformedRecord).ModelReference.RelativePath);
            AssertValidCpuReadableModelCompanion(Path.Combine(BuildRootPath, "cooked", "cpu-models", "engine", "cube.hasset"));
        }

        [Fact]
        public void Cpu_readable_model_reference_AttributeMetadata_UsesPublicFieldPropertyNonRepeatableInheritedContract() {
            AttributeUsageAttribute usage = typeof(CpuReadableModelReferenceAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.NotNull(usage);
            Assert.Equal(AttributeTargets.Field | AttributeTargets.Property, usage.ValidOn);
            Assert.False(usage.AllowMultiple);
            Assert.True(usage.Inherited);
        }

        /// <summary>
        /// Ensures the editor-only CPU-readable marker is omitted from reflection-disabled native source compilation.
        /// </summary>
        [Fact]
        public void Cpu_readable_model_reference_AttributeMetadata_IsExcludedFromCodegenReflectionBuild() {
            string sourcePath = Path.Combine(
                TestSourceRepositoryLocator.ResolveHelEngineRootPath(),
                "engine",
                "helengine.core",
                "scene",
                "CpuReadableModelReferenceAttribute.cs");
            string source = File.ReadAllText(sourcePath);

            Assert.Contains("#if !HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION", source, StringComparison.Ordinal);
            Assert.Contains("#endif", source, StringComparison.Ordinal);
        }

        [Fact]
        public void TryTransform_Cpu_readable_model_reference_PublicField_WritesCompanion() {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            SceneComponentAssetRecord record = CreateCpuReadableModelFieldReferenceRecord(SceneAssetReferenceTestFactory.CreateEngineCubeModel());

            Assert.True(service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord));

            Assert.Equal(
                "cooked/cpu-models/engine/cube.hasset",
                DeserializeCpuReadableModelFieldReferenceComponent(transformedRecord).ModelReference.RelativePath);
        }

        [Fact]
        public void TryTransform_Cpu_readable_model_reference_InheritedMember_WritesCompanion() {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            SceneComponentAssetRecord record = CreateCpuReadableModelInheritedReferenceRecord(SceneAssetReferenceTestFactory.CreateEngineCubeModel());

            Assert.True(service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord));

            Assert.Equal(
                "cooked/cpu-models/engine/cube.hasset",
                DeserializeCpuReadableModelInheritedReferenceComponent(transformedRecord).ModelReference.RelativePath);
        }

        [Fact]
        public void TryTransform_Cpu_readable_model_reference_UnsupportedGeneratedReference_FailsClearly() {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            SceneAssetReference reference = SceneAssetReferenceTestFactory.CreateSerialized(
                SceneAssetReferenceSourceKind.Generated,
                "Engine/Models/Unsupported",
                "engine",
                "engine:model:unsupported");
            SceneComponentAssetRecord record = CreateCpuReadableModelReferenceRecord(reference);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                service.TryTransform(record, BuildRootPath, out _));

            Assert.Contains("Unsupported generated CPU-readable model asset id", exception.Message, StringComparison.Ordinal);
            Assert.Contains("engine:model:unsupported", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TryTransform_Cpu_readable_model_reference_OrdinaryMeshModel_RemainsNormalModelPackaging() {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            SceneComponentAssetRecord record = CreateWrappedTessellatedMeshRecord(tessellateAtCookTime: false);

            Assert.True(service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord));

            SceneAssetReference modelReference = ReadAutomaticComponentAssetReference<MeshComponent>(transformedRecord, nameof(MeshComponent.Model));
            Assert.Equal("cooked/engine/models/cube.hasset", modelReference.RelativePath);
            Assert.False(Directory.Exists(Path.Combine(BuildRootPath, "cooked", "cpu-models")));
        }

        /// <summary>
        /// Ensures a stale imported texture path fails instead of being rewritten while preparing a builder cook request.
        /// </summary>
        [Fact]
        public void ValidateImportedTextureCookField_WhenPathIsStale_RejectsCurrentSettings() {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            string textureAssetId = "ff8a0f1fafe1f1c4989f73f39db8b800512e09e26439b011cb7afb0fed44dd5a";
            string staleTextureRelativePath = "cooked/imported/obsolete.hetex";
            Dictionary<string, string> fieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["texture-relative-path"] = staleTextureRelativePath
            };
            ShaderMaterialAsset materialAsset = new ShaderMaterialAsset {
                DiffuseTextureAssetId = textureAssetId
            };

            MethodInfo normalizeMethod = typeof(SceneComponentPackagingTransformService).GetMethod(
                "ValidateImportedTextureCookField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(normalizeMethod);

            TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(() => normalizeMethod.Invoke(
                service,
                [fieldValues, materialAsset, staleTextureRelativePath]));
            InvalidOperationException exception = Assert.IsType<InvalidOperationException>(invocation.InnerException);
            Assert.Contains("noncanonical imported texture path", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Regenerate the material settings", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(staleTextureRelativePath, fieldValues["texture-relative-path"]);
        }

        /// <summary>
        /// Ensures platform-extended text metadata stored in detached DS overrides is emitted into the packaged ordinal runtime payload.
        /// </summary>
        [Fact]
        public void TryTransform_WhenDsTextComponentHasSyntheticBgLayerOverride_WritesSyntheticPlatformMemberIntoPackagedPayload() {
            PlatformDefinition platformDefinition = CreateDsSyntheticTextPlatformDefinition();
            SceneComponentPackagingTransformService service = CreateDsSyntheticTextService(platformDefinition);
            SceneComponentAssetRecord record = CreateWrappedTextRecord(false, "1");

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            TextComponent restored = DeserializePlatformExtendedAutomaticComponent<TextComponent>(transformedRecord, platformDefinition);
            Assert.Equal(1, restored.GetSyntheticInt32MemberOrDefault("BGLayer", -1));
        }

        /// <summary>
        /// Ensures packaging selects a complete automatic-component payload from the target platform instead of silently reverting to common values.
        /// </summary>
        [Fact]
        public void TryTransform_WhenTargetPlatformHasTextOverride_UsesTargetPlatformFontScale() {
            PlatformDefinition platformDefinition = CreateDsSyntheticTextPlatformDefinition();
            SceneComponentPackagingTransformService service = CreateDsSyntheticTextService(platformDefinition);
            SceneComponentAssetRecord record = CreateWrappedTextRecordWithFontScaleOverride(0.5f);

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            TextComponent restored = DeserializePlatformExtendedAutomaticComponent<TextComponent>(transformedRecord, platformDefinition);
            Assert.Equal(0.5f, restored.FontScale, 3);
        }

        /// <summary>
        /// Ensures flagged text remains a runtime text payload and does not call the bake service.
        /// </summary>
        [Fact]
        public void TryTransform_WhenTextComponentIsFlagged_KeepsTextComponentPayloadAndDoesNotCallBakeService() {
            StubTextComponentSpriteBakeService bakeService = new StubTextComponentSpriteBakeService();
            SceneComponentPackagingTransformService service = CreateService(bakeService);
            SceneComponentAssetRecord record = CreateTextRecord(true);

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            Assert.Equal("helengine.TextComponent", transformedRecord.ComponentTypeId);
            Assert.False(bakeService.WasCalled);
        }

        /// <summary>
        /// Ensures flagged text no longer writes one generated texture asset into packaged build output.
        /// </summary>
        [Fact]
        public void TryTransform_WhenTextComponentIsFlagged_DoesNotWriteGeneratedTextureAssetToCookedOutput() {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            SceneComponentAssetRecord record = CreateTextRecord(true);

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            string generatedTextureDirectoryPath = Path.Combine(BuildRootPath, "cooked", "generated", "text-sprites");
            Assert.False(Directory.Exists(generatedTextureDirectoryPath));
        }

        /// <summary>
        /// Ensures flagged text no longer enqueues one builder-owned texture cook work item when the selected platform owns texture cooking.
        /// </summary>
        [Fact]
        public void TryTransform_WhenBuilderOwnedTextureCookIsEnabled_DoesNotEnqueueGeneratedTextureCookWorkItem() {
            List<PlatformCookWorkItem> workItems = new List<PlatformCookWorkItem>();
            SceneComponentPackagingTransformService service = CreateBuilderOwnedTextureService(workItems, new StubTextComponentSpriteBakeService());

            bool transformed = service.TryTransform(CreateTextRecord(true), BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            Assert.Empty(workItems);
        }

        /// <summary>
        /// Ensures builder-owned font-atlas texture capabilities externalize imported font atlases through the dedicated cook kind while keeping the shared runtime texture path.
        /// </summary>
        [Fact]
        public void TryTransform_WhenPlatformOwnsFontAtlasTextureCooking_ExternalizesImportedFontAtlasUsingGenericTexturePath() {
            string fontRelativePath = "Fonts/DemoDiscTitle.ttf";
            List<PlatformCookWorkItem> workItems = new List<PlatformCookWorkItem>();
            SceneComponentPackagingTransformService service = CreateBuilderOwnedFontAtlasService(workItems, new StubTextComponentSpriteBakeService());
            WriteSourceFont(fontRelativePath);
            SceneComponentAssetRecord record = CreateDebugRecord(CreateFileFontReference(fontRelativePath));

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            PlatformCookWorkItem workItem = Assert.Single(workItems);
            Assert.Equal("font-atlas-texture", workItem.SourceAssetKind);
            Assert.Equal(".hetex", Path.GetExtension(workItem.SourceAssetPath));
            Assert.Contains(Path.Combine(ProjectRootPath, "cache", "generated", "platform-fonts"), workItem.SourceAssetPath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("cooked/fonts/demodisctitle.hetex", workItem.OutputRelativePath);

            string cookedFontPath = Path.Combine(BuildRootPath, "cooked", "fonts", "demodisctitle.hefont");
            using FileStream fontStream = File.OpenRead(cookedFontPath);
            FontAsset cookedFontAsset = helengine.files.FontAssetBinarySerializer.Deserialize(fontStream);
            Assert.Equal("cooked/fonts/demodisctitle.hetex", cookedFontAsset.CookedAtlasTextureRelativePath);
            Assert.Null(cookedFontAsset.SourceTextureAsset);
        }

        /// <summary>
        /// Ensures rooted packaged-path platforms write rooted runtime font-atlas references while preserving the shared builder-owned texture path.
        /// </summary>
        [Fact]
        public void TryTransform_WhenPlatformOwnsFontAtlasTextureCookingAndAllowsRootedPackagedPaths_WritesRootedAtlasRuntimePath() {
            string fontRelativePath = "Fonts/DemoDiscTitle.ttf";
            List<PlatformCookWorkItem> workItems = new List<PlatformCookWorkItem>();
            SceneComponentPackagingTransformService service = CreateRootedBuilderOwnedFontAtlasService(workItems, new StubTextComponentSpriteBakeService());
            WriteSourceFont(fontRelativePath);
            SceneComponentAssetRecord record = CreateDebugRecord(CreateFileFontReference(fontRelativePath));

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            PlatformCookWorkItem workItem = Assert.Single(workItems);
            Assert.Equal("cooked/fonts/demodisctitle.hetex", workItem.OutputRelativePath);

            string cookedFontPath = Path.Combine(BuildRootPath, "cooked", "fonts", "demodisctitle.hefont");
            using FileStream fontStream = File.OpenRead(cookedFontPath);
            FontAsset cookedFontAsset = helengine.files.FontAssetBinarySerializer.Deserialize(fontStream);
            Assert.Equal("/cooked/fonts/demodisctitle.hetex", cookedFontAsset.CookedAtlasTextureRelativePath);
            Assert.Null(cookedFontAsset.SourceTextureAsset);
        }

        /// <summary>
        /// Ensures authored sprite components that persist their texture field through the automatic editor payload contract still package successfully.
        /// </summary>
        [Fact]
        public void TryTransform_WhenSpriteComponentUsesAuthoredTextureField_RewritesSpritePayload() {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            SceneComponentAssetRecord record = CreateSpriteRecord();

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            Assert.Equal("helengine.SpriteComponent", transformedRecord.ComponentTypeId);
            SceneAssetReference textureReference = ReadAutomaticComponentAssetReference<SpriteComponent>(transformedRecord, nameof(SpriteComponent.Texture));
            Assert.NotNull(textureReference);
            Assert.StartsWith("cooked/imported/", textureReference.RelativePath, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures equal enabled MeshComponents reuse one scale-aware generated model variant after normal model packaging.
        /// </summary>
        [Fact]
        public void TryTransform_WhenMeshTessellationIsEnabled_ReusesOneGeneratedVariantForEqualScaleAndSettings() {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            SceneComponentAssetRecord firstRecord = CreateWrappedTessellatedMeshRecord();
            SceneComponentAssetRecord secondRecord = CreateWrappedTessellatedMeshRecord();
            SceneComponentPackagingTransformContext context = new SceneComponentPackagingTransformContext(new float3(4f, 1f, 1f));

            bool firstTransformed = service.TryTransform(firstRecord, BuildRootPath, context, out SceneComponentAssetRecord firstOutput);
            bool secondTransformed = service.TryTransform(secondRecord, BuildRootPath, context, out SceneComponentAssetRecord secondOutput);

            Assert.True(firstTransformed);
            Assert.True(secondTransformed);
            SceneAssetReference firstModelReference = ReadAutomaticComponentAssetReference<MeshComponent>(firstOutput, nameof(MeshComponent.Model));
            SceneAssetReference secondModelReference = ReadAutomaticComponentAssetReference<MeshComponent>(secondOutput, nameof(MeshComponent.Model));
            Assert.Equal(firstModelReference.RelativePath, secondModelReference.RelativePath);
            Assert.StartsWith("cooked/generated/models/tessellation/", firstModelReference.RelativePath, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(BuildRootPath, firstModelReference.RelativePath)));
            string variantDirectoryPath = Path.Combine(BuildRootPath, "cooked", "generated", "models", "tessellation");
            Assert.Single(Directory.GetFiles(variantDirectoryPath, "*.hasset"));
        }

        /// <summary>
        /// Ensures an enabled load-time tessellation request keeps its packaged source model instead of creating a cooked variant.
        /// </summary>
        [Fact]
        public void TryTransform_WhenMeshTessellationRunsAtLoadTime_DoesNotCreateCookedVariant() {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            SceneComponentAssetRecord record = CreateWrappedTessellatedMeshRecord(tessellateAtCookTime: false);
            SceneComponentPackagingTransformContext context = new SceneComponentPackagingTransformContext(new float3(4f, 1f, 1f));

            bool transformed = service.TryTransform(record, BuildRootPath, context, out SceneComponentAssetRecord output);

            Assert.True(transformed);
            SceneAssetReference modelReference = ReadAutomaticComponentAssetReference<MeshComponent>(output, nameof(MeshComponent.Model));
            Assert.DoesNotContain("cooked/generated/models/tessellation/", modelReference.RelativePath, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(BuildRootPath, "cooked", "generated", "models", "tessellation")));
        }

        /// <summary>
        /// Ensures superseded per-platform tessellation members do not create a cooked model variant.
        /// </summary>
        [Fact]
        public void TryTransform_WhenSupersededTessellationMembersAreEnabled_DoesNotCreateCookedVariant() {
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            SceneComponentAssetRecord record = CreateWrappedSupersededTessellatedMeshRecord();
            SceneComponentPackagingTransformContext context = new SceneComponentPackagingTransformContext(new float3(4f, 1f, 1f));

            bool transformed = service.TryTransform(record, BuildRootPath, context, out SceneComponentAssetRecord output);

            Assert.True(transformed);
            Assert.False(Directory.Exists(Path.Combine(BuildRootPath, "cooked", "generated", "models", "tessellation")));
        }

        /// <summary>
        /// Ensures tessellation loads a generated model from the package root when PS2 serializes its runtime model reference with a rooted path.
        /// </summary>
        [Fact]
        public void TryTransform_WhenMeshTessellationUsesRootedPs2ModelReference_LoadsThePackagedModelBeforeWritingTheVariant() {
            List<PlatformCookWorkItem> workItems = new List<PlatformCookWorkItem>();
            SceneComponentPackagingTransformService service = CreateRootedBuilderOwnedFontAtlasService(workItems, new StubTextComponentSpriteBakeService());
            SceneComponentAssetRecord record = CreateWrappedTessellatedMeshRecord("ps2");
            SceneComponentPackagingTransformContext context = new SceneComponentPackagingTransformContext(new float3(4f, 1f, 1f));

            bool transformed = service.TryTransform(record, BuildRootPath, context, out SceneComponentAssetRecord output);

            Assert.True(transformed);
            SceneAssetReference modelReference = ReadAutomaticComponentAssetReference<MeshComponent>(output, nameof(MeshComponent.Model), true);
            Assert.StartsWith("/cooked/generated/models/tessellation/", modelReference.RelativePath, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(BuildRootPath, modelReference.RelativePath.TrimStart('/', '\\'))));
        }

        /// <summary>
        /// Ensures authored automatic audio-source components rewrite their file-backed clip references into cooked packaged audio assets.
        /// </summary>
        [Fact]
        public void TryTransform_WhenAudioSourceComponentUsesAuthoredClipReference_RewritesAudioPayload() {
            const string audioRelativePath = "audio/menu/theme.wav";
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            string sourcePath = WriteSourceAudio(audioRelativePath);
            ConfigureAudioImportSettings(
                sourcePath,
                "windows",
                new AudioAssetProcessorSettings {
                    PlaybackMode = AudioPlaybackMode.Streamed,
                    EncodingFamilyId = "pcm-streamed",
                    DefaultBusId = "music",
                    DefaultLoop = true,
                    StreamChunkByteSize = 4
                });
            SceneComponentAssetRecord record = CreateAudioSourceRecord(audioRelativePath);

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            SceneAssetReference clipReference = ReadAutomaticComponentAssetReference<AudioSourceComponent>(transformedRecord, nameof(AudioSourceComponent.Clip));
            Assert.NotNull(clipReference);
            Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, clipReference.SourceKind);
            Assert.Equal("cooked/audio/menu/theme.hasset", clipReference.RelativePath);
            Assert.True(File.Exists(Path.Combine(BuildRootPath, "cooked", "audio", "menu", "theme.hasset")));
        }

        /// <summary>
        /// Ensures automatic asset-reference rewriting accepts engine asset member types that arrive from another load context but keep the same full name.
        /// </summary>
        [Fact]
        public void RewriteAutomaticComponentReference_WhenAudioTypeMatchesByFullName_RewritesAudioPayload() {
            const string audioRelativePath = "audio/menu/theme.wav";
            SceneComponentPackagingTransformService service = CreateService(new StubTextComponentSpriteBakeService());
            string sourcePath = WriteSourceAudio(audioRelativePath);
            ConfigureAudioImportSettings(
                sourcePath,
                "windows",
                new AudioAssetProcessorSettings {
                    PlaybackMode = AudioPlaybackMode.Streamed,
                    EncodingFamilyId = "pcm-streamed",
                    DefaultBusId = "music",
                    DefaultLoop = true,
                    StreamChunkByteSize = 4
                });

            MethodInfo rewriteMethod = typeof(SceneComponentPackagingTransformService).GetMethod(
                "RewriteAutomaticComponentReference",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(rewriteMethod);

            Type foreignAudioAssetType = CreateForeignEngineType("helengine.AudioAsset");
            SceneAssetReference sourceReference = global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateFileSystemAudio(audioRelativePath);

            SceneAssetReference rewrittenReference = Assert.IsType<SceneAssetReference>(rewriteMethod.Invoke(service, [foreignAudioAssetType, sourceReference, BuildRootPath]));

            Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, rewrittenReference.SourceKind);
            Assert.Equal("cooked/audio/menu/theme.hasset", rewrittenReference.RelativePath);
        }

        /// <summary>
        /// Ensures DS-authored generated debug-font references are rejected before packaging can materialize one runtime payload.
        /// </summary>
        [Fact]
        public void SerializeDebugRecord_WhenDebugComponentUsesRemovedNintendoDsGeneratedFont_ThrowsUnsupportedGeneratedReference() {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => CreateDebugRecord(CreateNintendoDsDebugFontReference()));
            Assert.Contains("Unsupported generated font asset id", exception.Message);
        }

        /// <summary>
        /// Ensures a registered static-mesh cook processor can populate the cooked runtime payload during packaging.
        /// </summary>
        [Fact]
        public void TryTransform_WhenStaticMeshColliderUsesRegisteredCookProcessor_WritesCookedRuntimePayload() {
            StaticMeshCollisionCookProcessorRegistry registry = new StaticMeshCollisionCookProcessorRegistry();
            registry.RegisterProcessor(new StubStaticMeshCollisionCookProcessor3D());
            SceneComponentPackagingTransformService service = CreateBigEndianStaticMeshService(new StubTextComponentSpriteBakeService(), registry);
            SceneComponentAssetRecord record = CreateStaticMeshColliderRecord();

            bool transformed = service.TryTransform(record, BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            StaticMeshCollider3DComponent restored = DeserializeAutomaticComponent<StaticMeshCollider3DComponent>(transformedRecord);
            Assert.NotNull(restored.CookedRuntimeData);
            Assert.Equal("test.static-mesh", restored.CookedRuntimeData.FormatId);
            using EngineBinaryReader reader = restored.CookedRuntimeData.CreatePayloadReader("test.static-mesh", 0x7A01, 3);
            Assert.Equal(EngineBinaryEndianness.BigEndian, reader.Endianness);
            Assert.Equal(1, reader.ReadInt32());
            Assert.Equal(0.25f, reader.ReadSingle());
        }

        /// <summary>
        /// Ensures the real BEPU static-mesh cook processor can populate a BEPU-owned runtime payload during packaging.
        /// </summary>
        [Fact]
        public void TryTransform_WhenStaticMeshColliderUsesRealBepuCookProcessor_WritesBepuPayload() {
            StaticMeshCollisionCookProcessorRegistry registry = new StaticMeshCollisionCookProcessorRegistry();
            registry.RegisterProcessor(new BepuStaticMeshCollisionCookProcessor3D());
            SceneComponentPackagingTransformService service = CreateBigEndianStaticMeshService(new StubTextComponentSpriteBakeService(), registry);

            bool transformed = service.TryTransform(CreateStaticMeshColliderRecord(), BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            StaticMeshCollider3DComponent restored = DeserializeAutomaticComponent<StaticMeshCollider3DComponent>(transformedRecord);
            Assert.Equal(BepuStaticMeshCollisionCookProcessor3D.FormatIdValue, restored.CookedRuntimeData.FormatId);
            using EngineBinaryReader reader = restored.CookedRuntimeData.CreatePayloadReader(
                BepuStaticMeshCollisionCookProcessor3D.FormatIdValue,
                BepuStaticMeshCollisionCookProcessor3D.BinaryFormatIdValue,
                BepuStaticMeshCollisionCookProcessor3D.BinaryFormatVersionValue);
            Assert.Equal(EngineBinaryEndianness.BigEndian, reader.Endianness);
        }

        /// <summary>
        /// Ensures the BEPU cook processor preserves the generic static mesh collision data while adding the cooked payload.
        /// </summary>
        [Fact]
        public void TryTransform_WhenBepuCookProcessorRuns_PreservesGenericCollisionDataAlongsideCookedPayload() {
            StaticMeshCollisionCookProcessorRegistry registry = new StaticMeshCollisionCookProcessorRegistry();
            registry.RegisterProcessor(new BepuStaticMeshCollisionCookProcessor3D());
            SceneComponentPackagingTransformService service = CreateBigEndianStaticMeshService(new StubTextComponentSpriteBakeService(), registry);

            bool transformed = service.TryTransform(CreateStaticMeshColliderRecord(), BuildRootPath, out SceneComponentAssetRecord transformedRecord);

            Assert.True(transformed);
            Assert.NotNull(transformedRecord);
            StaticMeshCollider3DComponent restored = DeserializeAutomaticComponent<StaticMeshCollider3DComponent>(transformedRecord);
            Assert.Equal(3, restored.CollisionData.Vertices.Length);
            Assert.Equal(new[] { 0, 1, 2 }, restored.CollisionData.Indices);
            Assert.Equal(BepuStaticMeshCollisionCookProcessor3D.FormatIdValue, restored.CookedRuntimeData.FormatId);
            using EngineBinaryReader reader = restored.CookedRuntimeData.CreatePayloadReader(
                BepuStaticMeshCollisionCookProcessor3D.FormatIdValue,
                BepuStaticMeshCollisionCookProcessor3D.BinaryFormatIdValue,
                BepuStaticMeshCollisionCookProcessor3D.BinaryFormatVersionValue);
            Assert.Equal(EngineBinaryEndianness.BigEndian, reader.Endianness);
        }

        /// <summary>
        /// Creates one transform service wired to real project dependencies and one injected bake-service seam.
        /// </summary>
        /// <param name="bakeService">Bake service that should receive flagged text requests.</param>
        /// <returns>Configured transform service.</returns>
        SceneComponentPackagingTransformService CreateService(
            ITextComponentSpriteBakeService bakeService,
            StaticMeshCollisionCookProcessorRegistry staticMeshCookProcessorRegistry = null,
            bool registerModelImporter = false,
            IModelImporter modelImporter = null,
            Action<PlatformCookWorkItem> platformCookWorkItemSink = null,
            PlatformDefinition platformDefinition = null) {
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(ProjectRootPath));
            AssetImportManager assetImportManager = new AssetImportManager(ProjectRootPath, contentManager);
            assetImportManager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), [".ttf"]));
            assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"]));
            assetImportManager.RegisterAudioImporter(new AudioImporterRegistration("test-audio", new TestAudioImporter(), [".wav"]));
            if (registerModelImporter || modelImporter != null) {
                assetImportManager.RegisterModelImporter(new ModelImporterRegistration("test-model", modelImporter ?? new TestModelImporter(), [".obj"]));
            }
            EditorFileSystemModelResolver fileSystemModelResolver = new EditorFileSystemModelResolver(assetImportManager);

            return new SceneComponentPackagingTransformService(
                Path.Combine(ProjectRootPath, "assets"),
                contentManager,
                assetImportManager,
                fileSystemModelResolver,
                TestGeneratedAssetGraph.CreateShaderLibrary(),
                "windows",
                null,
                string.Empty,
                string.Empty,
                null,
                platformCookWorkItemSink,
                platformDefinition,
                bakeService,
                staticMeshCookProcessorRegistry);
        }

        /// <summary>
        /// Creates one transform service configured with one big-endian codegen profile for static-mesh runtime payload verification.
        /// </summary>
        /// <param name="bakeService">Bake service that should receive flagged text requests.</param>
        /// <param name="staticMeshCookProcessorRegistry">Cook processor registry used by the service.</param>
        /// <returns>Configured transform service.</returns>
        SceneComponentPackagingTransformService CreateBigEndianStaticMeshService(
            ITextComponentSpriteBakeService bakeService,
            StaticMeshCollisionCookProcessorRegistry staticMeshCookProcessorRegistry) {
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(ProjectRootPath));
            AssetImportManager assetImportManager = new AssetImportManager(ProjectRootPath, contentManager);
            assetImportManager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), [".ttf"]));
            assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"]));
            assetImportManager.RegisterAudioImporter(new AudioImporterRegistration("test-audio", new TestAudioImporter(), [".wav"]));
            EditorFileSystemModelResolver fileSystemModelResolver = new EditorFileSystemModelResolver(assetImportManager);
            PlatformDefinition platformDefinition = CreateBigEndianStaticMeshPlatformDefinition();

            return new SceneComponentPackagingTransformService(
                Path.Combine(ProjectRootPath, "assets"),
                contentManager,
                assetImportManager,
                fileSystemModelResolver,
                TestGeneratedAssetGraph.CreateShaderLibrary(),
                "gamecube",
                null,
                "main",
                string.Empty,
                null,
                null,
                platformDefinition,
                bakeService,
                staticMeshCookProcessorRegistry);
        }

        /// <summary>
        /// Creates one transform service configured for DS text synthetic-member packaging verification.
        /// </summary>
        /// <param name="platformDefinition">Platform definition that exposes the synthetic text member.</param>
        /// <returns>Configured transform service.</returns>
        SceneComponentPackagingTransformService CreateDsSyntheticTextService(PlatformDefinition platformDefinition) {
            if (platformDefinition == null) {
                throw new ArgumentNullException(nameof(platformDefinition));
            }

            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(ProjectRootPath));
            AssetImportManager assetImportManager = new AssetImportManager(ProjectRootPath, contentManager);
            assetImportManager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), [".ttf"]));
            assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"]));
            assetImportManager.RegisterAudioImporter(new AudioImporterRegistration("test-audio", new TestAudioImporter(), [".wav"]));
            EditorFileSystemModelResolver fileSystemModelResolver = new EditorFileSystemModelResolver(assetImportManager);

            return new SceneComponentPackagingTransformService(
                Path.Combine(ProjectRootPath, "assets"),
                contentManager,
                assetImportManager,
                fileSystemModelResolver,
                TestGeneratedAssetGraph.CreateShaderLibrary(),
                "ds",
                null,
                string.Empty,
                string.Empty,
                null,
                null,
                platformDefinition,
                new StubTextComponentSpriteBakeService());
        }

        /// <summary>
        /// Creates one transform service whose target platform publishes builder-owned texture cooking.
        /// </summary>
        /// <param name="workItems">Collected builder-owned work items emitted during packaging.</param>
        /// <param name="bakeService">Bake service that should receive flagged text requests.</param>
        /// <returns>Configured transform service that records generated texture cook work items.</returns>
        SceneComponentPackagingTransformService CreateBuilderOwnedTextureService(List<PlatformCookWorkItem> workItems, ITextComponentSpriteBakeService bakeService) {
            if (workItems == null) {
                throw new ArgumentNullException(nameof(workItems));
            }

            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(ProjectRootPath));
            AssetImportManager assetImportManager = new AssetImportManager(ProjectRootPath, contentManager);
            assetImportManager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), [".ttf"]));
            assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"]));
            assetImportManager.RegisterAudioImporter(new AudioImporterRegistration("test-audio", new TestAudioImporter(), [".wav"]));
            EditorFileSystemModelResolver fileSystemModelResolver = new EditorFileSystemModelResolver(assetImportManager);

            return new SceneComponentPackagingTransformService(
                Path.Combine(ProjectRootPath, "assets"),
                contentManager,
                assetImportManager,
                fileSystemModelResolver,
                TestGeneratedAssetGraph.CreateShaderLibrary(),
                "ds",
                null,
                string.Empty,
                string.Empty,
                null,
                workItems.Add,
                CreateBuilderOwnedTexturePlatformDefinition(),
                bakeService);
        }

        /// <summary>
        /// Creates one transform service whose target platform publishes builder-owned font-atlas texture cooking.
        /// </summary>
        /// <param name="workItems">Collected builder-owned work items emitted during packaging.</param>
        /// <param name="bakeService">Bake service that should receive flagged text requests.</param>
        /// <returns>Configured transform service that records generated font-atlas cook work items.</returns>
        SceneComponentPackagingTransformService CreateBuilderOwnedFontAtlasService(List<PlatformCookWorkItem> workItems, ITextComponentSpriteBakeService bakeService) {
            if (workItems == null) {
                throw new ArgumentNullException(nameof(workItems));
            }

            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(ProjectRootPath));
            AssetImportManager assetImportManager = new AssetImportManager(ProjectRootPath, contentManager);
            assetImportManager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), [".ttf"]));
            assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"]));
            assetImportManager.RegisterAudioImporter(new AudioImporterRegistration("test-audio", new TestAudioImporter(), [".wav"]));
            EditorFileSystemModelResolver fileSystemModelResolver = new EditorFileSystemModelResolver(assetImportManager);

            return new SceneComponentPackagingTransformService(
                Path.Combine(ProjectRootPath, "assets"),
                contentManager,
                assetImportManager,
                fileSystemModelResolver,
                TestGeneratedAssetGraph.CreateShaderLibrary(),
                "external-platform",
                null,
                string.Empty,
                string.Empty,
                null,
                workItems.Add,
                CreateBuilderOwnedFontAtlasPlatformDefinition(),
                bakeService);
        }

        /// <summary>
        /// Creates one transform service whose target platform publishes builder-owned font-atlas cooking and rooted packaged runtime paths.
        /// </summary>
        /// <param name="workItems">Collected builder-owned work items emitted during packaging.</param>
        /// <param name="bakeService">Bake service that should receive flagged text requests.</param>
        /// <returns>Configured transform service that records rooted font-atlas cook work items.</returns>
        SceneComponentPackagingTransformService CreateRootedBuilderOwnedFontAtlasService(List<PlatformCookWorkItem> workItems, ITextComponentSpriteBakeService bakeService) {
            if (workItems == null) {
                throw new ArgumentNullException(nameof(workItems));
            }

            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(ProjectRootPath));
            AssetImportManager assetImportManager = new AssetImportManager(ProjectRootPath, contentManager);
            assetImportManager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), [".ttf"]));
            assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"]));
            assetImportManager.RegisterAudioImporter(new AudioImporterRegistration("test-audio", new TestAudioImporter(), [".wav"]));
            EditorFileSystemModelResolver fileSystemModelResolver = new EditorFileSystemModelResolver(assetImportManager);

            return new SceneComponentPackagingTransformService(
                Path.Combine(ProjectRootPath, "assets"),
                contentManager,
                assetImportManager,
                fileSystemModelResolver,
                TestGeneratedAssetGraph.CreateShaderLibrary(),
                "ps2",
                null,
                string.Empty,
                string.Empty,
                null,
                workItems.Add,
                CreateRootedBuilderOwnedFontAtlasPlatformDefinition(),
                bakeService);
        }

        static SceneComponentAssetRecord CreateCpuReadableModelReferenceRecord(SceneAssetReference reference) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            return descriptor.SerializeComponent(
                new CpuReadableModelReferenceComponent { ModelReference = reference },
                0,
                null);
        }

        static SceneComponentAssetRecord CreateCpuReadableModelFieldReferenceRecord(SceneAssetReference reference) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            CpuReadableModelFieldReferenceComponent component = new CpuReadableModelFieldReferenceComponent {
                ModelReference = reference
            };
            return descriptor.SerializeComponent(component, 0, null);
        }

        static SceneComponentAssetRecord CreateCpuReadableModelInheritedReferenceRecord(SceneAssetReference reference) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            return descriptor.SerializeComponent(
                new CpuReadableModelInheritedReferenceComponent { ModelReference = reference },
                0,
                null);
        }

        void WriteSourceModel(string relativePath) {
            string sourcePath = Path.Combine(ProjectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
            string directoryPath = Path.GetDirectoryName(sourcePath);
            Directory.CreateDirectory(directoryPath);
            File.WriteAllText(sourcePath, "test model source");
        }

        static SceneComponentAssetRecord CreateUnmarkedModelReferenceRecord(SceneAssetReference reference) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            return descriptor.SerializeComponent(
                new CpuReadableModelUnmarkedReferenceComponent { ModelReference = reference },
                0,
                null);
        }

        static SceneComponentAssetRecord CreateInvalidCpuReadableModelReferenceRecord() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            return descriptor.SerializeComponent(
                new InvalidCpuReadableModelReferenceComponent { ModelPath = "Models/cube.obj" },
                0,
                null);
        }

        static CpuReadableModelReferenceComponent DeserializeCpuReadableModelReferenceComponent(SceneComponentAssetRecord record) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            return Assert.IsType<CpuReadableModelReferenceComponent>(descriptor.DeserializeComponent(record, new EntitySaveComponent(), null));
        }

        static CpuReadableModelUnmarkedReferenceComponent DeserializeUnmarkedModelReferenceComponent(SceneComponentAssetRecord record) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            return Assert.IsType<CpuReadableModelUnmarkedReferenceComponent>(descriptor.DeserializeComponent(record, new EntitySaveComponent(), null));
        }

        static CpuReadableModelFieldReferenceComponent DeserializeCpuReadableModelFieldReferenceComponent(SceneComponentAssetRecord record) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            return Assert.IsType<CpuReadableModelFieldReferenceComponent>(descriptor.DeserializeComponent(record, new EntitySaveComponent(), null));
        }

        static CpuReadableModelInheritedReferenceComponent DeserializeCpuReadableModelInheritedReferenceComponent(SceneComponentAssetRecord record) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            return Assert.IsType<CpuReadableModelInheritedReferenceComponent>(descriptor.DeserializeComponent(record, new EntitySaveComponent(), null));
        }

        static void AssertValidCpuReadableModelCompanion(string companionPath) {
            using FileStream stream = new FileStream(companionPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            ModelAsset model = Assert.IsType<ModelAsset>(AssetSerializer.Deserialize(stream));
            Assert.NotNull(model.Positions);
            Assert.NotEmpty(model.Positions);
            bool has16BitIndices = model.Indices16 != null && model.Indices16.Length > 0;
            bool has32BitIndices = model.Indices32 != null && model.Indices32.Length > 0;
            Assert.True(has16BitIndices ^ has32BitIndices);
        }

        static SceneAssetReference ReadPackagedCpuReadableModelReference(SceneComponentAssetRecord record) {
            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            Assert.Equal(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion, reader.ReadByte());
            Assert.Equal(1, reader.ReadInt32());
            return Assert.IsType<SceneAssetReference>(global::helengine.SceneAssetReferenceFactory.ReadOptionalReference(reader));
        }

        /// <summary>
        /// Creates one automatic reflected text-component record for packaging verification.
        /// </summary>
        /// <param name="convertTextToSprite">True when the authored text should request build-time sprite conversion.</param>
        /// <returns>Serialized text-component record.</returns>
        SceneComponentAssetRecord CreateTextRecord(bool convertTextToSprite) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            TextComponent textComponent = new TextComponent {
                Font = CreatePackagedFontAsset(),
                Text = "Hello world",
                WrapText = true,
                Size = new int2(128, 32),
                Color = new byte4(12, 34, 56, 255),
                SourceRect = new float4(0f, 0f, 1f, 1f),
                Rotation = 0.25f,
                FontScale = 2f,
                RenderOrder2D = 19,
                SelectionEnabled = true,
                ConvertTextToSprite = convertTextToSprite,
                Alignment = TextAlignment.Center
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(nameof(TextComponent.Font), CreateEditorFontReference());

            return descriptor.SerializeComponent(textComponent, 0, saveState);
        }

        /// <summary>
        /// Creates one wrapped automatic reflected text-component record that carries one detached DS synthetic member override.
        /// </summary>
        /// <param name="convertTextToSprite">True when the authored text should request build-time sprite conversion.</param>
        /// <param name="bgLayerValue">Serialized DS background-layer override value.</param>
        /// <returns>Wrapped serialized text-component record.</returns>
        SceneComponentAssetRecord CreateWrappedTextRecord(bool convertTextToSprite, string bgLayerValue) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            TextComponent textComponent = new TextComponent {
                Font = CreatePackagedFontAsset(),
                Text = "Hello world",
                WrapText = true,
                Size = new int2(128, 32),
                Color = new byte4(12, 34, 56, 255),
                SourceRect = new float4(0f, 0f, 1f, 1f),
                Rotation = 0.25f,
                FontScale = 2f,
                RenderOrder2D = 19,
                SelectionEnabled = true,
                ConvertTextToSprite = convertTextToSprite,
                Alignment = TextAlignment.Center
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(nameof(TextComponent.Font), CreateEditorFontReference());
            SceneComponentAssetRecord baseRecord = descriptor.SerializeComponent(textComponent, 0, saveState);
            EntityComponentPlatformOverrideState overrideState = saveState.GetOrCreatePlatformOverride("ds");
            overrideState.Payload = baseRecord.Payload;
            overrideState.SetPropertyOverride("BGLayer");
            overrideState.SetMemberValue("BGLayer", bgLayerValue);
            return new ComponentPlatformOverridePayloadService().Wrap(baseRecord, saveState);
        }

        /// <summary>
        /// Creates one wrapped text-component record whose DS override contains a smaller font scale than the common component.
        /// </summary>
        /// <param name="fontScale">Target-platform font scale stored in the override payload.</param>
        /// <returns>Wrapped text-component record with a target-platform font override.</returns>
        SceneComponentAssetRecord CreateWrappedTextRecordWithFontScaleOverride(float fontScale) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            TextComponent commonTextComponent = new TextComponent {
                Font = CreatePackagedFontAsset(),
                Text = "Hello world",
                WrapText = true,
                Size = new int2(128, 32),
                Color = new byte4(12, 34, 56, 255),
                SourceRect = new float4(0f, 0f, 1f, 1f),
                Rotation = 0.25f,
                FontScale = 2f,
                RenderOrder2D = 19,
                SelectionEnabled = true,
                Alignment = TextAlignment.Center
            };
            TextComponent targetTextComponent = new TextComponent {
                Font = CreatePackagedFontAsset(),
                Text = "Hello world",
                WrapText = true,
                Size = new int2(128, 32),
                Color = new byte4(12, 34, 56, 255),
                SourceRect = new float4(0f, 0f, 1f, 1f),
                Rotation = 0.25f,
                FontScale = fontScale,
                RenderOrder2D = 19,
                SelectionEnabled = true,
                Alignment = TextAlignment.Center
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(nameof(TextComponent.Font), CreateEditorFontReference());
            SceneComponentAssetRecord baseRecord = descriptor.SerializeComponent(commonTextComponent, 0, saveState);
            EntityComponentSaveState targetSaveState = new EntityComponentSaveState();
            targetSaveState.SetAssetReference(nameof(TextComponent.Font), CreateEditorFontReference());
            SceneComponentAssetRecord targetRecord = descriptor.SerializeComponent(targetTextComponent, 0, targetSaveState);
            EntityComponentPlatformOverrideState overrideState = saveState.GetOrCreatePlatformOverride("ds");
            overrideState.Payload = targetRecord.Payload;
            overrideState.SetPropertyOverride(nameof(TextComponent.FontScale));
            return new ComponentPlatformOverridePayloadService().Wrap(baseRecord, saveState);
        }

        /// <summary>
        /// Creates one automatic reflected static-mesh collider record for packaging verification.
        /// </summary>
        /// <returns>Serialized static-mesh collider record.</returns>
        static SceneComponentAssetRecord CreateStaticMeshColliderRecord() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            StaticMeshCollider3DComponent component = new StaticMeshCollider3DComponent {
                CollisionData = new StaticMeshCollisionData3D(
                    [
                        new float3(-1f, 0f, -1f),
                        new float3(1f, 0f, -1f),
                        new float3(-1f, 0f, 1f)
                    ],
                    [0, 1, 2])
            };

            return descriptor.SerializeComponent(component, 0, new EntityComponentSaveState());
        }

        /// <summary>
        /// Creates one automatic reflected sprite-component record for packaging verification.
        /// </summary>
        /// <returns>Serialized sprite-component record.</returns>
        SceneComponentAssetRecord CreateSpriteRecord() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            WriteTextureSourceFile();
            SpriteComponent spriteComponent = new SpriteComponent {
                Texture = new TestRuntimeTexture(),
                Size = new int2(128, 32),
                Color = new byte4(255, 255, 255, 255),
                SourceRect = new float4(0f, 0f, 1f, 1f),
                RenderOrder2D = 19,
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(
                nameof(SpriteComponent.Texture),
                global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateFileSystemTexture("Images/Menu/helengine-logo.png"));

            return descriptor.SerializeComponent(spriteComponent, 0, saveState);
        }

        /// <summary>
        /// Creates one MeshComponent record with editor-only Windows tessellation metadata wrapped around its common payload.
        /// </summary>
        /// <returns>Wrapped MeshComponent record prepared for scale-aware tessellation packaging.</returns>
        static SceneComponentAssetRecord CreateWrappedTessellatedMeshRecord(string platformId = "windows", bool tessellateAtCookTime = true) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            MeshComponent meshComponent = new MeshComponent {
                Model = new TestRuntimeModel()
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(nameof(MeshComponent.Model), global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateEngineCubeModel());
            SceneComponentAssetRecord baseRecord = descriptor.SerializeComponent(meshComponent, 0, saveState);
            MeshComponentModifierStackService stackService = new MeshComponentModifierStackService();
            stackService.SetStack(saveState, platformId, [
                new MeshComponentModifier(MeshComponentModifier.TessellateKind) {
                    MaxEdgeLength = 0.5,
                    AtCookTime = tessellateAtCookTime
                }
            ]);
            saveState.GetOrCreatePlatformOverride(platformId).Payload = baseRecord.Payload;
            return new ComponentPlatformOverridePayloadService().Wrap(baseRecord, saveState);
        }

        /// <summary>
        /// Creates one MeshComponent record containing only superseded per-platform tessellation members.
        /// </summary>
        /// <returns>Wrapped MeshComponent record prepared to prove superseded members are ignored.</returns>
        static SceneComponentAssetRecord CreateWrappedSupersededTessellatedMeshRecord() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            MeshComponent meshComponent = new MeshComponent {
                Model = new TestRuntimeModel()
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(nameof(MeshComponent.Model), global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateEngineCubeModel());
            SceneComponentAssetRecord baseRecord = descriptor.SerializeComponent(meshComponent, 0, saveState);
            EntityComponentPlatformOverrideState overrideState = saveState.GetOrCreatePlatformOverride("windows");
            overrideState.Payload = baseRecord.Payload;
            overrideState.SetPropertyOverride("MeshTessellate");
            overrideState.SetMemberValue("MeshTessellate", "True");
            overrideState.SetPropertyOverride("MeshTessellationMaxEdgeLength");
            overrideState.SetMemberValue("MeshTessellationMaxEdgeLength", "0.5");
            overrideState.SetPropertyOverride("MeshTessellateAtCookTime");
            overrideState.SetMemberValue("MeshTessellateAtCookTime", "True");
            return new ComponentPlatformOverridePayloadService().Wrap(baseRecord, saveState);
        }

        /// <summary>
        /// Creates one automatic reflected audio-source component record for packaging verification.
        /// </summary>
        /// <param name="audioRelativePath">Project-relative authored audio path referenced by the component.</param>
        /// <returns>Serialized audio-source component record.</returns>
        SceneComponentAssetRecord CreateAudioSourceRecord(string audioRelativePath) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            AudioSourceComponent audioSourceComponent = new AudioSourceComponent {
                Clip = new AudioAsset(),
                PlayOnStart = true,
                Loop = true,
                BusId = "music",
                Gain = 0.75f
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(
                nameof(AudioSourceComponent.Clip),
                global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateFileSystemAudio(audioRelativePath));

            return descriptor.SerializeComponent(audioSourceComponent, 0, saveState);
        }

        /// <summary>
        /// Writes one minimal PNG texture source file expected by the authored sprite packaging path.
        /// </summary>
        void WriteTextureSourceFile() {
            string relativePath = Path.Combine("assets", "Images", "Menu");
            string directoryPath = Path.Combine(ProjectRootPath, relativePath);
            Directory.CreateDirectory(directoryPath);
            string fullPath = Path.Combine(directoryPath, "helengine-logo.png");
            byte[] pngBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAAAAAMAASsJTYQAAAAASUVORK5CYII=");
            File.WriteAllBytes(fullPath, pngBytes);
        }

        static Type CreateForeignEngineType(string fullTypeName) {
            AssemblyName assemblyName = new AssemblyName("SceneComponentPackagingTransformServiceTests.Dynamic." + Guid.NewGuid().ToString("N"));
            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("main");
            return moduleBuilder.DefineType(fullTypeName, TypeAttributes.Public | TypeAttributes.Class).CreateType();
        }

        /// <summary>
        /// Writes one minimal source audio file expected by the authored audio packaging path.
        /// </summary>
        /// <param name="relativePath">Project-relative audio path.</param>
        /// <returns>Absolute authored audio source path.</returns>
        string WriteSourceAudio(string relativePath) {
            string fullPath = Path.Combine(ProjectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, [1, 2, 3, 4]);
            return fullPath;
        }

        /// <summary>
        /// Writes one deterministic audio import-settings sidecar for the requested platform.
        /// </summary>
        /// <param name="sourcePath">Absolute authored audio path whose settings should be updated.</param>
        /// <param name="platformId">Target platform id whose processor settings should be stored.</param>
        /// <param name="processorSettings">Processor settings that should be persisted for the target platform.</param>
        void ConfigureAudioImportSettings(string sourcePath, string platformId, AudioAssetProcessorSettings processorSettings) {
            ContentManager contentManager = new(new HostFileSystemContentStreamSource(ProjectRootPath));
            AssetImportManager manager = new(ProjectRootPath, contentManager);
            manager.CurrentPlatformId = platformId;
            manager.RegisterAudioImporter(new AudioImporterRegistration("test-audio", new TestAudioImporter(), [".wav"]));

            AudioAssetImportSettings settings = manager.LoadOrCreateAudioImportSettings(sourcePath);
            settings.Processor.Platforms[platformId] = processorSettings;
            manager.SaveAudioImportSettings(sourcePath, settings);
        }

        /// <summary>
        /// Writes one minimal source font file expected by the authored font packaging path.
        /// </summary>
        /// <param name="relativePath">Project-relative source font path.</param>
        void WriteSourceFont(string relativePath) {
            string fullPath = Path.Combine(ProjectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, [0x00]);
        }

        /// <summary>
        /// Creates one generated editor-font reference matching authored text scene payloads.
        /// </summary>
        /// <returns>Generated editor-font reference.</returns>
        static SceneAssetReference CreateEditorFontReference() {
            return global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateEditorUiFont();
        }

        /// <summary>
        /// Creates one file-backed font reference for authored runtime payloads.
        /// </summary>
        /// <param name="relativePath">Project-relative font path.</param>
        /// <returns>File-backed font reference.</returns>
        static SceneAssetReference CreateFileFontReference(string relativePath) {
            return global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateFileSystemFont(relativePath);
        }

        /// <summary>
        /// Creates one generated Nintendo DS debug-font reference matching authored DS text scene payloads.
        /// </summary>
        /// <returns>Generated Nintendo DS debug-font reference.</returns>
        static SceneAssetReference CreateNintendoDsDebugFontReference() {
            return global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateSerialized(
                SceneAssetReferenceSourceKind.Generated,
                "generated/editor/fonts/ds-debug.hefont",
                "editor",
                "ds-debug-font");
        }

        /// <summary>
        /// Creates one minimal packaged font asset suitable for automatic text serialization.
        /// </summary>
        /// <returns>Minimal font asset.</returns>
        static FontAsset CreatePackagedFontAsset() {
            return new FontAsset(
                new FontInfo("Demo", 16, 8f),
                null,
                new Dictionary<char, FontChar>(),
                16f,
                64,
                64) {
                    SourceTextureAsset = new TextureAsset {
                        Id = "fonts/demo-source",
                        Width = 64,
                        Height = 64,
                        ColorFormat = TextureAssetColorFormat.Rgba32,
                        AlphaPrecision = TextureAssetAlphaPrecision.A8,
                        Colors = new byte[64 * 64 * 4]
                    }
                };
        }

        /// <summary>
        /// Creates one automatic reflected debug-component record for font-reference packaging verification.
        /// </summary>
        /// <param name="fontReference">Generated font reference the authored debug component should carry.</param>
        /// <returns>Serialized debug-component record.</returns>
        SceneComponentAssetRecord CreateDebugRecord(SceneAssetReference fontReference) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            DebugComponent debugComponent = new DebugComponent {
                Font = CreatePackagedFontAsset(),
                RefreshIntervalSeconds = 0.5f,
                Padding = new int2(2, 3),
                RenderOrder2D = 17
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(nameof(DebugComponent.Font), fontReference);

            return descriptor.SerializeComponent(debugComponent, 0, saveState);
        }

        /// <summary>
        /// Reads one automatic-component asset reference through the production runtime decoder.
        /// </summary>
        /// <typeparam name="TComponent">Automatic component type represented by the payload.</typeparam>
        /// <param name="record">Packaged component record being decoded.</param>
        /// <param name="memberName">Stable reflected member name whose scene reference should be restored.</param>
        /// <returns>Restored scene asset reference stored for the requested member.</returns>
        SceneAssetReference ReadAutomaticComponentAssetReference<TComponent>(SceneComponentAssetRecord record, string memberName, bool allowRootedPackagedPath = false) where TComponent : Component, new() {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (string.IsNullOrWhiteSpace(memberName)) {
                throw new ArgumentException("Member name must be provided.", nameof(memberName));
            }

            if (allowRootedPackagedPath) {
                return ReadRootedPackagedAssetReference<TComponent>(record, memberName);
            }

            RecordingHostFileSystemContentStreamSource contentSource = new RecordingHostFileSystemContentStreamSource(BuildRootPath);
            using TestClockDrivenCore core = new TestClockDrivenCore(new CoreInitializationOptions {
                ContentStreamSource = contentSource
            });
            core.Initialize(
                new TestRenderManager3D(),
                new TestRenderManager2D(),
                new TestInputBackend(),
                new PlatformInfo("test", "test-version"));

            RuntimeSceneAssetReferenceResolver referenceResolver = core.SceneAssetReferenceResolver;
            referenceResolver.BeginOwnedAssetTracking();
            try {
                AutomaticScriptComponentRuntimeDeserializer runtimeDeserializer = new AutomaticScriptComponentRuntimeDeserializer(
                    record.ComponentTypeId,
                    typeof(TComponent));
                Assert.IsType<TComponent>(runtimeDeserializer.Deserialize(record, referenceResolver));
            } finally {
                referenceResolver.CancelOwnedAssetTracking();
            }

            string requestedAssetPath = Assert.Single(contentSource.RequestedAssetPaths);
            return global::helengine.SceneAssetReferenceFactory.CreateFileSystemTexture(requestedAssetPath);
        }

        /// <summary>
        /// Reads one rooted packaged reference using the production field readers when the desktop runtime build intentionally rejects rooted paths.
        /// </summary>
        /// <typeparam name="TComponent">Automatic component type represented by the payload.</typeparam>
        /// <param name="record">Packaged component record being decoded.</param>
        /// <param name="memberName">Stable reflected member name whose reference should be returned.</param>
        /// <returns>Rooted packaged reference restored from the current ordinal payload.</returns>
        static SceneAssetReference ReadRootedPackagedAssetReference<TComponent>(SceneComponentAssetRecord record, string memberName) where TComponent : Component, new() {
            ScriptComponentReflectionSchema schema = new ScriptComponentReflectionSchemaBuilder().Build(typeof(TComponent));
            TComponent component = new TComponent();
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            Assert.Equal(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion, reader.ReadByte());
            Assert.Equal(schema.Members.Count, reader.ReadInt32());
            for (int index = 0; index < schema.Members.Count; index++) {
                ScriptComponentReflectionMember member = schema.Members[index];
                object value;
                if (AutomaticComponentAssetReferenceSupport.IsSupportedAssetReferenceType(member.ValueType)) {
                    SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.ReadOptionalReference(reader);
                    if (reference != null) {
                        saveComponent.SetAssetReference(component, member.Name, reference);
                    }
                    value = null;
                } else if (AutomaticComponentAssetReferenceSupport.IsSupportedAssetReferenceArrayType(member.ValueType)) {
                    int referenceCount = reader.ReadInt32();
                    Assert.True(referenceCount >= -1);
                    if (referenceCount < 0) {
                        value = null;
                    } else {
                        Type elementType = member.ValueType.GetElementType();
                        Array references = Array.CreateInstance(elementType, referenceCount);
                        for (int referenceIndex = 0; referenceIndex < referenceCount; referenceIndex++) {
                            SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.ReadOptionalReference(reader);
                            if (reference != null) {
                                saveComponent.SetAssetReference(component, AutomaticComponentAssetReferenceSupport.BuildIndexedReferenceName(member.Name, referenceIndex), reference);
                            }
                        }
                        value = references;
                    }
                } else {
                    value = AutomaticScriptComponentPersistenceDescriptor.ReadSupportedMemberValue(reader, member, component, saveComponent, null);
                }
                member.SetValue(component, value);
            }

            Assert.True(saveComponent.TryGetComponentState(component, out EntityComponentSaveState saveState));
            Assert.True(saveState.TryGetAssetReference(memberName, out SceneAssetReference result));
            return Assert.IsType<SceneAssetReference>(result);
        }

        /// <summary>
        /// Reads the packaged debug-component font reference from one strict runtime payload.
        /// </summary>
        /// <param name="record">Transformed debug-component record to inspect.</param>
        /// <returns>Packaged font reference stored in the debug payload.</returns>
        static SceneAssetReference ReadDebugFontReference(SceneComponentAssetRecord record) {
            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            Assert.Equal(1, reader.ReadByte());
            SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.ReadOptionalReference(reader);
            return Assert.IsType<SceneAssetReference>(reference);
        }

        /// <summary>
        /// Deserializes one automatic reflected component from the supplied transformed record.
        /// </summary>
        /// <typeparam name="TComponent">Expected component type.</typeparam>
        /// <param name="record">Transformed record to deserialize.</param>
        /// <returns>Deserialized component instance.</returns>
        static TComponent DeserializeAutomaticComponent<TComponent>(SceneComponentAssetRecord record) where TComponent : Component {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            return Assert.IsType<TComponent>(descriptor.DeserializeComponent(record, new EntitySaveComponent(), new TestSceneAssetReferenceResolver()));
        }

        public sealed class CpuReadableModelReferenceComponent : Component {
            [CpuReadableModelReference]
            public SceneAssetReference ModelReference { get; set; }
        }

        public sealed class CpuReadableModelFieldReferenceComponent : Component {
            [CpuReadableModelReference]
            public SceneAssetReference ModelReference;
        }

        public class CpuReadableModelInheritedReferenceBaseComponent : Component {
            [CpuReadableModelReference]
            public SceneAssetReference ModelReference { get; set; }
        }

        public sealed class CpuReadableModelInheritedReferenceComponent : CpuReadableModelInheritedReferenceBaseComponent {
        }

        public sealed class CpuReadableModelUnmarkedReferenceComponent : Component {
            public SceneAssetReference ModelReference { get; set; }
        }

        public sealed class InvalidCpuReadableModelReferenceComponent : Component {
            [CpuReadableModelReference]
            public string ModelPath { get; set; }
        }

        sealed class MalformedModelImporter : IModelImporter {
            public ImportedModelAssetSet ImportModel(Stream stream) {
                if (stream == null) {
                    throw new ArgumentNullException(nameof(stream));
                }

                return new ImportedModelAssetSet(
                    new ModelAsset {
                        Positions = Array.Empty<float3>(),
                        Indices16 = new ushort[] { 0, 1, 2 },
                        Submeshes = Array.Empty<ModelSubmeshAsset>()
                    },
                    Array.Empty<ImportedModelMaterialAsset>());
            }
        }

        sealed class MultipleIndexWidthModelImporter : IModelImporter {
            public ImportedModelAssetSet ImportModel(Stream stream) {
                if (stream == null) {
                    throw new ArgumentNullException(nameof(stream));
                }

                return new ImportedModelAssetSet(
                    new ModelAsset {
                        Positions = new[] { float3.Zero },
                        Indices16 = new ushort[] { 0 },
                        Indices32 = new uint[] { 0 },
                        Submeshes = Array.Empty<ModelSubmeshAsset>()
                    },
                    Array.Empty<ImportedModelMaterialAsset>());
            }
        }

        /// <summary>
        /// Records runtime content paths while delegating stream opening to the host filesystem source.
        /// </summary>
        sealed class RecordingHostFileSystemContentStreamSource : IContentStreamSource {
            /// <summary>
            /// Host filesystem source that opens the requested package files.
            /// </summary>
            readonly HostFileSystemContentStreamSource InnerSource;

            /// <summary>
            /// Initializes one path-recording host content source.
            /// </summary>
            /// <param name="rootPath">Package root used by the delegated host source.</param>
            public RecordingHostFileSystemContentStreamSource(string rootPath) {
                InnerSource = new HostFileSystemContentStreamSource(rootPath);
                RequestedAssetPaths = new List<string>();
            }

            /// <summary>
            /// Gets the ordered runtime asset paths requested by the decoder.
            /// </summary>
            public List<string> RequestedAssetPaths { get; }

            /// <summary>
            /// Records and opens one runtime package path.
            /// </summary>
            /// <param name="assetPath">Runtime package path requested by the content manager.</param>
            /// <returns>Readable package stream.</returns>
            public Stream OpenRead(string assetPath) {
                RequestedAssetPaths.Add(assetPath);
                return InnerSource.OpenRead(assetPath);
            }
        }

        /// <summary>
        /// Deserializes one packaged automatic component record using the platform-extended schema expected by the target runtime.
        /// </summary>
        /// <typeparam name="TComponent">Expected component type.</typeparam>
        /// <param name="record">Packaged component record being decoded.</param>
        /// <param name="platformDefinition">Platform definition that owns any synthetic schema members.</param>
        /// <returns>Decoded component instance.</returns>
        static TComponent DeserializePlatformExtendedAutomaticComponent<TComponent>(
            SceneComponentAssetRecord record,
            PlatformDefinition platformDefinition) where TComponent : Component, new() {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (platformDefinition == null) {
                throw new ArgumentNullException(nameof(platformDefinition));
            }

            PlatformExtendedScriptComponentSchemaBuilder schemaBuilder = new PlatformExtendedScriptComponentSchemaBuilder();
            ScriptComponentReflectionSchema schema = schemaBuilder.Build(typeof(TComponent), platformDefinition);
            TComponent component = new TComponent();
            TestSceneAssetReferenceResolver referenceResolver = new TestSceneAssetReferenceResolver();
            referenceResolver.RegisterFont(
                global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateSerialized(
                    SceneAssetReferenceSourceKind.FileSystem,
                    "cooked/fonts/default.hefont",
                    string.Empty,
                    string.Empty),
                CreatePackagedFontAsset());
            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            Assert.Equal(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion, reader.ReadByte());
            Assert.Equal(schema.Members.Count, reader.ReadInt32());
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            for (int index = 0; index < schema.Members.Count; index++) {
                ScriptComponentReflectionMember member = schema.Members[index];
                object value;
                if (AutomaticComponentAssetReferenceSupport.IsSupportedAssetReferenceType(member.ValueType)) {
                    SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.ReadOptionalReference(reader);
                    value = reference == null ? null : referenceResolver.ResolveFont(reference);
                } else {
                    value = AutomaticScriptComponentPersistenceDescriptor.ReadSupportedMemberValue(reader, member, component, saveComponent, referenceResolver);
                }
                member.SetValue(component, value);
            }

            return component;
        }

        /// <summary>
        /// Creates one minimal platform definition whose texture cook is owned by the builder.
        /// </summary>
        /// <returns>Minimal platform definition with one builder-owned texture cook capability.</returns>
        static PlatformDefinition CreateBuilderOwnedTexturePlatformDefinition() {
            return new PlatformDefinition(
                "ds",
                "Nintendo DS",
                Array.Empty<PlatformBuildProfileDefinition>(),
                Array.Empty<PlatformGraphicsProfileDefinition>(),
                Array.Empty<PlatformAssetRequirementDefinition>(),
                Array.Empty<PlatformMaterialSchemaDefinition>(),
                Array.Empty<PlatformComponentSupportRule>(),
                Array.Empty<PlatformCodegenProfileDefinition>(),
                Array.Empty<PlatformStorageProfileDefinition>(),
                Array.Empty<PlatformMediaProfileDefinition>(),
                RuntimeGenerationContract.CreateDefault(),
                PlatformHostDebugCapability.CreateDefault(),
                new[] {
                    new PlatformAssetCookCapabilityDefinition(
                        "texture",
                        "texture",
                        PlatformAssetCookOwnershipKind.BuilderOwned,
                        "texture.settings",
                        "{\"maxResolution\":64,\"colorFormat\":\"Indexed8\",\"alphaPrecision\":\"A4\",\"indexingMethod\":\"QuantizedIndexed\"}")
                });
        }

        /// <summary>
        /// Creates one minimal platform definition that publishes a builder-owned model cook capability.
        /// </summary>
        /// <returns>Minimal platform definition with one builder-owned model cook capability.</returns>
        static PlatformDefinition CreateBuilderOwnedModelPlatformDefinition() {
            return new PlatformDefinition(
                "model-cook-platform",
                "Model Cook Platform",
                Array.Empty<PlatformBuildProfileDefinition>(),
                Array.Empty<PlatformGraphicsProfileDefinition>(),
                Array.Empty<PlatformAssetRequirementDefinition>(),
                Array.Empty<PlatformMaterialSchemaDefinition>(),
                Array.Empty<PlatformComponentSupportRule>(),
                Array.Empty<PlatformCodegenProfileDefinition>(),
                Array.Empty<PlatformStorageProfileDefinition>(),
                Array.Empty<PlatformMediaProfileDefinition>(),
                RuntimeGenerationContract.CreateDefault(),
                PlatformHostDebugCapability.CreateDefault(),
                new[] {
                    new PlatformAssetCookCapabilityDefinition(
                        "model",
                        "runtime-model",
                        PlatformAssetCookOwnershipKind.BuilderOwned,
                        "model.settings",
                        "{}",
                        null,
                        ".hasset")
                });
        }

        /// <summary>
        /// Creates one minimal platform definition whose dedicated font-atlas cook is owned by the builder.
        /// </summary>
        /// <returns>Minimal platform definition with one builder-owned font-atlas cook capability.</returns>
        static PlatformDefinition CreateBuilderOwnedFontAtlasPlatformDefinition() {
            return new PlatformDefinition(
                "external-platform",
                "External Platform",
                Array.Empty<PlatformBuildProfileDefinition>(),
                Array.Empty<PlatformGraphicsProfileDefinition>(),
                Array.Empty<PlatformAssetRequirementDefinition>(),
                Array.Empty<PlatformMaterialSchemaDefinition>(),
                Array.Empty<PlatformComponentSupportRule>(),
                Array.Empty<PlatformCodegenProfileDefinition>(),
                Array.Empty<PlatformStorageProfileDefinition>(),
                Array.Empty<PlatformMediaProfileDefinition>(),
                RuntimeGenerationContract.CreateDefault(),
                PlatformHostDebugCapability.CreateDefault(),
                new[] {
                    new PlatformAssetCookCapabilityDefinition(
                        "font-atlas-texture",
                        "runtime-texture",
                        PlatformAssetCookOwnershipKind.BuilderOwned,
                        "texture.settings",
                        "{\"maxResolution\":64,\"colorFormat\":\"Indexed8\",\"alphaPrecision\":\"A8\",\"indexingMethod\":\"QuantizedIndexed\"}",
                        null,
                        ".hetex")
                });
        }

        /// <summary>
        /// Creates one minimal platform definition whose builder-owned font-atlas texture outputs resolve through rooted packaged runtime paths.
        /// </summary>
        /// <returns>Minimal platform definition with rooted packaged runtime-path support.</returns>
        static PlatformDefinition CreateRootedBuilderOwnedFontAtlasPlatformDefinition() {
            return new PlatformDefinition(
                "ps2",
                "PlayStation 2",
                Array.Empty<PlatformBuildProfileDefinition>(),
                Array.Empty<PlatformGraphicsProfileDefinition>(),
                Array.Empty<PlatformAssetRequirementDefinition>(),
                Array.Empty<PlatformMaterialSchemaDefinition>(),
                Array.Empty<PlatformComponentSupportRule>(),
                Array.Empty<PlatformCodegenProfileDefinition>(),
                Array.Empty<PlatformStorageProfileDefinition>(),
                Array.Empty<PlatformMediaProfileDefinition>(),
                new RuntimeGenerationContract(
                    RuntimeMaterialResolutionMode.CookedPlatformOwned,
                    true,
                    PackagedPathPolicy.RootedOrContentRelative),
                PlatformHostDebugCapability.CreateDefault(),
                new[] {
                    new PlatformAssetCookCapabilityDefinition(
                        "font-atlas-texture",
                        "runtime-texture",
                        PlatformAssetCookOwnershipKind.BuilderOwned,
                        "texture.settings",
                        "{\"maxResolution\":64,\"colorFormat\":\"Indexed8\",\"alphaPrecision\":\"A8\",\"indexingMethod\":\"QuantizedIndexed\"}",
                        null,
                        ".hetex")
                });
        }

        /// <summary>
        /// Creates one minimal DS platform definition that exposes the synthetic text background-layer member.
        /// </summary>
        /// <returns>Minimal DS platform definition with one synthetic text member.</returns>
        static PlatformDefinition CreateDsSyntheticTextPlatformDefinition() {
            return new PlatformDefinition(
                "ds",
                "Nintendo DS",
                Array.Empty<PlatformBuildProfileDefinition>(),
                Array.Empty<PlatformGraphicsProfileDefinition>(),
                Array.Empty<PlatformAssetRequirementDefinition>(),
                Array.Empty<PlatformMaterialSchemaDefinition>(),
                Array.Empty<PlatformComponentSupportRule>(),
                Array.Empty<PlatformCodegenProfileDefinition>(),
                Array.Empty<PlatformStorageProfileDefinition>(),
                Array.Empty<PlatformMediaProfileDefinition>(),
                componentMemberDefinitions: [
                    new PlatformComponentMemberDefinition(
                        "helengine.TextComponent",
                        "BGLayer",
                        "BG Layer",
                        PlatformComponentMemberValueKind.Int32,
                        "0",
                        0)
                ]);
        }

        /// <summary>
        /// Creates one minimal platform definition whose selected build profile resolves to one big-endian codegen profile.
        /// </summary>
        /// <returns>Minimal big-endian platform definition.</returns>
        static PlatformDefinition CreateBigEndianStaticMeshPlatformDefinition() {
            return new PlatformDefinition(
                "gamecube",
                "GameCube",
                [
                    new PlatformBuildProfileDefinition(
                        "main",
                        "Main",
                        "Main build profile",
                        "default",
                        "gc-cpp",
                        Array.Empty<PlatformSettingDefinition>())
                ],
                [
                    new PlatformGraphicsProfileDefinition(
                        "default",
                        "Default",
                        "Default graphics profile",
                        Array.Empty<PlatformSettingDefinition>())
                ],
                Array.Empty<PlatformAssetRequirementDefinition>(),
                Array.Empty<PlatformMaterialSchemaDefinition>(),
                Array.Empty<PlatformComponentSupportRule>(),
                [
                    new PlatformCodegenProfileDefinition(
                        "gc-cpp",
                        "GC C++",
                        "GameCube codegen",
                        PlatformCodegenLanguage.Cpp,
                        PlatformSerializationEndianness.BigEndian,
                        Array.Empty<PlatformSettingDefinition>())
                ],
                Array.Empty<PlatformStorageProfileDefinition>(),
                Array.Empty<PlatformMediaProfileDefinition>());
        }

        /// <summary>
        /// Imports deterministic audio metadata for authored transform-service tests without relying on one real platform codec.
        /// </summary>
        sealed class TestAudioImporter : IAudioImporter {
            /// <summary>
            /// Produces one stable imported audio payload for the supplied source stream.
            /// </summary>
            /// <param name="stream">Source audio stream being imported.</param>
            /// <returns>Deterministic imported audio payload.</returns>
            public ImportedAudioSource ImportAudio(Stream stream) {
                if (stream == null) {
                    throw new ArgumentNullException(nameof(stream));
                }

                return new ImportedAudioSource {
                    Channels = 2,
                    SampleRate = 44100,
                    DurationSeconds = 3.5f,
                    Pcm16Bytes = [1, 2, 3, 4]
                };
            }
        }

        /// <summary>
        /// Provides a controllable text-sprite bake result for transform-service tests.
        /// </summary>
        sealed class StubTextComponentSpriteBakeService : ITextComponentSpriteBakeService {
            /// <summary>
            /// Gets whether the bake service has been invoked.
            /// </summary>
            public bool WasCalled { get; private set; }

            /// <summary>
            /// Gets the last bake request received by the stub.
            /// </summary>
            public TextComponentSpriteBakeRequest LastRequest { get; private set; }

            /// <summary>
            /// Returns one deterministic generated texture bake result for the supplied request.
            /// </summary>
            /// <param name="request">Bake request issued by the transform service.</param>
            /// <returns>Generated bake result.</returns>
            public TextComponentSpriteBakeResult Bake(TextComponentSpriteBakeRequest request) {
                WasCalled = true;
                LastRequest = request;

                return new TextComponentSpriteBakeResult(
                    new TextureAsset {
                        Id = "generated:text-sprite",
                        Width = 128,
                        Height = 32,
                        ColorFormat = TextureAssetColorFormat.Rgba32,
                        AlphaPrecision = TextureAssetAlphaPrecision.A8,
                        Colors = new byte[128 * 32 * 4]
                    },
                    new TextureAssetProcessorSettings {
                        ColorFormat = TextureAssetColorFormat.Rgba32,
                        AlphaPrecision = TextureAssetAlphaPrecision.A8
                    },
                    "text-scene-0");
            }
        }

        /// <summary>
        /// Provides one deterministic cooked runtime payload for static-mesh packaging tests.
        /// </summary>
        sealed class StubStaticMeshCollisionCookProcessor3D : IStaticMeshCollisionCookProcessor3D {
            /// <summary>
            /// Gets the stable test payload format identifier.
            /// </summary>
            public string FormatId => "test.static-mesh";

            /// <summary>
            /// Gets the stable binary payload format identifier written into the HELE header.
            /// </summary>
            public ushort BinaryFormatId => 0x7A01;

            /// <summary>
            /// Gets the binary payload format version written into the HELE header.
            /// </summary>
            public byte BinaryFormatVersion => 3;

            /// <summary>
            /// Writes one deterministic cooked payload for test assertions.
            /// </summary>
            /// <param name="writer">Endian-aware writer owned by Helengine.</param>
            /// <param name="collisionData">Generic collision data passed by the packaging service.</param>
            public void WritePayload(EngineBinaryWriter writer, StaticMeshCollisionData3D collisionData) {
                if (writer == null) {
                    throw new ArgumentNullException(nameof(writer));
                } else if (collisionData == null) {
                    throw new ArgumentNullException(nameof(collisionData));
                }

                writer.WriteInt32(collisionData.TriangleCount);
                writer.WriteSingle(0.25f);
            }
        }
    }
}


