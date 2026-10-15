using System.Reflection;
using System.Text.Json;
using helengine.editor.automation;

namespace helengine.editor.mcp.tests;

/// <summary>
/// Checks the shared editor tool catalog's cross-tool contract requirements.
/// </summary>
public sealed class EditorToolCatalogTests
{
    /// <summary>
    /// Verifies every editor operation requires an explicit binding in its input schema.
    /// </summary>
    [Fact]
    public void Catalog_requires_binding_for_editor_tools()
    {
        Type catalogType = GetCatalogType();

        MethodInfo getToolsMethod = catalogType.GetMethod("GetTools", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes);
        Assert.NotNull(getToolsMethod);

        object definitions = getToolsMethod.Invoke(null, null);
        Assert.NotNull(definitions);

        List<string> missingBindings = new();
        foreach (object definition in (IEnumerable<object>)definitions)
        {
            string name = (string)definition.GetType().GetProperty("Name").GetValue(definition);
            if (name == "list_editors" || name == "bind_editor")
            {
                continue;
            }

            JsonElement inputSchema = (JsonElement)definition.GetType().GetProperty("InputSchema").GetValue(definition);
            bool hasBinding = inputSchema.GetProperty("required").EnumerateArray()
                .Any(field => field.GetString() == "binding_id");
            if (!hasBinding)
            {
                missingBindings.Add(name);
            }
        }

        Assert.Empty(missingBindings);
    }

