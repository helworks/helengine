using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Builds reflected component schemas extended with builder-owned synthetic members for one active target platform.
    /// </summary>
    public sealed class PlatformExtendedScriptComponentSchemaBuilder {
        /// <summary>
        /// Shared reflected schema builder used to discover the base persisted component member set.
        /// </summary>
        readonly ScriptComponentReflectionSchemaBuilder ReflectionSchemaBuilder;

        /// <summary>
        /// Initializes one platform-extended reflected schema builder.
        /// </summary>
        public PlatformExtendedScriptComponentSchemaBuilder() {
            ReflectionSchemaBuilder = new ScriptComponentReflectionSchemaBuilder();
        }

        /// <summary>
        /// Builds one reflected component schema augmented with any synthetic members exposed by the supplied platform definition.
        /// </summary>
        /// <param name="componentType">Component type whose schema should be built.</param>
        /// <param name="platformDefinition">Optional platform definition that may expose synthetic members for the component type.</param>
        /// <returns>Deterministic reflected schema that includes any matching synthetic platform members.</returns>
        public ScriptComponentReflectionSchema Build(Type componentType, PlatformDefinition platformDefinition) {
            if (componentType == null) {
                throw new ArgumentNullException(nameof(componentType));
            }

            ScriptComponentReflectionSchema baseSchema = ReflectionSchemaBuilder.Build(componentType);
            if (platformDefinition == null || platformDefinition.ComponentMemberDefinitions.Length < 1) {
                return baseSchema;
            }

            string componentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(componentType);
            List<ScriptComponentReflectionMember> members = new List<ScriptComponentReflectionMember>(baseSchema.Members.Count + platformDefinition.ComponentMemberDefinitions.Length);
            for (int index = 0; index < baseSchema.Members.Count; index++) {
                members.Add(baseSchema.Members[index]);
            }

            PlatformComponentMemberDefinition[] matchingDefinitions = platformDefinition.ComponentMemberDefinitions
                .Where(definition => string.Equals(definition.ComponentTypeId, componentTypeId, StringComparison.Ordinal))
                .OrderBy(definition => definition.Order)
                .ThenBy(definition => definition.MemberName, StringComparer.Ordinal)
                .ToArray();
            for (int index = 0; index < matchingDefinitions.Length; index++) {
                members.Add(CreateSyntheticMember(matchingDefinitions[index]));
            }

            return matchingDefinitions.Length < 1
                ? baseSchema
                : new ScriptComponentReflectionSchema(baseSchema.ComponentType, members);
        }

        /// <summary>
        /// Creates one synthetic reflected member descriptor backed by the component's generic synthetic-member store.
        /// </summary>
        /// <param name="definition">Platform-owned synthetic member definition.</param>
        /// <returns>Synthetic reflected member descriptor.</returns>
        ScriptComponentReflectionMember CreateSyntheticMember(PlatformComponentMemberDefinition definition) {
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            }

            return definition.ValueKind switch {
                PlatformComponentMemberValueKind.String => new ScriptComponentReflectionMember(
                    definition.MemberName,
                    typeof(string),
                    component => component.GetSyntheticStringMemberOrDefault(
                        definition.MemberName,
                        (string)PlatformComponentMemberValueUtility.ParseValue(definition, definition.DefaultValue)),
                    (component, value) => component.SetSyntheticStringMember(definition.MemberName, (string)value),
                    definition),
                PlatformComponentMemberValueKind.Boolean => new ScriptComponentReflectionMember(
                    definition.MemberName,
                    typeof(bool),
                    component => component.GetSyntheticBooleanMemberOrDefault(
                        definition.MemberName,
                        (bool)PlatformComponentMemberValueUtility.ParseValue(definition, definition.DefaultValue)),
                    (component, value) => component.SetSyntheticBooleanMember(definition.MemberName, (bool)value),
                    definition),
                PlatformComponentMemberValueKind.Int32 => new ScriptComponentReflectionMember(
                    definition.MemberName,
                    typeof(int),
                    component => component.GetSyntheticInt32MemberOrDefault(
                        definition.MemberName,
                        (int)PlatformComponentMemberValueUtility.ParseValue(definition, definition.DefaultValue)),
                    (component, value) => component.SetSyntheticInt32Member(definition.MemberName, (int)value),
                    definition),
                PlatformComponentMemberValueKind.Single => new ScriptComponentReflectionMember(
                    definition.MemberName,
                    typeof(float),
                    component => component.GetSyntheticSingleMemberOrDefault(
                        definition.MemberName,
                        (float)PlatformComponentMemberValueUtility.ParseValue(definition, definition.DefaultValue)),
                    (component, value) => component.SetSyntheticSingleMember(definition.MemberName, (float)value),
                    definition),
                _ => throw new InvalidOperationException($"Unsupported platform synthetic member value kind '{definition.ValueKind}'.")
            };
        }
    }
}
