using System.Text;
using System.Xml.Linq;

namespace helengine.patching {
    /// <summary>
    /// Writes a generated project file for an engine build plan.
    /// </summary>
    public sealed class EngineProjectWriter {
        /// <summary>
        /// Writes the project file for the provided build plan.
        /// </summary>
        /// <param name="plan">Build plan containing project details.</param>
        public void WriteProject(EngineBuildPlan plan) {
            if (plan == null) {
                throw new ArgumentNullException(nameof(plan));
            }

            Directory.CreateDirectory(plan.BuildRootPath);

            XDocument doc = BuildProjectDocument(plan);
            using var stream = File.Create(plan.ProjectPath);
            doc.Save(stream);
        }

        /// <summary>
        /// Builds the XML document representing the generated project file.
        /// </summary>
        /// <param name="plan">Build plan.</param>
        /// <returns>XML document ready to save.</returns>
        XDocument BuildProjectDocument(EngineBuildPlan plan) {
            var propertyGroup = new XElement("PropertyGroup",
                new XElement("TargetFramework", "net9.0"),
                new XElement("ImplicitUsings", "enable"),
                new XElement("Nullable", "disable"),
                new XElement("AssemblyName", plan.AssemblyName),
                new XElement("RootNamespace", plan.AssemblyName));

            if (plan.Defines != null && plan.Defines.Count > 0) {
                string defineValue = BuildDefineConstants(plan.Defines);
                propertyGroup.Add(new XElement("DefineConstants", defineValue));
            }

            var itemGroup = new XElement("ItemGroup");
            IReadOnlyList<string> sources = plan.SourceFiles;
            if (sources != null) {
                for (int i = 0; i < sources.Count; i++) {
                    itemGroup.Add(new XElement("Compile", new XAttribute("Include", sources[i])));
                }
            }

            var project = new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"), propertyGroup, itemGroup);
            return new XDocument(project);
        }

        /// <summary>
        /// Builds the define constants string from a list of defines.
        /// </summary>
        /// <param name="defines">Define list.</param>
        /// <returns>Semicolon-separated define string.</returns>
        string BuildDefineConstants(IReadOnlyList<string> defines) {
            var builder = new StringBuilder();
            for (int i = 0; i < defines.Count; i++) {
                if (i > 0) {
                    builder.Append(';');
                }

                builder.Append(defines[i]);
            }

            return builder.ToString();
        }
    }
}
