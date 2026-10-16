### Tarefa 01 — Contrato, catálogo e prova do SDK

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor.automation\helengine.editor.automation.csproj`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp.tests\helengine.editor.mcp.tests.csproj`.

**Criar tipos:** `C:\dev\helworks\helengine\engine\helengine.editor.automation\EditorOperationRequest.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.automation\EditorOperationResult.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.automation\EditorInstanceDescriptor.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.automation\EditorBindingDescriptor.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.automation\EditorToolDefinition.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.automation\EditorJobSnapshot.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.automation\EditorToolCatalog.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.automation\EditorAutomationLimits.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.automation\editor-tools.json`.

**Testar:** `C:\dev\helworks\helengine\engine\helengine.editor.mcp.tests\EditorToolCatalogTests.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp.tests\SdkCompatibilityTests.cs`.

**Interfaces:** produz os tipos da seção 2 e `IReadOnlyList<EditorToolDefinition> EditorToolCatalog.GetTools()`. O JSON embarcado contém as ferramentas e schemas da matriz, sem marcar operações como implementadas.

- [ ] Escrever `Catalog_requires_binding_for_editor_tools`, `Schemas_reject_unknown_and_wrong_typed_fields` e `Sdk_can_exchange_tool_calls_over_stdio_and_http_on_net9`; asserções: `Assert.Empty(missingBindings); Assert.False(invalidPayloadAccepted); Assert.Equal("pong", result)`.
- [ ] Executar vermelho: suíte `$Mcp`, filtro `FullyQualifiedName~EditorToolCatalogTests|FullyQualifiedName~SdkCompatibilityTests`.
- [ ] Implementar DTOs, catálogo validado e host de teste mínimo com SDK 2.2.0; provar HTTP stateless, stdio e saída JSON sem logs misturados. Registrar versões negociadas e dependências resolvidas no relatório de teste. Falha de compatibilidade que demande outra arquitetura volta a Helena.
- [ ] Executar verde com o mesmo filtro e revisar publicação framework-dependent em .NET 9.
- [ ] Commit: `feat: define editor automation contract and validate MCP SDK`.



## Decisão adicional de Helena
Usar biblioteca de JSON Schema como dependência, com versão fixa compatível com net9, para validar argumentos recebidos em runtime. Não implementar validador próprio. O SDK anuncia schemas mas não faz enforcement. Testar rejeição de propriedades desconhecidas e tipos inválidos pela biblioteca real. API compartilhada de validação pode ser adicionada para consumo posterior pelos transportes; o projeto automation continua sem dependências de motor/UI/SDK MCP. Escolha registrada em commit 16f50ecb.
