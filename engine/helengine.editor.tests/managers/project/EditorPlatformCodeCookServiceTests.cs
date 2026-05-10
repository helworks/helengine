using helengine.baseplatform.Definitions;
using helengine.baseplatform.Manifest;
using helengine.baseplatform.Profiles;
using Xunit;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies authored code modules are mapped into shared build-graph module records.
/// </summary>
public sealed class EditorPlatformCodeCookServiceTests : IDisposable {
    readonly string ProjectRootPath;
    readonly string OutputRootPath;

    public EditorPlatformCodeCookServiceTests() {
        ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-code-cook-tests", Guid.NewGuid().ToString("N"));
        OutputRootPath = Path.Combine(ProjectRootPath, "Build", "code");
        Directory.CreateDirectory(ProjectRootPath);
        Directory.CreateDirectory(OutputRootPath);
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Scripts"));
        File.WriteAllText(
            Path.Combine(ProjectRootPath, "assets", "Scripts", "PlayerController.cs"),
            "public sealed class PlayerController { }");
    }

    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    [Fact]
    public void Compile_code_modules_reads_manifest_and_emits_module_records() {
        RecordingCodegenToolRunner toolRunner = new();
        EditorPlatformCodeCookService service = new(ProjectRootPath, toolRunner);
        EditorCodeModuleManifestDocument manifestDocument = new([
            new EditorCodeModuleManifestEntry("gameplay", "assets/Scripts", [], ["always-loaded"])
        ]);

        PlatformBuildCodeModule[] modules = service.CompileModules(
            manifestDocument,
            "windows",
            "windows-loose-files",
            "/tmp/fake-codegen.exe",
            new PlatformCodegenProfileDefinition(
                "windows-cpp",
                "Windows C++",
                "Default Windows C++ codegen profile.",
                PlatformCodegenLanguage.Cpp,
                PlatformSerializationEndianness.LittleEndian,
                []),
            [],
            new Dictionary<string, string> {
                ["write-conversion-report"] = "true"
            },
            OutputRootPath);

        Assert.Single(modules);
        Assert.Equal("gameplay", modules[0].ModuleId);
        Assert.Equal("code/gameplay", modules[0].ArtifactId);
        Assert.Equal("windows-loose-files", modules[0].RuntimeSpecializationId);
        Assert.Single(toolRunner.Invocations);
        Assert.Contains("--platform", toolRunner.Invocations[0].Arguments);
        Assert.Contains("windows", toolRunner.Invocations[0].Arguments);
        Assert.Contains("--set", toolRunner.Invocations[0].Arguments);
        Assert.Contains("runtime-specialization=windows-loose-files", toolRunner.Invocations[0].Arguments);
        Assert.Contains("--endianness", toolRunner.Invocations[0].Arguments);
        Assert.Contains("little", toolRunner.Invocations[0].Arguments);
    }

    [Fact]
    public void Compile_code_modules_respects_selected_module_ids() {
        RecordingCodegenToolRunner toolRunner = new();
        EditorPlatformCodeCookService service = new(ProjectRootPath, toolRunner);
        EditorCodeModuleManifestDocument manifestDocument = new([
            new EditorCodeModuleManifestEntry("gameplay", "assets/Scripts", [], ["always-loaded"]),
            new EditorCodeModuleManifestEntry("ui", "assets/Scripts/Ui", ["gameplay"], ["scene-loaded"])
        ]);
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Scripts", "Ui"));
        File.WriteAllText(Path.Combine(ProjectRootPath, "assets", "Scripts", "Ui", "Hud.cs"), "public sealed class Hud { }");

        PlatformBuildCodeModule[] modules = service.CompileModules(
            manifestDocument,
            "windows",
            "windows-loose-files",
            "/tmp/fake-codegen.exe",
            new PlatformCodegenProfileDefinition(
                "windows-cpp",
                "Windows C++",
                "Default Windows C++ codegen profile.",
                PlatformCodegenLanguage.Cpp,
                PlatformSerializationEndianness.LittleEndian,
                []),
            ["ui"],
            new Dictionary<string, string>(),
            OutputRootPath);

        Assert.Collection(
            modules,
            module => Assert.Equal("gameplay", module.ModuleId),
            module => Assert.Equal("ui", module.ModuleId));
        Assert.Equal(2, toolRunner.Invocations.Count);
        Assert.Equal(ProjectRootPath, toolRunner.Invocations[0].WorkingDirectory);
        Assert.Equal(ProjectRootPath, toolRunner.Invocations[1].WorkingDirectory);
    }

