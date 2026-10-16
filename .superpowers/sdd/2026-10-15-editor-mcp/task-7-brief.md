### Tarefa 07 — Servidor MCP HTTP embutido

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor.mcp\helengine.editor.mcp.csproj`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp\EditorMcpServer.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp\EditorMcpAccessPolicy.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp\EditorMcpToolAdapter.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp.tests\EditorMcpServerTests.cs`.

**Interfaces:** `Task<EditorInstanceDescriptor> StartAsync(CancellationToken cancellationToken)`, `Task StopAsync(CancellationToken cancellationToken)`; construtor recebe dispatcher, bindings, catálogo, registro e credential store. Adapter traduz schemas/resultado para SDK sem expor objetos vivos.

- [ ] Escrever `Binds_only_ipv4_loopback_on_automatic_port`, `Rejects_missing_credentials_and_invalid_origin`, `Authenticated_direct_http_obeys_bindings`, `Published_instance_is_already_ready`; `Assert.Equal("127.0.0.1", endpoint.Host); Assert.NotEqual(0, endpoint.Port); Assert.Equal(401, unauthorizedStatus); Assert.Equal(403, invalidOriginStatus)`.
- [ ] Vermelho: `$Mcp`, `FullyQualifiedName~EditorMcpServerTests`.
- [ ] Implementar `/mcp`, autenticação em todas as chamadas, Host `127.0.0.1`, Origin ausente permitido para clientes nativos e Origin presente validado contra origem local exata. Sem CORS permissivo nem redirects. Publicar registro somente depois do start. Endpoint direto lista a própria instância; conector agrega descoberta.
- [ ] Verde com o mesmo filtro e prova SDK da tarefa 01.
- [ ] Commit: `feat: host authenticated MCP HTTP inside each editor`.


