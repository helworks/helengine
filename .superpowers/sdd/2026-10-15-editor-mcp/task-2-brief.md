### Tarefa 02 — Registro local e credenciais por instância

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor.automation\discovery\EditorInstanceRegistry.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.automation\discovery\EditorLocalCredentialStore.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.automation\discovery\EditorRegistryPaths.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp.tests\EditorInstanceRegistryTests.cs`.

**Interfaces:** `void Publish(EditorInstanceDescriptor instance)`, `IReadOnlyList<EditorInstanceDescriptor> ReadCandidates()`, `void Remove(string instanceId)`; credenciais: `string Create(string instanceId)`, `string Read(string instanceId)`, `void Delete(string instanceId)`. Construtores recebem diretório absoluto; registry recebe também `Action<string> reportIssue` para comunicar entradas inválidas sem esconder destinos saudáveis. Produção resolve LocalApplicationData, testes usam `$Artifacts`.

- [ ] Escrever `Publish_is_atomic_and_private`, `Corrupt_entry_does_not_hide_healthy_instances`, `Same_name_and_same_project_keep_distinct_instances`, `Cleanup_cannot_remove_replacement`, `Unicode_and_case_paths_roundtrip`; conferir `Assert.Equal(2, candidates.Count)`, credencial ausente no descriptor e erro diagnóstico para entrada corrompida.
- [ ] Vermelho: `$Mcp`, `FullyQualifiedName~EditorInstanceRegistryTests`.
- [ ] Implementar criação criptográfica de credencial, ACL restrita, escrita atômica por instance ID e remoção condicional. Nunca seguir junction/entrada arbitrária para fora da raiz privada. Candidatos são apenas registros; disponibilidade será verificada por HTTP.
- [ ] Verde com o mesmo filtro, incluindo credencial recusada para usuário não autorizado no teste Windows de ACL.
- [ ] Commit: `feat: discover local editor instances with private credentials`.



## Ambiente de execução já preparado
Worktree: C:\dev\helworks\helengine\.worktrees\editor-mcp. Substituir a raiz original dos caminhos deste brief por esta raiz. SDK9.0.308; submódulos locais inicializados; Windows. Tarefa01 adicionou JsonSchema.Net9.4.1 para avaliação real dos schemas; o compartilhado continua sem motor/UI/SDK MCP. Métodos de validação estão em global-context.md.
Para evitar recompilar o mesmo grafo sem necessidade, pode reutilizar --artifacts-path C:\dev\helworks\builds\helengine\editor-mcp\task01-implementation-20261015-b para estes mesmos projetos/worktree, preservando logs e TRX anteriores. Nomeie logs/fixtures desta tarefa distintamente. Cache NuGet original compartilhado: argumento citado '-p:RestorePackagesPath=C:\dev\helworks\helengine\.nuget\packages'. Rede sandbox pode falhar NU1301/proxy127.0.0.1:9; restore escalado foi aprovado para dependências da tarefa01, não desabilitar assinaturas.

Detalhes alinhados com requisitos aprovados: raiz do produto em LocalApplicationData/Helengine, injetável; metadados JSON e credenciais separados; filename derivado de hash do instanceId, com comparação do ID real do conteúdo. ACL exclusivamente SID usuário atual, herança desativada, diretório e arquivos privados antes da publicação. Não adicionar grantSYSTEM desnecessário. Não introduzir dependências. Layout não deve ser removido por atualização futura do launcher.
