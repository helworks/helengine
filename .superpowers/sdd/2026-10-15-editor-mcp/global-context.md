## Global Constraints

- “Cada instância do editor hospeda seu próprio servidor MCP HTTP.”
- “O acesso é local, em `127.0.0.1`.” Bind na porta `0`; publicar o endereço efetivamente atribuído.
- “Não existe variável global de ‘editor atual’” é a regra operacional: todo acesso a editor exige vínculo explícito.
- “Editar uma cena não a salva automaticamente.” Salvamento e descarte precisam de intenção explícita.
- “O histórico é compartilhado com a interface e com outros clientes; não há uma pilha privada de undo por conversa.”
- “A entrega cobre o editor inteiro.” As tarefas são etapas, não redução de escopo.
- Manter SDK `9.0.308` e os alvos atuais; não atualizar runtime para acomodar dependência sem feedback de Helena.
- Uma classe por arquivo, comentários XML substantivos, campos PascalCase, sem tuplas, funções locais C# ou anotações nullable nos novos projetos. UI contém apresentação e ligação de eventos.
- Código gerado é corrigido no gerador; não editar saídas. Nenhuma execução de código arbitrário substitui ferramentas de domínio.
- Outputs, logs e fixtures ficam em `C:\dev\helworks\builds\helengine\editor-mcp`, em subdiretório exclusivo por execução. Não direcionar saídas ao diretório temporário do usuário.
- Não capturar tela/janelas sem autorização específica. Validar estado e integração por testes e consultas.
- Não reverter alterações de outras tarefas/pessoas. Começar a execução em checkout isolado; este plano não exige isolamento para a edição documental.
- Decisão arquitetural nova ou mudança incompatível exige feedback de Helena, inclusive se a prova de SDK falhar.

## Review Focus

1. Mesmo nome de projeto, duas cópias, duas instâncias e reutilização de PID/porta: confirmar identidade e nunca trocar de destino. Testes nas tarefas 02, 03, 08 e 23.
2. UTF-8, espaços, caminho Windows com casing diferente, junction e arquivo de registro parcialmente escrito: não perder identidade, aceitar entrada inválida ou escapar da raiz. Testes nas tarefas 02 e 13.
3. Alteração pela UI, undo/redo ou hot reload entre leitura e escrita: invalidar revisão/handle/esquema antes de qualquer efeito. Testes nas tarefas 05 e 12.
4. Resposta perdida após commit, cancelamento tardio e fechamento com requisições em fila: não duplicar efeito, prometer rollback inexistente ou bloquear indefinidamente. Testes nas tarefas 04, 06, 17 e 23.
5. Play salva implicitamente hoje e builds consomem arquivos: distinguir estado vivo de fonte salva sem alterar silenciosamente a semântica da interface. Testes nas tarefas 10, 19 e 20.

## 1. Estrutura proposta e dependências

Os diretórios novos abaixo são propostas. Arquivos marcados “modificar” nas tarefas foram localizados no repositório; os demais serão criados na execução.

| Unidade | Caminho absoluto | Responsabilidade |
| --- | --- | --- |
| Compartilhado novo | `C:\dev\helworks\helengine\engine\helengine.editor.automation` | DTOs, catálogo, schemas, descoberta e armazenamento local sem referência ao motor, UI ou SDK MCP |
| Domínio no editor existente | `C:\dev\helworks\helengine\engine\helengine.editor\automation` | Despacho, revisão, bindings, ledger, jobs e adaptadores dos serviços de domínio |
| Host HTTP novo | `C:\dev\helworks\helengine\engine\helengine.editor.mcp` | MCP C# SDK, Kestrel, autenticação, lifecycle e adaptação de resultados |
| Conector novo | `C:\dev\helworks\helengine\engine\helengine.editor.mcp.connector` | Executável stdio, descoberta, conexões HTTP e roteamento por vínculo |
| Testes de transporte novos | `C:\dev\helworks\helengine\engine\helengine.editor.mcp.tests` | Compatibilidade SDK, registro, HTTP, stdio, isolamento e distribuição |
| Testes de domínio existentes | `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation` | Serviços reais e fixtures determinísticas de editor |
| Composição existente | `C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\MainForm.cs` | Iniciar/parar host MCP junto à sessão, sem regras de domínio |

