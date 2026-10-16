### Tarefa 03 — Bind explícito no servidor

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorBindingService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorBindingServiceTests.cs`. **Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\helengine.editor.csproj` para referenciar automation.

**Interfaces:** `EditorBindingDescriptor Bind(string expectedInstanceId, string expectedProjectGeneration)`, `EditorBindingDescriptor Get(string bindingId)`, `void Unbind(string bindingId)`, `void InvalidateProject(string newProjectGeneration)`, `void Close()`.

- [ ] Escrever `Second_bind_does_not_redirect_first`, `Project_change_invalidates_all_old_bindings`, `Reused_port_is_not_identity`, `Binding_limit_is_explicit`; `Assert.NotEqual(a.BindingId, b.BindingId)` e `Assert.Equal("binding_invalid", result.ErrorCode)` após invalidação.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorBindingServiceTests`.
- [ ] Implementar armazenamento por ID opaco aleatório, validação de instância/geração e limite de 64; não inferir destino por cwd, nome ou foco.
- [ ] Verde com o mesmo filtro.
- [ ] Commit: `feat: bind automation calls to explicit editor instances`.



## Contexto de execução
Worktree C:\dev\helworks\helengine\.worktrees\editor-mcp, branch feature/editor-mcp. Substituir raiz original dos paths pela raiz deste worktree. Contrato compartilhado implementado na tarefa01; ver global-context.md para APIs.
Baseline: 25/25 testes construction/dispose passaram em código original. Para aproveitar cache do grafo do editor deste mesmo checkout, usar --artifacts-path C:\dev\helworks\builds\helengine\editor-mcp\baseline-20261015; preservar baseline.log/TRX, usar nome de log e diretório de fixtures específicos da tarefa. Cache NuGet original pode ser reutilizado com argumento quoted '-p:RestorePackagesPath=C:\dev\helworks\helengine\.nuget\packages'. Submódulos locais já inicializados em commits versionados.

Lição da revisão01 para DTOs públicos: snapshots devolvidos não podem permitir que consumidores alterem o estado interno do serviço. EditorInstanceDescriptor/EditorBindingDescriptor têm propriedades públicas; preserve identidade/geração internas mesmo se uma resposta anterior for modificada pelo consumidor. Testar comportamento sem depender de objetos compartilhados.