    /// <summary>
    /// Verifies the catalog contains every planned operation and reports required ownership metadata.
    /// </summary>
    [Fact]
    public void Catalog_declares_all_operations_and_metadata_without_claiming_implementation()
    {
        Type catalogType = GetCatalogType();
        MethodInfo getToolsMethod = catalogType.GetMethod("GetTools", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes);
        Assert.NotNull(getToolsMethod);
        IEnumerable<object> definitions = (IEnumerable<object>)getToolsMethod.Invoke(null, null);

        List<object> definitionList = definitions.ToList();
        Assert.Equal(107, definitionList.Count);
        Assert.Equal(107, definitionList.Select(definition => (string)definition.GetType().GetProperty("Name").GetValue(definition)).Distinct(StringComparer.Ordinal).Count());

        foreach (object definition in definitionList)
        {
            Type definitionType = definition.GetType();
            string name = (string)definitionType.GetProperty("Name").GetValue(definition);
            JsonElement inputSchema = (JsonElement)definitionType.GetProperty("InputSchema").GetValue(definition);
            JsonElement outputSchema = (JsonElement)definitionType.GetProperty("OutputSchema").GetValue(definition);
            Assert.False(inputSchema.GetProperty("additionalProperties").GetBoolean());
            Assert.Equal(2, outputSchema.GetProperty("oneOf").GetArrayLength());
            Assert.True((int)definitionType.GetProperty("OwnerTask").GetValue(definition) > 0);
            Assert.False(string.IsNullOrWhiteSpace((string)definitionType.GetProperty("FunctionalSource").GetValue(definition)));
            Assert.False(string.IsNullOrWhiteSpace((string)definitionType.GetProperty("BehaviorTest").GetValue(definition)));
            Assert.Contains((string)definitionType.GetProperty("Persistence").GetValue(definition), new[] { "none", "scene_dirty", "immediate_disk", "job_output", "type_defined", "command_defined" });

            List<string> required = inputSchema.GetProperty("required").EnumerateArray().Select(field => field.GetString()).ToList();
            bool mutates = (bool)definitionType.GetProperty("Mutates").GetValue(definition);
            if (mutates && name != "bind_editor" && name != "unbind_editor")
            {
                Assert.Contains("expected_revision", required);
                Assert.Contains("operation_id", required);
            }

            if (new[] { "scenes_list", "entities_list", "component_types_list", "components_list", "assets_list", "asset_dependencies_get", "jobs_list", "logs_read", "project_commands_list" }.Contains(name, StringComparer.Ordinal))
            {
                JsonElement limit = inputSchema.GetProperty("properties").GetProperty("limit");
                Assert.Equal(1, limit.GetProperty("minimum").GetInt32());
                Assert.Equal(500, limit.GetProperty("maximum").GetInt32());
                Assert.Equal(100, limit.GetProperty("default").GetInt32());
            }
        }

        Assembly automationAssembly = typeof(EditorToolCatalog).Assembly;
        string resourceName = automationAssembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(".editor-tools.json", StringComparison.Ordinal));
        using Stream resource = automationAssembly.GetManifestResourceStream(resourceName);
        using JsonDocument catalog = JsonDocument.Parse(resource);
        foreach (JsonElement definition in catalog.RootElement.GetProperty("tools").EnumerateArray())
        {
            Assert.False(definition.TryGetProperty("implemented", out _));
        }
    }

    /// <summary>
    /// Verifies consumers cannot mutate the shared catalog or desynchronize its input validators.
    /// </summary>
    [Fact]
    public void Catalog_returns_detached_tool_definitions()
    {
        const string ToolName = "list_editors";
        EditorToolDefinition exposedDefinition = Assert.Single(EditorToolCatalog.GetTools(), definition => definition.Name == ToolName);
        string originalName = exposedDefinition.Name;
        JsonElement originalInputSchema = exposedDefinition.InputSchema.Clone();
        JsonElement originalOutputSchema = exposedDefinition.OutputSchema.Clone();
        bool originalMutates = exposedDefinition.Mutates;

        using JsonDocument replacementSchema = JsonDocument.Parse("""{"type":"object","additionalProperties":true}""");
        using JsonDocument unknownArguments = JsonDocument.Parse("""{"consumer_mutation":true}""");

        try
        {
            exposedDefinition.Name = "consumer_mutated_name";
            exposedDefinition.InputSchema = replacementSchema.RootElement.Clone();
            exposedDefinition.OutputSchema = replacementSchema.RootElement.Clone();
            exposedDefinition.Mutates = !originalMutates;

            Assert.False(EditorToolCatalog.TryValidateInputArguments(ToolName, unknownArguments.RootElement, out _));

            EditorToolDefinition catalogDefinition = Assert.Single(EditorToolCatalog.GetTools(), definition => definition.Name == ToolName);
            Assert.Equal(originalInputSchema.GetRawText(), catalogDefinition.InputSchema.GetRawText());
            Assert.Equal(originalOutputSchema.GetRawText(), catalogDefinition.OutputSchema.GetRawText());
            Assert.Equal(originalMutates, catalogDefinition.Mutates);
        }
        finally
        {
            exposedDefinition.Name = originalName;
            exposedDefinition.InputSchema = originalInputSchema;
            exposedDefinition.OutputSchema = originalOutputSchema;
            exposedDefinition.Mutates = originalMutates;
        }
    }

    /// <summary>
    /// Verifies strict schemas reject unknown properties, wrong types, and invalid discriminated objects.
    /// </summary>
    [Fact]
    public void Schemas_reject_unknown_and_wrong_typed_fields()
    {
        bool unknownFieldAccepted = ValidateInput("list_editors", """{"unexpected":true}""");
        bool wrongTypedFieldAccepted = ValidateInput("list_editors", """{"project_root":false}""");
        bool invalidScopeAccepted = ValidateInput("entity_update", """{"binding_id":"b","scene_handle":"s","expected_revision":"1","operation_id":"op","entity_id":"e","scope":{"kind":"environment","platform_id":"desktop"},"patch":{"name":"Renamed"}}""");
        bool emptyReferenceAccepted = ValidateInput("asset_reference_resolve", """{"binding_id":"b","reference":{},"expected_kind":"mesh"}""");
        bool invalidPayloadAccepted = unknownFieldAccepted || wrongTypedFieldAccepted || invalidScopeAccepted || emptyReferenceAccepted;

        Assert.False(invalidPayloadAccepted);
    }

    /// <summary>
    /// Verifies a correctly discriminated environment scope passes the same runtime schema validator.
    /// </summary>
    [Fact]
    public void Schemas_accept_valid_composite_scope()
    {
        bool validPayloadAccepted = ValidateInput("entity_update", """{"binding_id":"b","scene_handle":"s","expected_revision":"1","operation_id":"op","entity_id":"e","scope":{"kind":"environment","platform_id":"desktop","environment_id":"debug"},"patch":{"name":"Renamed"}}""");

        Assert.True(validPayloadAccepted);
    }

    /// <summary>
    /// Verifies the shared runtime API enforces a fetched type-specific patch schema with JsonSchema.Net.
    /// </summary>
    [Fact]
    public void Schemas_validate_dynamic_patch_fields_with_the_json_schema_library()
    {
        using JsonDocument schema = JsonDocument.Parse("""{"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","properties":{"name":{"type":"string"}},"required":["name"],"additionalProperties":false}""");
        using JsonDocument validPatch = JsonDocument.Parse("""{"name":"Renamed"}""");
        using JsonDocument wrongTypePatch = JsonDocument.Parse("""{"name":42}""");
        using JsonDocument unknownFieldPatch = JsonDocument.Parse("""{"name":"Renamed","unknown":true}""");

        bool validPatchAccepted = EditorToolCatalog.TryValidateAgainstSchema(schema.RootElement, validPatch.RootElement, out IReadOnlyList<string> validErrors);
        bool wrongTypeAccepted = EditorToolCatalog.TryValidateAgainstSchema(schema.RootElement, wrongTypePatch.RootElement, out IReadOnlyList<string> wrongTypeErrors);
        bool unknownFieldAccepted = EditorToolCatalog.TryValidateAgainstSchema(schema.RootElement, unknownFieldPatch.RootElement, out IReadOnlyList<string> unknownFieldErrors);

        Assert.True(validPatchAccepted);
        Assert.Empty(validErrors);
        Assert.False(wrongTypeAccepted);
        Assert.NotEmpty(wrongTypeErrors);
        Assert.False(unknownFieldAccepted);
        Assert.NotEmpty(unknownFieldErrors);
    }

    /// <summary>
    /// Verifies the shared defaults match the bounded contract and can be reduced in tests.
    /// </summary>
    [Fact]
    public void Automation_limits_expose_contract_defaults_and_validate_injected_bounds()
    {
        EditorAutomationLimits defaults = new();
        Assert.Equal(100, defaults.DefaultPageSize);
        Assert.Equal(500, defaults.MaximumPageSize);
        Assert.Equal(1024, defaults.MaximumPendingCalls);
        Assert.Equal(64, defaults.MaximumBindings);
        Assert.Equal(10000, defaults.MaximumOperationIdsPerBinding);
        Assert.Equal(4 * 1024 * 1024, defaults.MaximumRequestBytes);
        Assert.Equal(10000, defaults.MaximumRetainedLogEntries);

        EditorAutomationLimits injected = new(3, 8, 10, 2, 20, 1024, 30);
        Assert.Equal(3, injected.DefaultPageSize);
        Assert.Equal(8, injected.MaximumPageSize);
        Assert.Throws<ArgumentOutOfRangeException>(() => new EditorAutomationLimits(9, 8, 10, 2, 20, 1024, 30));
    }

    /// <summary>
    /// Verifies contract DTO JSON field names remain snake_case at the serialization boundary.
    /// </summary>
    [Fact]
    public void Contract_dtos_serialize_with_snake_case_property_names()
    {
        using JsonDocument arguments = JsonDocument.Parse("{}");
        EditorOperationRequest request = new() { Arguments = arguments.RootElement.Clone() };
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(request));
        Assert.True(document.RootElement.TryGetProperty("tool_name", out _));
        Assert.True(document.RootElement.TryGetProperty("binding_id", out _));
        Assert.True(document.RootElement.TryGetProperty("expected_revision", out _));
        Assert.False(document.RootElement.TryGetProperty("ToolName", out _));
    }

    /// <summary>
    /// Gets the shared catalog type without requiring it to exist during the red test phase.
    /// </summary>
    /// <returns>The loaded editor automation catalog type.</returns>
    static Type GetCatalogType()
    {
        Type catalogType = Type.GetType("helengine.editor.automation.EditorToolCatalog, helengine.editor.automation");
        Assert.NotNull(catalogType);
        return catalogType;
    }

    /// <summary>
    /// Runs the catalog's public JSON Schema validation API against a test payload.
    /// </summary>
    /// <param name="ToolName">The name of the tool whose input schema should be evaluated.</param>
    /// <param name="Payload">The JSON payload to validate.</param>
    /// <returns>True when the catalog accepts the payload.</returns>
    static bool ValidateInput(string ToolName, string Payload)
    {
        MethodInfo validateMethod = GetCatalogType().GetMethod("TryValidateInputArguments", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(validateMethod);

        using JsonDocument document = JsonDocument.Parse(Payload);
        object[] arguments = { ToolName, document.RootElement, null };
        return (bool)validateMethod.Invoke(null, arguments);
    }
}