Direção de referências: editor → automation; host MCP → editor + automation; conector → automation + SDK MCP. O conector não referencia editor, renderizadores ou app. O app referencia host MCP. O SDK usa seu contêiner de hospedagem apenas dentro dos adaptadores de transporte; não reestruturar a composição do motor.

Criar os quatro projetos novos como `net9.0`, com nullable desabilitado. A distribuição e os testes nativos iniciais são Windows. A camada de armazenamento local verifica suporte a ACL do Windows explicitamente; não fingir suporte a outro sistema operacional com permissões permissivas.

`ModelContextProtocol.AspNetCore` e `ModelContextProtocol` ficam fixados em `2.2.0` nos respectivos projetos de transporte. O pacote publicado inclui alvo `net9.0`; isso não dispensa provar resolução de dependências e publicação junto ao host. HTTP usa `/mcp`, `HttpServerSessionMode.Stateless` e `HttpTransportMode.StreamableHttp`; bind de aplicação não depende de sessão do transporte. Não habilitar fallback para SSE legado. A tarefa 01 verifica negociação suportada pelo SDK e registra versões reais dos clientes, sem copiar APIs de exemplos do SDK v1.

O portable atual já inclui `Microsoft.AspNetCore.App` `9.0.11`; a tarefa 22 verifica e conserva esse payload. Não assumir instalação global do runtime para iniciar o pacote portable.

Dependências de tarefas:

```text
01 → 02 → 03
01 → 04 → 05 → 06
02 + 03 + 04 + 06 → 07 → 08 → 09
05 + 09 → 10 → 11 → 12 → 13 → 14 → 15 → 16
06 + 09 → 17 → 18 → 19 → 20 → 21
16 + 21 → 22 → 23
```

As duas trilhas de domínio compartilham composição em EditorSession; a execução padrão é sequencial. Paralelismo futuro precisa atribuir ownership para evitar edições concorrentes nessa classe.

## 2. Contrato que todas as tarefas usam

Os nomes abaixo são próprios do Helengine. Cada tipo vai para um arquivo homônimo no projeto compartilhado, listado na tarefa 01.

| Tipo | Campos/contrato |
| --- | --- |
| `EditorOperationRequest` | `string ToolName`, `BindingId`, `SceneHandle`, `ExpectedRevision`, `OperationId`; `JsonElement Arguments`. Strings ausentes em consultas viram vazio, mas valores obrigatórios são validados e rejeitados, nunca fabricados |
| `EditorOperationResult` | `bool Succeeded`; `string ErrorCode`, `Message`, `InstanceId`, `ProjectGeneration`, `Revision`, `OperationId`; `JsonElement Data`. Erro e sucesso têm schemas explícitos |
| `EditorInstanceDescriptor` | `string InstanceId`, `ProjectName`, `ProjectRootPath`, `ProjectGeneration`, `Endpoint`, `ContractVersion`, `EditorVersion`; `int ProcessId`; `DateTimeOffset StartedAt`; sem segredo |
| `EditorBindingDescriptor` | `string BindingId`; `EditorInstanceDescriptor Instance`; `string State`; ID aleatório não derivado da porta |
| `EditorToolDefinition` | `string Name`; `JsonElement InputSchema`, `OutputSchema`; `bool Mutates`, `Undoable`; `string Persistence`, `OwnerTask`, `FunctionalSource`, `BehaviorTest`; sem callback de motor |
| `EditorJobSnapshot` | `string JobId`, `Kind`, `State`, `Message`; `bool CanCancel`; `JsonElement Progress`, `Result`; estados `queued`, `running`, `cancel_requested`, `cancelled`, `succeeded`, `failed` |

