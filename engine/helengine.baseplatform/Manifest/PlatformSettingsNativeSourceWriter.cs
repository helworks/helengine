using System.Text;

namespace helengine.baseplatform.Manifest {
    /// <summary>Emits the shared native catalog factory from the portable settings payload for any platform builder.</summary>
    public static class PlatformSettingsNativeSourceWriter {
        /// <summary>Returns the forward declaration and factory contract consumed by native host initialization.</summary>
        public static string BuildDeclarations() => "class PlatformSettingsConfiguration;\nPlatformSettingsConfiguration* he_create_runtime_platform_settings();\n";
        /// <summary>Emits an owned catalog factory, with an explicitly empty catalog for legacy manifests lacking authored settings.</summary>
        public static string BuildFactory(PlatformSettingsConfiguration configuration, string platformId) {
            PlatformSettingsConfiguration source = configuration ?? new PlatformSettingsConfiguration(platformId, [], [], string.Empty);
            byte[] bytes = PlatformSettingsSerializer.WriteBytes(source);
            StringBuilder builder = new();
            builder.AppendLine("static const std::uint8_t kRuntimeSettingsPayload[] = {" + string.Join(",", bytes) + "};");
            builder.AppendLine("PlatformSettingsConfiguration* he_create_runtime_platform_settings() {");
            builder.AppendLine("    Array<std::uint8_t> bytes(" + bytes.Length + ");");
            builder.AppendLine("    for (int index = 0; index < " + bytes.Length + "; ++index) bytes[index] = kRuntimeSettingsPayload[index];");
            builder.AppendLine("    return PlatformSettingsSerializer::ReadBytes(&bytes);");
            builder.AppendLine("}");
            return builder.ToString();
        }
    }
}
