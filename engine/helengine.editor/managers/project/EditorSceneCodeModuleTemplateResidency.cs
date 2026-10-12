using System.Reflection;

namespace helengine.editor {
    /// <summary>Protects template definitions whose lifetime owner varies with the including translation unit.</summary>
    internal static class EditorSceneCodeModuleTemplateResidency {
        /// <summary>Identifies a generic definition that cannot move safely or an owner-dependent header template body.</summary>
        /// <param name="type">Actual authored type represented by the generated physical source.</param>
        /// <param name="headerPath">Official emitted header containing any method-level template definitions.</param>
        /// <returns>True when the complete physical source must remain resident; ordinary nongeneric methods use their source owner's lifetime hooks.</returns>
        internal static bool RequiresResidentSource(Type type, string headerPath) {
            ArgumentNullException.ThrowIfNull(type);
            if (type.IsGenericTypeDefinition || type.ContainsGenericParameters) return true;
            bool hasGenericMethod = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                BindingFlags.Instance | BindingFlags.DeclaredOnly).Any(method => method.IsGenericMethodDefinition);
            if (!hasGenericMethod) return false;
            if (!File.Exists(headerPath)) throw new FileNotFoundException("A method-template owner has no official generated header.", headerPath);
            return File.ReadAllText(headerPath).Contains("HELENGINE_CODE_MODULE_ID", StringComparison.Ordinal);
        }
    }
}