Campos JSON usam snake_case; IDs numéricos internos são representados como strings no protocolo. `ContractVersion` inicial é `1`; divergência é rejeitada no bind, independentemente da versão de transporte negociada pelo SDK. Não devolver ponteiros, tipos CLR, instâncias de componentes nem payloads arbitrários de reflexão. `Data` é JSON destacado do objeto vivo.

Interfaces de domínio a criar, cada uma em seu arquivo na tarefa 04:

```csharp
Task<EditorOperationResult> IEditorOperationDispatcher.InvokeAsync(EditorOperationRequest request, CancellationToken cancellationToken);
EditorOperationResult IEditorOperationHandler.Execute(EditorOperationRequest request);
```

O handler roda na thread do frame e retorna imediatamente; trabalho demorado é submetido como job. Toda tarefa de domínio produz um handler registrado pelas ferramentas que possui. `EditorOperationRegistry.Register(string toolName, IEditorOperationHandler handler)` rejeita nomes duplicados. A UI chama os serviços compartilhados abaixo dos handlers e não precisa produzir JSON, credencial ou binding.

Schemas e operações do documento complementar são parte deste contrato: [C:\dev\helworks\helengine\docs\superpowers\plans\2026-10-15-editor-mcp-coverage.md](C:/dev/helworks/helengine/docs/superpowers/plans/2026-10-15-editor-mcp-coverage.md).

Revisão inicial: um contador conservador por geração de projeto, avançado por toda alteração observável de estado editável, inclusive pela UI e histórico. Repaint não avança revisão. Esse desenho aceita conflitos adicionais entre alvos independentes, evitando um sistema prematuro de revisões por objeto. Cada cena carregada recebe handle próprio; esquemas de scripts têm geração própria.

Limites iniciais propostos para revisão: página padrão 100/máximo 500; 1.024 chamadas pendentes; 64 vínculos simultâneos; 10.000 IDs de mutação por vínculo; 4 MiB por corpo de requisição; 10.000 entradas no buffer de logs. Exceder limite retorna erro explícito. Não descartar IDs de operações para abrir espaço. Payloads volumosos são consultados por página, sem truncamento silencioso. Esses limites ficam em `EditorAutomationLimits`, injetáveis nos testes.

## 3. Rotina de teste e checkpoints

Na execução, usar estas variáveis PowerShell, ajustando somente a raiz se estiver num worktree:

```powershell
$Dotnet = 'C:\dev\helworks\helengine\dotnetw.ps1'
$Mcp = 'C:\dev\helworks\helengine\engine\helengine.editor.mcp.tests\helengine.editor.mcp.tests.csproj'
$Editor = 'C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj'
$Artifacts = Join-Path 'C:\dev\helworks\builds\helengine\editor-mcp' ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $Artifacts | Out-Null
```

Cada tarefa fornece suíte e filtro exatos. Rodar o comando abaixo antes/depois da implementação, substituindo `$TestProject` por `$Mcp`/`$Editor` e `$Filter` pelo filtro da tarefa. Usar nome de log diferente no ciclo vermelho e no verde.

```powershell
$TestLog = Join-Path $Artifacts 'task-red-or-green.log'
& $Dotnet test $TestProject --filter $Filter --artifacts-path $Artifacts --logger trx --results-directory $Artifacts -v minimal *> $TestLog
$TestExit = $LASTEXITCODE
$TestTail = (Get-Content -LiteralPath $TestLog -Tail 35 | Out-String)
$TestTail.Substring(0, [Math]::Min(4000, $TestTail.Length))
if ($TestExit -ne 0) { throw "Test command failed; inspect $TestLog" }
```

Vermelho esperado: teste novo falha pelo comportamento ausente ou tipo ainda não implementado; falha de restore/runtime não serve como teste vermelho. Verde esperado: exit code 0, ao menos um teste executado e todas as asserções passando. Filtro sem testes não passa. Não alterar testes existentes para esconder regressão.

Ao final de cada tarefa, revisar `git diff --check`, executar os testes indicados e fazer commit somente dos arquivos daquela tarefa. Mensagem sugerida está em cada tarefa; nunca usar `git add .`. Novos DTOs e fixtures também obedecem à regra de uma classe por arquivo.