    [Fact]
    public void Compile_code_modules_runs_codegen_outside_module_output_directory() {
        RecordingCodegenToolRunner toolRunner = new();
        EditorPlatformCodeCookService service = new(ProjectRootPath, toolRunner);
        EditorCodeModuleManifestDocument manifestDocument = new([
            new EditorCodeModuleManifestEntry("gameplay", "assets/Scripts", [], ["always-loaded"])
        ]);

        service.CompileModules(
            manifestDocument,
            "windows",
            "windows-loose-files",
            "/tmp/fake-codegen.exe",
            new PlatformCodegenProfileDefinition(
                "windows-cpp",
                "Windows C++",
                "Default Windows C++ codegen profile.",
                PlatformCodegenLanguage.Cpp,
                PlatformSerializationEndianness.LittleEndian,
                []),
            [],
            new Dictionary<string, string>(),
            OutputRootPath);

        Assert.Single(toolRunner.Invocations);
        Assert.Equal(ProjectRootPath, toolRunner.Invocations[0].WorkingDirectory);
        Assert.DoesNotContain(
            Path.Combine("Build", "code", "gameplay"),
            toolRunner.Invocations[0].WorkingDirectory,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_code_modules_throws_when_dependency_module_is_missing() {
        RecordingCodegenToolRunner toolRunner = new();
        EditorPlatformCodeCookService service = new(ProjectRootPath, toolRunner);
        EditorCodeModuleManifestDocument manifestDocument = new([
            new EditorCodeModuleManifestEntry("ui", "assets/Scripts/Ui", ["gameplay"], ["scene-loaded"])
        ]);
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Scripts", "Ui"));
        File.WriteAllText(Path.Combine(ProjectRootPath, "assets", "Scripts", "Ui", "Hud.cs"), "public sealed class Hud { }");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => service.CompileModules(
            manifestDocument,
            "windows",
            "windows-loose-files",
            "/tmp/fake-codegen.exe",
            new PlatformCodegenProfileDefinition(
                "windows-cpp",
                "Windows C++",
                "Default Windows C++ codegen profile.",
                PlatformCodegenLanguage.Cpp,
                PlatformSerializationEndianness.LittleEndian,
                []),
            ["ui"],
            new Dictionary<string, string>(),
            OutputRootPath));

        Assert.Contains("depends on missing module 'gameplay'", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(toolRunner.Invocations);
    }

    [Fact]
    public void Compile_code_modules_throws_when_selected_module_id_is_unknown() {
        RecordingCodegenToolRunner toolRunner = new();
        EditorPlatformCodeCookService service = new(ProjectRootPath, toolRunner);
        EditorCodeModuleManifestDocument manifestDocument = new([
            new EditorCodeModuleManifestEntry("gameplay", "assets/Scripts", [], ["always-loaded"])
        ]);
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Scripts"));
        File.WriteAllText(Path.Combine(ProjectRootPath, "assets", "Scripts", "PlayerController.cs"), "public sealed class PlayerController { }");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => service.CompileModules(
            manifestDocument,
            "windows",
            "windows-loose-files",
            "/tmp/fake-codegen.exe",
            new PlatformCodegenProfileDefinition(
                "windows-cpp",
                "Windows C++",
                "Default Windows C++ codegen profile.",
                PlatformCodegenLanguage.Cpp,
                PlatformSerializationEndianness.LittleEndian,
                []),
            ["ui"],
            new Dictionary<string, string>(),
            OutputRootPath));

        Assert.Contains("Selected code module id(s) ui were not found", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(toolRunner.Invocations);
    }

    [Fact]
    public void Compile_code_modules_excludes_editor_modules_from_runtime_build_outputs() {
        RecordingCodegenToolRunner toolRunner = new();
        EditorPlatformCodeCookService service = new(ProjectRootPath, toolRunner);
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Scripts", "Tools"));
        File.WriteAllText(Path.Combine(ProjectRootPath, "assets", "Scripts", "Tools", "MenuCommand.cs"), "public sealed class MenuCommand { }");
        EditorCodeModuleManifestDocument manifestDocument = new([
            new EditorCodeModuleManifestEntry("gameplay", "assets/Scripts", [], ["always-loaded"], EditorCodeModuleKind.Runtime),
            new EditorCodeModuleManifestEntry("menu.tools", "assets/Scripts/Tools", ["gameplay"], ["always-loaded"], EditorCodeModuleKind.Editor)
        ]);

        PlatformBuildCodeModule[] modules = service.CompileModules(
            manifestDocument,
            "windows",
            "windows-loose-files",
            "/tmp/fake-codegen.exe",
            new PlatformCodegenProfileDefinition(
                "windows-cpp",
                "Windows C++",
                "Default Windows C++ codegen profile.",
                PlatformCodegenLanguage.Cpp,
                PlatformSerializationEndianness.LittleEndian,
                []),
            [],
            new Dictionary<string, string>(),
            OutputRootPath);

        Assert.Single(modules);
        Assert.Equal("gameplay", modules[0].ModuleId);
        Assert.DoesNotContain(modules, module => string.Equals(module.ModuleId, "menu.tools", StringComparison.OrdinalIgnoreCase));
        Assert.Single(toolRunner.Invocations);
    }

    [Fact]
    public void Compile_code_modules_normalizes_generated_float4_orientation_temporaries() {
        RecordingCodegenToolRunner toolRunner = new() {
            GeneratedRelativePath = Path.Combine("DirectionalShadowSunSweepComponent.cpp"),
            GeneratedContents =
                "#include \"DirectionalShadowSunSweepComponent.hpp\"\n"
                + "void DirectionalShadowSunSweepComponent::Update()\n"
                + "{\n"
                + "    float4 *orientation;\n"
                + "    float4->CreateFromYawPitchRoll(static_cast<float>(yawRadians), this->PitchRadians, 0.0f, orientation);\n"
                + "    orientation->Normalize();\n"
                + "    Parent->LocalOrientation = orientation;\n"
                + "}\n"
        };
        EditorPlatformCodeCookService service = new(ProjectRootPath, toolRunner);
        EditorCodeModuleManifestDocument manifestDocument = new([
            new EditorCodeModuleManifestEntry("gameplay", "assets/Scripts", [], ["always-loaded"])
        ]);

        service.CompileModules(
            manifestDocument,
            "ps2",
            "ps2-disc",
            "/tmp/fake-codegen.exe",
            new PlatformCodegenProfileDefinition(
                "ps2-cpp",
                "PS2 C++",
                "Default PS2 C++ codegen profile.",
                PlatformCodegenLanguage.Cpp,
                PlatformSerializationEndianness.LittleEndian,
                []),
            [],
            new Dictionary<string, string>(),
            OutputRootPath);

        string generatedSourcePath = Path.Combine(OutputRootPath, "gameplay", "DirectionalShadowSunSweepComponent.cpp");
        string generatedSource = File.ReadAllText(generatedSourcePath);
        Assert.Contains("    float4 orientation;", generatedSource);
        Assert.Contains("    float4::CreateFromYawPitchRoll(static_cast<float>(yawRadians), this->PitchRadians, 0.0f, orientation);", generatedSource);
        Assert.Contains("    orientation.Normalize();", generatedSource);
        Assert.Contains("    Parent->LocalOrientation = orientation;", generatedSource);
        Assert.DoesNotContain("float4 *orientation;", generatedSource);
        Assert.DoesNotContain("float4->CreateFromYawPitchRoll", generatedSource);
        Assert.DoesNotContain("orientation->Normalize()", generatedSource);
    }

    sealed class RecordingCodegenToolRunner : IEditorCodegenToolRunner {
        public List<(string ToolPath, IReadOnlyList<string> Arguments, string WorkingDirectory)> Invocations { get; } = [];

        public string GeneratedRelativePath { get; set; } = "module.generated.cpp";

        public string GeneratedContents { get; set; } = "// generated";

        public void Run(string toolPath, IReadOnlyList<string> arguments, string workingDirectory) {
            Invocations.Add((toolPath, [.. arguments], workingDirectory));
            string outputPath = ResolveOutputPath(arguments);
            Directory.CreateDirectory(outputPath);
            File.WriteAllText(Path.Combine(outputPath, GeneratedRelativePath), GeneratedContents);
        }

        static string ResolveOutputPath(IReadOnlyList<string> arguments) {
            for (int index = 0; index < arguments.Count - 1; index++) {
                if (string.Equals(arguments[index], "--output", StringComparison.OrdinalIgnoreCase)) {
                    return arguments[index + 1];
                }
            }

            throw new InvalidOperationException("Expected generated module codegen invocation to supply `--output`.");
        }
    }
}
