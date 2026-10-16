### Tarefa 08 — Conector stdio sem destino global

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor.mcp.connector\helengine.editor.mcp.connector.csproj`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp.connector\Program.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp.connector\EditorMcpConnector.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp.connector\EditorEndpointVerifier.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp.tests\EditorMcpConnectorTests.cs`.

**Interfaces:** `Task<EditorOperationResult> InvokeAsync(EditorOperationRequest request, CancellationToken cancellationToken)`; ferramenta `list_editors` agrega candidatos verificados; `bind_editor` valida identidade/versão pelo endpoint e guarda rota pelo binding retornado. Catálogo carregado mesmo sem editor aberto.

- [ ] Escrever `Two_bindings_share_connection_without_cross_routing`, `Editor_opened_later_is_discoverable`, `Recycled_port_fails_identity_check`, `Restart_requires_rebind_not_configuration_change`, `Stdout_contains_only_protocol`; `Assert.Equal(instanceA, responseA.InstanceId); Assert.Equal(instanceB, responseB.InstanceId); Assert.Empty(nonProtocolStdout)`.
- [ ] Vermelho: `$Mcp`, `FullyQualifiedName~EditorMcpConnectorTests`.
- [ ] Implementar cliente HTTP do SDK, credenciais fora de prompts e roteamento explícito. Verificar identidade pela descoberta autenticada do endpoint e comparar a geração novamente com o resultado do bind; se mudou, liberar o vínculo recém-criado e retornar conflito. Impedir redirects e proxy para loopback; não procurar endereço em texto de projeto. Reconectar apenas mesma identidade e nunca repetir mutação com ID novo automaticamente. Logs em stderr.
- [ ] Verde com o mesmo filtro usando dois hosts HTTP reais de teste.
- [ ] Commit: `feat: bridge Codex and Claude stdio connections to bound editors`.


