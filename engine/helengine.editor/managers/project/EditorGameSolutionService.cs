using System.Security.Cryptography;
using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Generates one C# solution for the current game project and opens it in the configured IDE.
    /// </summary>
    public sealed class EditorGameSolutionService {
        /// <summary>
        /// Project folder name used by the generated C# project to enumerate game scripts.
        /// </summary>
        const string AssetsFolderName = "assets";

        /// <summary>
        /// Project file extension used by the generated game project.
        /// </summary>
        const string ProjectFileExtension = ".csproj";

        /// <summary>
        /// Solution file extension used by the generated IDE workspace.
        /// </summary>
        const string SolutionFileExtension = ".sln";

        /// <summary>
        /// SDK-style project type GUID used by Visual Studio solutions for C# projects.
        /// </summary>
        const string CSharpProjectTypeGuid = "FAE04EC0-301F-11D3-BF4B-00C04F79EFBC";

        /// <summary>
        /// Default framework used by the generated game project.
        /// </summary>
        const string TargetFrameworkValue = "net9.0";

        /// <summary>
        /// Relative output folder used for generated intermediate build files.
        /// </summary>
        const string IntermediateOutputPathValue = "../obj/";

        /// <summary>
        /// Relative output folder used for generated binary build files.
        /// </summary>
        const string OutputPathValue = "../bin/";

        /// <summary>
        /// Legacy intermediate folder that should not remain under the assets project root.
        /// </summary>
        const string LegacyIntermediateFolderName = "obj";

        /// <summary>
        /// Legacy binary folder that should not remain under the assets project root.
        /// </summary>
        const string LegacyBinaryFolderName = "bin";

        /// <summary>
        /// Absolute path to the game project root.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Absolute path to the assets folder that owns the generated C# project.
        /// </summary>
        readonly string ProjectAssetsRootPath;

        /// <summary>
        /// User-visible game project name loaded from the canonical project document.
        /// </summary>
        readonly string ProjectName;

        /// <summary>
        /// Sanitized identifier used for file names and assembly metadata.
        /// </summary>
        readonly string ProjectIdentifier;

        /// <summary>
        /// Generated project file path.
        /// </summary>
        readonly string ProjectFilePath;

        /// <summary>
        /// Generated solution file path.
        /// </summary>
        readonly string SolutionFilePath;

        /// <summary>
        /// Stable project GUID emitted into the generated solution.
        /// </summary>
        readonly Guid ProjectGuid;

        /// <summary>
        /// IDE launcher used after generating the solution files.
        /// </summary>
        readonly IEditorIdeLauncher IdeLauncher;

        /// <summary>
        /// Detector used to skip reopening a solution that is already active in the IDE.
        /// </summary>
        readonly IEditorIdeSolutionDetector SolutionDetector;

        /// <summary>
        /// Initializes one solution generator for the supplied game project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative game project root path.</param>
        /// <param name="projectName">Display name of the game project.</param>
        /// <param name="ideLauncher">Launcher used to open the generated solution.</param>
        public EditorGameSolutionService(string projectRootPath, string projectName, IEditorIdeLauncher ideLauncher)
            : this(projectRootPath, projectName, ideLauncher, new EditorVisualStudioLauncher()) {
        }

        /// <summary>
        /// Initializes one solution generator for the supplied game project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative game project root path.</param>
        /// <param name="projectName">Display name of the game project.</param>
        /// <param name="ideLauncher">Launcher used to open the generated solution.</param>
        /// <param name="solutionDetector">Detector used to skip reopening an already-open solution.</param>
        public EditorGameSolutionService(string projectRootPath, string projectName, IEditorIdeLauncher ideLauncher, IEditorIdeSolutionDetector solutionDetector) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (string.IsNullOrWhiteSpace(projectName)) {
                throw new ArgumentException("Project name must be provided.", nameof(projectName));
            }
            if (ideLauncher == null) {
                throw new ArgumentNullException(nameof(ideLauncher));
            }
            if (solutionDetector == null) {
                throw new ArgumentNullException(nameof(solutionDetector));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            ProjectAssetsRootPath = Path.Combine(ProjectRootPath, AssetsFolderName);
            ProjectName = projectName;
            ProjectIdentifier = SanitizeIdentifier(projectName);
            if (string.IsNullOrWhiteSpace(ProjectIdentifier)) {
                ProjectIdentifier = "Game";
            }

            ProjectFilePath = Path.Combine(ProjectAssetsRootPath, ProjectIdentifier + ProjectFileExtension);
            SolutionFilePath = Path.Combine(ProjectRootPath, ProjectIdentifier + SolutionFileExtension);
            ProjectGuid = CreateStableGuid(ProjectRootPath + "|" + ProjectName);
            IdeLauncher = ideLauncher;
            SolutionDetector = solutionDetector;
        }

        /// <summary>
        /// Gets the absolute path to the generated project file.
        /// </summary>
        public string GeneratedProjectFilePath => ProjectFilePath;

        /// <summary>
        /// Gets the absolute path to the generated solution file.
        /// </summary>
        public string GeneratedSolutionFilePath => SolutionFilePath;

        /// <summary>
        /// Generates the solution and project files, overwriting any older copies in place.
        /// </summary>
        /// <returns>Absolute path to the generated solution file.</returns>
        public string GenerateSolutionFiles() {
            Directory.CreateDirectory(ProjectRootPath);
            Directory.CreateDirectory(ProjectAssetsRootPath);
            DeleteLegacyProjectFolders();
            File.WriteAllText(ProjectFilePath, BuildProjectFileContents());
            File.WriteAllText(SolutionFilePath, BuildSolutionFileContents());
            return SolutionFilePath;
        }

        /// <summary>
        /// Generates the solution files and opens the solution in the configured IDE.
        /// </summary>
        public void OpenSolutionInIde() {
            string solutionPath = GenerateSolutionFiles();
            if (SolutionDetector.IsSolutionAlreadyOpen(solutionPath)) {
                return;
            }

            IdeLauncher.OpenSolution(solutionPath);
        }

        /// <summary>
        /// Builds the SDK-style project file for the game's C# script sources.
        /// </summary>
        /// <returns>Project file contents.</returns>
        string BuildProjectFileContents() {
            string compileGlob = "**/*.cs";
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
            builder.AppendLine("  <PropertyGroup>");
            builder.AppendLine("    <TargetFramework>" + TargetFrameworkValue + "</TargetFramework>");
            builder.AppendLine("    <OutputType>Library</OutputType>");
            builder.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
            builder.AppendLine("    <Nullable>disable</Nullable>");
            builder.AppendLine("    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>");
            builder.AppendLine("    <EnableDefaultNoneItems>false</EnableDefaultNoneItems>");
            builder.AppendLine("    <EnableDefaultContentItems>false</EnableDefaultContentItems>");
            builder.AppendLine("    <EnableDefaultEmbeddedResourceItems>false</EnableDefaultEmbeddedResourceItems>");
            builder.AppendLine("    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>");
            builder.AppendLine("    <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>");
            builder.AppendLine("    <ImplicitUsings>disable</ImplicitUsings>");
            builder.AppendLine("    <BaseIntermediateOutputPath>" + IntermediateOutputPathValue + "</BaseIntermediateOutputPath>");
            builder.AppendLine("    <MSBuildProjectExtensionsPath>" + IntermediateOutputPathValue + "</MSBuildProjectExtensionsPath>");
            builder.AppendLine("    <BaseOutputPath>" + OutputPathValue + "</BaseOutputPath>");
            builder.AppendLine("    <AssemblyName>" + EscapeXml(ProjectIdentifier) + "</AssemblyName>");
            builder.AppendLine("    <RootNamespace>" + EscapeXml(ProjectIdentifier) + "</RootNamespace>");
            builder.AppendLine("  </PropertyGroup>");
            builder.AppendLine("  <ItemGroup>");
            builder.AppendLine("    <Compile Include=\"" + EscapeXml(compileGlob) + "\" />");
            builder.AppendLine("  </ItemGroup>");
            builder.AppendLine("</Project>");
            return builder.ToString();
        }

        /// <summary>
        /// Builds the Visual Studio solution file for the generated project.
        /// </summary>
        /// <returns>Solution file contents.</returns>
        string BuildSolutionFileContents() {
            string projectFileName = Path.GetFileName(ProjectFilePath);
            string relativeProjectFileName = AssetsFolderName + "/" + projectFileName;
            string projectGuidText = ProjectGuid.ToString("B").ToUpperInvariant();

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
            builder.AppendLine("# Visual Studio Version 17");
            builder.AppendLine("VisualStudioVersion = 17.0.31903.59");
            builder.AppendLine("MinimumVisualStudioVersion = 10.0.40219.1");
            builder.AppendLine("Project(\"{" + CSharpProjectTypeGuid + "}\") = \"" + EscapeSolutionText(ProjectName) + "\", \"" + EscapeSolutionText(relativeProjectFileName) + "\", \"" + projectGuidText + "\"");
            builder.AppendLine("EndProject");
            builder.AppendLine("Global");
            builder.AppendLine("	GlobalSection(SolutionConfigurationPlatforms) = preSolution");
            builder.AppendLine("		Debug|Any CPU = Debug|Any CPU");
            builder.AppendLine("		Release|Any CPU = Release|Any CPU");
            builder.AppendLine("	EndGlobalSection");
            builder.AppendLine("	GlobalSection(ProjectConfigurationPlatforms) = postSolution");
            builder.AppendLine("		" + projectGuidText + ".Debug|Any CPU.ActiveCfg = Debug|Any CPU");
            builder.AppendLine("		" + projectGuidText + ".Debug|Any CPU.Build.0 = Debug|Any CPU");
            builder.AppendLine("		" + projectGuidText + ".Release|Any CPU.ActiveCfg = Release|Any CPU");
            builder.AppendLine("		" + projectGuidText + ".Release|Any CPU.Build.0 = Release|Any CPU");
            builder.AppendLine("	EndGlobalSection");
            builder.AppendLine("EndGlobal");
            return builder.ToString();
        }

        /// <summary>
        /// Deletes legacy output folders that may remain inside the assets project root from earlier layouts.
        /// </summary>
        void DeleteLegacyProjectFolders() {
            string legacyObjPath = Path.Combine(ProjectAssetsRootPath, LegacyIntermediateFolderName);
            if (Directory.Exists(legacyObjPath)) {
                Directory.Delete(legacyObjPath, true);
            }

            string legacyBinPath = Path.Combine(ProjectAssetsRootPath, LegacyBinaryFolderName);
            if (Directory.Exists(legacyBinPath)) {
                Directory.Delete(legacyBinPath, true);
            }
        }

        /// <summary>
        /// Escapes one text value for inclusion in XML text content.
        /// </summary>
        /// <param name="value">Text value to escape.</param>
        /// <returns>XML-safe text value.</returns>
        static string EscapeXml(string value) {
            if (string.IsNullOrEmpty(value)) {
                return string.Empty;
            }

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        /// <summary>
        /// Escapes one solution string for safe inclusion in a quoted field.
        /// </summary>
        /// <param name="value">Text value to escape.</param>
        /// <returns>Solution-safe text value.</returns>
        static string EscapeSolutionText(string value) {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\"", "\"\"");
        }

        /// <summary>
        /// Converts one arbitrary project name into a file-safe and assembly-safe identifier.
        /// </summary>
        /// <param name="value">Original project name.</param>
        /// <returns>Sanitized identifier string.</returns>
        static string SanitizeIdentifier(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++) {
                char ch = value[i];
                if (char.IsLetterOrDigit(ch)) {
                    builder.Append(ch);
                } else {
                    builder.Append('_');
                }
            }

            string result = builder.ToString().Trim('_');
            return result.Replace("__", "_");
        }

        /// <summary>
        /// Creates one stable GUID from the supplied seed string.
        /// </summary>
        /// <param name="seed">Seed value used to derive the GUID.</param>
        /// <returns>Deterministic GUID.</returns>
        static Guid CreateStableGuid(string seed) {
            byte[] hash = SHA1.HashData(Encoding.UTF8.GetBytes(seed ?? string.Empty));
            Span<byte> guidBytes = stackalloc byte[16];
            hash.AsSpan(0, guidBytes.Length).CopyTo(guidBytes);
            guidBytes[7] = (byte)((guidBytes[7] & 0x0F) | 0x50);
            guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
            return new Guid(guidBytes);
        }
    }
}