Quando o passo verde mencionar regressões existentes, usar também os filtros abaixo com `$TestProject = $Editor`, pelo mesmo comando. Não repetir suites já aprovadas sem alteração ou preocupação nova.

| Tarefa | Filtro de regressão existente |
| --- | --- |
| 04, 09 | `FullyQualifiedName~EditorSessionFrameAfterDisposeTests|FullyQualifiedName~EditorSessionConstructionLedgerTests` |
| 10 | `FullyQualifiedName~EditorSessionSceneOpenTests|FullyQualifiedName~EditorSessionSceneSaveTests|FullyQualifiedName~EditorSessionStartupSceneTests` |
| 11 | `FullyQualifiedName~EditorSessionSceneHierarchyReparentTests|FullyQualifiedName~BlueprintInheritedEntityProtectionTests|FullyQualifiedName~BlueprintEditorReadOnlyTests` |
| 12 | `FullyQualifiedName~EditorComponentHistoryMutationServiceTests|FullyQualifiedName~EditorEntityHistoryMutationServiceTests` |
| 13 | `FullyQualifiedName~EditorAssetFileOperationServiceTests|FullyQualifiedName~EditorProjectAuthoringSessionTests` |
| 14 | `FullyQualifiedName~MaterialAssetViewTests|FullyQualifiedName~AnimationClipEnvironmentResolutionTests|FullyQualifiedName~BlueprintSaveServiceTests` |
| 15 | `FullyQualifiedName~EditorProjectPlatformsServiceTests|FullyQualifiedName~EditorProjectEnvironmentsServiceTests|FullyQualifiedName~EditorProfileSettingsServiceTests` |
| 16 | `FullyQualifiedName~EditorSessionWorkspaceTests|FullyQualifiedName~EditorViewportSelectionFramingServiceTests|FullyQualifiedName~EditorSessionPreferencesTests|FullyQualifiedName~EditorSessionPreviewSelectionTests` |
| 18 | `FullyQualifiedName~EditorSessionAssetImportSettingsTests` |
| 19 | `FullyQualifiedName~EditorBuildQueueServiceTests|FullyQualifiedName~EditorBuildConfigServiceTests|FullyQualifiedName~EditorBuildPlatformConfigCopyServiceTests` |
| 20 | `FullyQualifiedName~EditorPlaySessionServiceTests|FullyQualifiedName~EditorPlaySessionPreparationTests` |
| 21 | `FullyQualifiedName~EditorCommandExecutionServiceTests|FullyQualifiedName~EditorGameSolutionServiceTests|FullyQualifiedName~EditorGameScriptHotReloadServiceTests` |



Decisão de execução: no máximo dois subagentes, ambos gpt-6-luna xhigh. Decisão de Helena: biblioteca JSON Schema adicionada para enforcement de argumentos em runtime; versão fixa compatível net9, sem validador próprio.

Task01 produced shared public APIs: EditorToolCatalog.GetTools(); TryValidateInputArguments(string ToolName, JsonElement Arguments, out IReadOnlyList<string> Errors); TryValidateAgainstSchema(JsonElement SchemaDocument, JsonElement Candidate, out IReadOnlyList<string> Errors). JsonSchema.Net9.4.1 validates runtime. Dynamic patch consumers MUST also validate against current document/type schema before mutation. Pin MCP2.2.0. Task01 remains under independent review.

## Superseding architecture correction — 2026-10-15

Helena requires Windows, Linux and macOS, and explicitly chose a shared local coordinator process: start with first editor, keep an in-memory list of live instances, stop after the last editor closes (crashes require expiry). This supersedes all filesystem registry/ACL/native rename requirements and the no-central-daemon statement above. Preserve each editor's ephemeral MCP HTTP endpoint and explicit binding. The coordinator outlives the editor that launched it; simultaneous starts and shutdown races require coordination. Spec and plan are currently being revised: reread authoritative documents and regenerate affected task briefs before implementing. Task01 remains completed; old task02 code is superseded, not production-ready for this architecture. No Windows 10 1709+ product requirement was approved.
