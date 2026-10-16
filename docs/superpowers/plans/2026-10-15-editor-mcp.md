# Helengine Editor MCP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expor o editor inteiro por MCP HTTP local, com portas automáticas e um conector stdio configurado uma única vez que vincula cada conversa ao editor escolhido.

**Architecture:** Cada editor mantém seu servidor e sua identidade. O conector descobre instâncias e encaminha ferramentas com `binding_id` explícito, sem destino global. Operações MCP e UI compartilham serviços de domínio; snapshots, revisões, histórico e jobs preservam o estado vivo.

**Tech Stack:** C#, SDK .NET `9.0.308`, alvos `net9.0`/`net9.0-windows` existentes, System.Text.Json, xUnit `2.9.3`, Microsoft.NET.Test.Sdk `17.14.1`. Proposta para revisão: MCP C# SDK `2.2.0`, Kestrel embutido e HTTP stateless; validar na tarefa 01 antes de expandir a implementação.

**Spec:** [C:\dev\helworks\helengine\docs\superpowers\specs\2026-10-15-editor-mcp-design.md](C:/dev/helworks/helengine/docs/superpowers/specs/2026-10-15-editor-mcp-design.md), aprovada em 2026-10-15.

Status deste plano: execução autorizada, em andamento. Helena definiu no máximo dois subagentes, ambos `gpt-6-luna` com esforço `xhigh`. As tarefas só são consideradas concluídas após testes e revisão.

Decisão adicional aprovada por Helena em 2026-10-15: usar uma biblioteca de JSON Schema como dependência para validar os argumentos recebidos em runtime. O SDK MCP anuncia schemas, mas isso não substitui a validação de entrada. Fixar uma versão compatível com .NET 9 e testar a rejeição de propriedades desconhecidas e tipos inválidos. O projeto compartilhado permanece independente do motor, da UI e do SDK MCP.

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
2. Inicializações concorrentes do coordenador, listener ocupado por processo alheio, resposta health/protocol inválida, lease expirada e recuperação após crash: não matar processo externo, perder editor vivo ou repetir mutações. Testes nas tarefas 02 e 07–09.
3. Alteração pela UI, undo/redo ou hot reload entre leitura e escrita: invalidar revisão/handle/esquema antes de qualquer efeito. Testes nas tarefas 05 e 12.
4. Resposta perdida após commit, cancelamento tardio e fechamento com requisições em fila: não duplicar efeito, prometer rollback inexistente ou bloquear indefinidamente. Testes nas tarefas 04, 06, 17 e 23.
5. Play salva implicitamente hoje e builds consomem arquivos: distinguir estado vivo de fonte salva sem alterar silenciosamente a semântica da interface. Testes nas tarefas 10, 19 e 20.

## 1. Estrutura proposta e dependências

Os diretórios novos abaixo são propostas. Arquivos marcados “modificar” nas tarefas foram localizados no repositório; os demais serão criados na execução.

| Unidade | Caminho absoluto | Responsabilidade |
| --- | --- | --- |
| Compartilhado novo | `C:\dev\helworks\helengine\engine\helengine.editor.automation` | DTOs, catálogo, schemas e contratos/clientes de descoberta sem referência ao motor, UI ou SDK MCP |
| Domínio no editor existente | `C:\dev\helworks\helengine\engine\helengine.editor\automation` | Despacho, revisão, bindings, ledger, jobs e adaptadores dos serviços de domínio |
| Host HTTP novo | `C:\dev\helworks\helengine\engine\helengine.editor.mcp` | MCP C# SDK, Kestrel, autenticação, lifecycle e adaptação de resultados |
| Conector novo | `C:\dev\helworks\helengine\engine\helengine.editor.mcp.connector` | Executável stdio, descoberta, conexões HTTP e roteamento por vínculo |
| Coordenador novo | `C:\dev\helworks\helengine\engine\helengine.editor.mcp.coordinator` | Processo local independente, endpoint loopback de descoberta e registros vivos em memória |
| Testes de transporte novos | `C:\dev\helworks\helengine\engine\helengine.editor.mcp.tests` | Compatibilidade SDK, registro, HTTP, stdio, isolamento e distribuição |
| Testes de domínio existentes | `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation` | Serviços reais e fixtures determinísticas de editor |
| Composição existente | `C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\MainForm.cs` | Iniciar/parar host MCP junto à sessão, sem regras de domínio |

Direção de referências: editor → automation; host MCP → editor + automation; conector → automation + SDK MCP; coordenador → contratos compartilhados. O conector não referencia editor, renderizadores ou app. O app inicia/garante o coordenador e referencia host MCP. O coordenador serve descoberta, enquanto o conector encaminha chamadas diretamente aos hosts dos editores, sem proxy MCP. O SDK usa seu contêiner de hospedagem apenas dentro dos adaptadores de transporte; não reestruturar a composição do motor.

Criar os cinco projetos novos como `net9.0`, com nullable desabilitado. O MCP deve acompanhar o editor no Windows, Linux e macOS. A máquina de desenvolvimento Windows não restringe o produto a esse sistema. Descoberta, credenciais e vínculo precisam preservar seus contratos nas três plataformas. A autenticação/bootstrap do coordenador e a privacidade das credenciais continuam pendentes; não assumir divulgação pública de segredos nem reduzir isolamento por usuário. Não estabelecer requisito de Windows 10 1709+ por causa de uma API escolhida durante a implementação. Testes executados somente no Windows não comprovam suporte aos outros sistemas.

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

## 4. Tarefas

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

### Tarefa 02 — Coordenador local em memória e lifecycle

**Substitui a implementação file-based anterior da tarefa 02, que está obsoleta e requer substituição. A tarefa 01 permanece válida.**

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor.automation\discovery\EditorInstanceRegistry.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp.tests\EditorInstanceRegistryTests.cs`.

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor.automation\discovery\EditorCoordinatorClient.cs`; projeto `C:\dev\helworks\helengine\engine\helengine.editor.mcp.coordinator\helengine.editor.mcp.coordinator.csproj`, com `C:\dev\helworks\helengine\engine\helengine.editor.mcp.coordinator\Program.cs`, `C:\dev\helworks\helengine\engine\helengine.editor.mcp.coordinator\CoordinatorServer.cs` e `C:\dev\helworks\helengine\engine\helengine.editor.mcp.coordinator\InMemoryEditorRegistry.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp.tests\EditorCoordinatorLifecycleTests.cs`.

**Remover na execução desta tarefa:** `C:\dev\helworks\helengine\engine\helengine.editor.automation\discovery\EditorLocalCredentialStore.cs` e `C:\dev\helworks\helengine\engine\helengine.editor.automation\discovery\EditorRegistryPaths.cs`, junto das implementações Windows de arquivo/ACL e testes exclusivos delas. Credenciais privadas continuam requisito; a remoção desses arquivos não autoriza expor segredos nem reduzir isolamento.

**Interfaces:** publicação e consulta de descriptors vivos, unregister, heartbeat/lease, health/protocol check e start/ensure/stop do processo. O coordenador usa endpoint loopback estável configurável uma vez, sem fixar aqui valor numérico. Não armazena registro persistente nem encaminha chamadas de ferramenta.

- [ ] Escrever testes para descoberta de duas instâncias, unregister do primeiro mantendo o coordenador para a segunda, encerramento após a última, lease expirada após crash do editor, inicialização concorrente com posse exclusiva, listener estrangeiro preservado em caso de colisão e recuperação/re-registro após crash do coordenador.
- [ ] Cobrir deterministicamente um novo registro disputando com unregister/expiração da última instância: registro aceito impede encerramento por lista vazia; encerramento já iniciado recusa registro e permite retry no próximo coordenador. Cobrir também o intervalo entre iniciar o processo e receber o primeiro registro, incluindo queda do editor iniciador.
- [ ] Vermelho: `$Mcp`, filtros `FullyQualifiedName~EditorInstanceRegistryTests|FullyQualifiedName~EditorCoordinatorLifecycleTests`.
- [ ] Implementar processo .NET 9 independente, registros apenas em memória, health/protocol check e retries limitados no startup/shutdown. Editores sobreviventes detectam recuperação e registram novamente. Nunca matar processo que ocupe a porta nem repetir mutações na reconexão.
- [ ] Verde com os mesmos filtros; teste a fixture real do processo/HTTP e execução concorrente, sem substituir prova de transporte por mocks.
- [ ] **Bloqueado para despacho:** não implementar o transporte de registro/descoberta nem fluxo de credenciais até Helena decidir autenticação/bootstrap e isolamento. Não adicionar endpoint público de registro ou divulgação de segredo. Credenciais de editor permanecem privadas.
- [ ] Commit: `feat: track live editor instances with local coordinator`.

### Tarefa 03 — Bind explícito no servidor

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorBindingService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorBindingServiceTests.cs`. **Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\helengine.editor.csproj` para referenciar automation.

**Interfaces:** `EditorBindingDescriptor Bind(string expectedInstanceId, string expectedProjectGeneration)`, `EditorBindingDescriptor Get(string bindingId)`, `void Unbind(string bindingId)`, `void InvalidateProject(string newProjectGeneration)`, `void Close()`.

- [ ] Escrever `Second_bind_does_not_redirect_first`, `Project_change_invalidates_all_old_bindings`, `Reused_port_is_not_identity`, `Binding_limit_is_explicit`; `Assert.NotEqual(a.BindingId, b.BindingId)` e `Assert.Equal("binding_invalid", result.ErrorCode)` após invalidação.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorBindingServiceTests`.
- [ ] Implementar armazenamento por ID opaco aleatório, validação de instância/geração e limite de 64; não inferir destino por cwd, nome ou foco.
- [ ] Verde com o mesmo filtro.
- [ ] Commit: `feat: bind automation calls to explicit editor instances`.

### Tarefa 04 — Despacho na thread do frame e encerramento da fila

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\IEditorOperationDispatcher.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\automation\IEditorOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorOperationRegistry.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorOperationDispatcher.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorOperationDispatcherTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`. **Reutilizar:** `C:\dev\helworks\helengine\engine\helengine.editor\managers\play\EditorMainThreadActionQueue.cs`.

**Interfaces:** produz os contratos de despacho da seção 2 e `void EditorOperationDispatcher.BeginShutdown()`. `TaskCompletionSource` usa continuações assíncronas; o handler não escapa com exceção para o frame.

- [ ] Escrever `Calls_run_on_frame_thread`, `Throwing_call_does_not_block_next`, `Cancellation_before_start_has_no_effect`, `Shutdown_completes_pending_calls`; `Assert.Equal(frameThreadId, handlerThreadId); Assert.True(next.Succeeded); Assert.True(allPendingCompleted)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorOperationDispatcherTests`.
- [ ] Enfileirar execução e snapshots destacados, limitar pendências, cancelar chamadas ainda não iniciadas e completar todas no shutdown. Nunca aguardar HTTP bloqueando a thread do frame.
- [ ] Verde com o mesmo filtro e testes existentes de frame após Dispose.
- [ ] Commit: `feat: dispatch editor automation safely through frame lifecycle`.

### Tarefa 05 — Revisões, gerações e histórico compartilhado

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorRevisionTracker.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorHistoryOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorRevisionTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\history\EditorUndoRedoService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\history\IEditorUndoRedoService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\EditorSceneMutationService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\testing\RecordingUndoRedoService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\hosting\EditorSceneWorkspaceCoordinatorTests.cs` para atualizar as duas implementações de teste da interface.

**Interfaces:** `string EditorRevisionTracker.Current`, `string Advance()`, `string ReplaceScene()`; adicionar `string Cursor` e descrição da operação disponível ao histórico, emitindo mudança para Record/Undo/Redo/Reset. Handler produz `history_get`, `history_undo`, `history_redo`.

- [ ] Escrever `Ui_edit_invalidates_mcp_revision`, `Undo_redo_advances_revision`, `Reload_rejects_reused_entity_id`, `Undo_rejects_changed_cursor`; asserções `Assert.NotEqual(before, after); Assert.Equal("revision_conflict", result.ErrorCode); Assert.Equal(userValue, valueAfterRejectedWrite)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorRevisionTests`.
- [ ] Validar revisão na mesma execução serializada da mutação. Integrar notificações existentes; as tarefas de domínio acrescentam avanço nos caminhos de UI ainda não cobertos. Manter IsAtSavedState separado de revisão de automação.
- [ ] Verde com o mesmo filtro e `FullyQualifiedName~EditorUndoRedoServiceTests|FullyQualifiedName~EditorSessionUndoRedoIntegrationTests`.
- [ ] Commit: `feat: reject stale editor operations and expose shared history`.

### Tarefa 06 — Identidade de operação e resposta perdida

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorOperationLedger.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorOperationLedgerTests.cs`. **Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorOperationDispatcher.cs`.

**Interfaces:** `Task<EditorOperationResult> ExecuteOnceAsync(EditorOperationRequest request, Func<Task<EditorOperationResult>> execute)`, `EditorOperationResult Get(string bindingId, string operationId)`, `void ReleaseBinding(string bindingId)`; expor ferramenta `operation_get`. Unbind impede chamadas novas; trabalho já iniciado conserva seu registro até terminar, sem ser cancelado ou descartado por liberação prematura.

- [ ] Escrever `Duplicate_inflight_requests_share_one_execution`, `Lost_response_can_be_queried`, `Changed_payload_is_rejected`, `Capacity_does_not_evict_old_ids`; `Assert.Equal(1, effects); Assert.Equal(first.OperationId, second.OperationId); Assert.Equal("operation_id_conflict", changed.ErrorCode)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorOperationLedgerTests`.
- [ ] Canonicalizar JSON por nomes de propriedades, preservando ordem de arrays; identidade inclui nome da ferramenta, alvo, geração, revisão e payload. Deduplicar antes da rechecagem de revisão para devolver resultado de chamada já aplicada. Consultas não consomem ledger. Limite de 10.000 IDs por vínculo; desconhecido após crash não vira autorização de replay.
- [ ] Verde com o mesmo filtro.
- [ ] Commit: `feat: track editor mutation outcomes without duplicate effects`.

### Tarefa 07 — Servidor MCP HTTP embutido

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor.mcp\helengine.editor.mcp.csproj`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp\EditorMcpServer.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp\EditorMcpAccessPolicy.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp\EditorMcpToolAdapter.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp.tests\EditorMcpServerTests.cs`.

**Interfaces:** `Task<EditorInstanceDescriptor> StartAsync(CancellationToken cancellationToken)`, `Task StopAsync(CancellationToken cancellationToken)`; construtor recebe dispatcher, bindings, catálogo e política de acesso. Adapter traduz schemas/resultado para SDK sem expor objetos vivos. O host é responsável somente pelo endpoint HTTP; registro, lease e unregister pertencem exclusivamente ao `EditorMcpLifetime` da tarefa 09.

- [ ] Escrever `Binds_only_ipv4_loopback_on_automatic_port`, `Rejects_missing_credentials_and_invalid_origin`, `Authenticated_direct_http_obeys_bindings`, `Start_returns_only_when_endpoint_is_ready`; `Assert.Equal("127.0.0.1", endpoint.Host); Assert.NotEqual(0, endpoint.Port); Assert.Equal(401, unauthorizedStatus); Assert.Equal(403, invalidOriginStatus)`.
- [ ] Vermelho: `$Mcp`, `FullyQualifiedName~EditorMcpServerTests`.
- [ ] Implementar `/mcp`, autenticação em todas as chamadas, Host loopback, Origin ausente permitido para clientes nativos e Origin presente validado contra origem local exata. Sem CORS permissivo nem redirects. Retornar o descriptor com endereço efetivo somente depois de o endpoint estar pronto, para registro pelo lifetime na tarefa 09. O bootstrap de autenticação continua pendente, conforme a tarefa 02.
- [ ] Verde com o mesmo filtro e prova SDK da tarefa 01.
- [ ] Commit: `feat: host authenticated MCP HTTP inside each editor`.

### Tarefa 08 — Conector stdio sem destino global

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor.mcp.connector\helengine.editor.mcp.connector.csproj`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp.connector\Program.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp.connector\EditorMcpConnector.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp.connector\EditorEndpointVerifier.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp.tests\EditorMcpConnectorTests.cs`.

**Interfaces:** `Task<EditorOperationResult> InvokeAsync(EditorOperationRequest request, CancellationToken cancellationToken)`; `list_editors` consulta coordenador e verifica candidatos; `bind_editor` valida identidade/versão no endpoint e guarda rota pelo binding retornado. Catálogo continua carregado sem editores. Após descoberta, todas as chamadas seguem diretamente ao editor selecionado, nunca via coordenador.

- [ ] Escrever `Two_bindings_share_connection_without_cross_routing`, `Editor_opened_later_is_discoverable`, `Recycled_port_fails_identity_check`, `Restart_requires_rebind_not_configuration_change`, `Stdout_contains_only_protocol`; `Assert.Equal(instanceA, responseA.InstanceId); Assert.Equal(instanceB, responseB.InstanceId); Assert.Empty(nonProtocolStdout)`.
- [ ] Vermelho: `$Mcp`, `FullyQualifiedName~EditorMcpConnectorTests`.
- [ ] Implementar cliente HTTP do SDK e roteamento explícito. Verificar identidade/versão no endpoint do editor e comparar a geração novamente com o resultado do bind; se mudou, liberar o vínculo recém-criado e retornar conflito. Aquisição e uso da credencial dependem da decisão pendente na tarefa 02; não buscar segredo em respostas públicas de descoberta. Impedir redirects e proxy para loopback; não procurar endereço em texto de projeto. Reconectar apenas mesma identidade e nunca repetir mutação com ID novo automaticamente. Logs em stderr.
- [ ] Verde com o mesmo filtro usando coordenador e dois hosts HTTP reais; comprovar que o coordenador só descobre e que a ferramenta chega diretamente ao host vinculado.
- [ ] Commit: `feat: bridge Codex and Claude stdio connections to bound editors`.

### Tarefa 09 — Composição no app, status e shutdown

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorAutomationSession.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp\EditorMcpLifetime.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\EditorMcpConnectionsDialog.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp.tests\EditorMcpLifetimeTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\helengine.editor.app.csproj`; `C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\MainForm.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\EditorTitleBar.cs`.

**Interfaces:** `EditorSession.Automation` fornece objeto explícito da sessão; `Task EditorMcpLifetime.StartAsync(CancellationToken token)`, `Task StopAsync(CancellationToken token)`. `EditorAutomationSession` implementa `IEditorOperationHandler.Execute` para `editor_get_state`, `editor_get_capabilities` e `editor_close`, delegando o fechamento ao fluxo existente e à política da tarefa 10. O lifetime garante/starta coordenador, inicia host, registra e mantém lease; no fechamento faz unregister antes do host e deixa o coordenador encerrar somente após a última instância. Status alimenta diálogo por snapshot, sem atualizar controles pela thread HTTP.

- [ ] Escrever `Coordinator_failure_is_visible`, `Shutdown_unregisters_only_this_editor`, `Last_editor_stops_coordinator`, `Shutdown_rejects_calls_before_dispose`, `Unbind_does_not_close_editor`, `Command_line_build_does_not_start_mcp`; validar que outro editor mantém o coordenador vivo.
- [ ] Vermelho: `$Mcp`, `FullyQualifiedName~EditorMcpLifetimeTests`.
- [ ] Compor coordenador/host após sessão pronta; mostrar endereço, projeto, estado, erro e vínculos. Encerrar aceitação, unregister desta instância, completar chamadas, parar host e liberar recursos. Falha ao contatar coordenador é visível e recuperável; não alterar modos CLI nem encerrar o editor automaticamente. Preservar o guard de cena suja.
- [ ] Verde com o mesmo filtro e testes existentes de construção/dispose da sessão.
- [ ] Commit: `feat: integrate MCP startup status and shutdown with editor lifetime`.

### Tarefa 10 — Cenas, salvamento e exportação

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorSceneOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\scene\EditorSceneDocumentOperations.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorSceneOperationTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\scene\EditorSceneLifecycleService.cs`. Reutilizar SceneSaveService, SceneFileLoadService e EditorSceneWorkspaceCoordinator existentes.

**Interfaces:** handler `Execute` da seção 2; serviço compartilhado `void Save(string absolutePath)`, `void Open(string absolutePath, string unsavedPolicy, string savePath)`, `void Create(string unsavedPolicy, string savePath)`, `void Close(string unsavedPolicy, string savePath)`, `void Reload(string unsavedPolicy, string savePath)`, `void ApplySettings(JsonElement patch)`, `void Export(string absolutePath)`. Políticas aceitas: `fail`, `save`, `discard`; `savePath` pode ser vazio somente quando não é preciso salvar uma cena sem destino. Operações e payloads na matriz.

- [ ] Escrever `Dirty_scene_requires_explicit_transition_policy`, `Mcp_edit_does_not_save_until_requested`, `Reload_changes_handle`, `Export_uses_existing_scene_export_path`; comparar bytes de disco antes/depois, `Assert.Equal("unsaved_changes", result.ErrorCode); Assert.NotEqual(oldHandle, newHandle)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorSceneOperationTests`.
- [ ] Extrair os caminhos dos handlers de save/open/export sem criar diálogo na chamada MCP. Salvar cena nova exige caminho; salvar/falhar mantém dirty correto. Substituir callbacks da UI pelos mesmos serviços e avançar revisão.
- [ ] Verde com o mesmo filtro e testes existentes de scene open/save/startup.
- [ ] Commit: `feat: expose scene document operations with explicit save policy`.

### Tarefa 11 — Hierarquia e criação de entidades

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorEntityOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\scene\EditorEntityAuthoringOperations.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorEntityOperationTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\SceneHierarchyPanel.cs`. Reutilizar EditorSceneEntityCreationService, EditorEntityReparentService, BlueprintEditorReadOnlyService e histórico existentes.

**Interfaces:** `EditorEntity Create(string kind, string name, uint parentId)`, `EditorEntity InstantiateAsset(string assetId, uint parentId)`, `void Delete(uint entityId)`, `EditorEntity Duplicate(uint entityId)`, `void Reparent(uint entityId, uint parentId)`, `void Apply(uint entityId, JsonElement scope, JsonElement patch)`; criação aceita tipos catalogados, incluindo Empty/Cube/Plane/Camera e quatro luzes. O adaptador converte somente a raiz explicitamente validada para o sentinela interno `0`.

- [ ] Escrever `Create_types_match_add_menu`, `Explicit_target_ignores_selection`, `Reparent_rejects_cycle`, `Inherited_blueprint_entity_remains_protected`, `Duplicate_and_delete_roundtrip_history`; `Assert.Equal(expectedKinds, actualKinds); Assert.Equal(beforeHierarchy, afterUndo)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorEntityOperationTests`.
- [ ] Expor hierarquia paginada, nomes, ativação, transforms e existência por escopo; validar handles antes de resolver IDs. Integrar atalhos/menu aos serviços, inclusive Add to scene de modelos/blueprints.
- [ ] Verde com o mesmo filtro e testes existentes de reparent/blueprint/read-only.
- [ ] Commit: `feat: automate scene hierarchy through shared authoring operations`.

### Tarefa 12 — Componentes, propriedades e overrides

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorComponentOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\inspection\EditorComponentPropertySchemaService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorComponentOperationTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\managers\inspection\ComponentPropertyEditController.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\ComponentPropertiesView.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorGameScriptHotReloadService.cs`.

**Interfaces:** `JsonElement Describe(string componentTypeId, string schemaGeneration)` e `object DecodeValue(string componentTypeId, string propertyPath, JsonElement value)` produzem somente propriedades editáveis já descritas pelo inspector; handler usa ComponentPropertyEditRequest e escopo explícito.

- [ ] Escrever `Custom_component_schema_is_editable`, `Platform_edit_preserves_common_value`, `Environment_override_is_isolated`, `Readonly_and_stale_schema_are_rejected`, `Nested_arrays_enums_and_references_validate`; `Assert.Equal(commonBefore, commonAfter); Assert.Equal("stale_schema", stale.ErrorCode)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorComponentOperationTests`.
- [ ] Implementar descoberta, add/remove, get/set de propriedade e limpar override. Converter JSON pelo schema; rejeitar NaN, infinito, enum inválido e referência incompatível. Reusar validação/histórico do controller e atualizar geração após hot reload.
- [ ] Verde com o mesmo filtro e testes existentes de propriedades/history/override.
- [ ] Commit: `feat: automate typed component editing and scoped overrides`.

### Tarefa 13 — Biblioteca, arquivos de assets e referências

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorAssetOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\asset\EditorAssetLibraryOperations.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorAssetOperationTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\managers\asset\EditorAssetFileOperationService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\asset\AssetBrowserPanel.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\asset\AssetBrowserView.cs`.

**Interfaces:** handler `Execute`; serviços `void CreateFolder(string absolutePath)`, `void CreateFromTemplate(string templateId, string absolutePath)`, `void Move(string sourcePath, string destinationPath)`, `void Duplicate(string sourcePath, string destinationPath)`, `void Delete(string absolutePath)`. Autoria/referências passam pela sessão existente.

- [ ] Escrever `Move_preserves_identity_and_references`, `Duplicate_gets_distinct_identity`, `Folder_operations_stay_under_project_assets`, `Unicode_spaces_case_and_junctions_are_validated`, `External_changes_advance_revision`; `Assert.Equal(oldId, movedId); Assert.NotEqual(oldId, duplicateId); Assert.False(outsidePathChanged)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorAssetOperationTests`.
- [ ] Consultar biblioteca com filtro/cursor, expor templates, criar/mover/duplicar/excluir com política de dependências explícita. Renomear usa Move. Atualizar índices e autoria; não sobrescrever arquivo externo nem reparar identidade silenciosamente. Retornar relatórios de reparo existentes.
- [ ] Verde com o mesmo filtro e testes existentes de asset file operations/authoring.
- [ ] Commit: `feat: automate asset library operations preserving identities`.

### Tarefa 14 — Editores especializados, blueprints e modificadores

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorAssetDocumentOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\asset\EditorAssetDocumentOperations.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorAssetDocumentOperationTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\MaterialAssetView.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\AnimationClipAssetView.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\scene\MeshComponentModifierStackService.cs`. Reutilizar MaterialAssetSettingsService, AnimationClipPlatformResolutionService e BlueprintSaveService existentes.

**Interfaces:** `JsonElement GetSchema(string assetId, JsonElement scope)`, `JsonElement Read(string assetId, JsonElement scope)`, `void Apply(string assetId, JsonElement scope, JsonElement patch)`. `scope` usa o objeto discriminado da matriz. Modificadores são operações de componente com índice/ID da pilha e ordem explícitos, não edição genérica de bytes.

- [ ] Escrever `Material_and_animation_edits_roundtrip_existing_serializers`, `Asset_scope_does_not_leak`, `Modifier_reorder_updates_preview_and_history`, `Blueprint_authored_edits_preserve_inherited_protection`; `Assert.Equal(expectedFields, reopenedFields); Assert.Equal(beforeCommon, afterCommon)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorAssetDocumentOperationTests`.
- [ ] Extrair commits dos controles para serviços; schemas refletem propriedades já editáveis e suas validações. Incluir salvar/autoria de blueprint e sua instanciação com os serviços existentes. Nenhum editor especializado pode ser omitido por ausência de um formulário genérico.
- [ ] Verde com o mesmo filtro e testes existentes de materiais/animações/blueprint/modifiers.
- [ ] Commit: `feat: expose specialized asset authoring and modifier operations`.

### Tarefa 15 — Projeto, plataformas, ambientes e perfis

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorProjectOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorProjectConfigurationOperations.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorProjectOperationTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\ProjectSettingsDialog.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\PlatformsDialog.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\EnvironmentsDialog.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\ProfilesDialog.cs`.

**Interfaces:** `JsonElement Read(string section)`, `void Apply(string section, JsonElement patch)` para `project`, `scene_routing`, `platforms`, `platform_groups`, `environments`, `profiles`, `runtime_loading`; validação deriva dos documentos e serviços existentes.

- [ ] Escrever `Configuration_updates_use_existing_persistence`, `Disabled_platform_is_rejected`, `Removing_in_use_environment_is_validated`, `Project_generation_change_invalidates_binding`; `Assert.Equal(expectedConfig, reopenedConfig); Assert.Equal("project_changed", result.ErrorCode)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorProjectOperationTests`.
- [ ] Extrair handlers de confirmação e compor os serviços existentes. Declarar persistência imediata quando real. Consultas mostram plataformas instaladas/habilitadas separadamente. Não instalar toolchains nem inventar operação de trocar projeto se o host não a oferece; a invalidação de geração continua obrigatória.
- [ ] Verde com o mesmo filtro e testes existentes de configurações/plataformas/ambientes/perfis.
- [ ] Commit: `feat: expose project configuration with explicit persistence`.

### Tarefa 16 — Seleção, viewport, workspace e preferências

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorWorkspaceOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\workspace\EditorWorkspaceOperations.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorWorkspaceOperationTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\EditorSelectionService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\PreviewPanel.cs`.

**Interfaces:** `JsonElement GetPanel(string panelId)`, `void SetPanelState(string panelId, JsonElement state)`, `void SaveLayout(int slot)`, `void LoadLayout(int slot)`; handler registra seleção, frame/navigation/projection/gizmo/snap, preview e preferências, delegando a serviços existentes.

- [ ] Escrever `Viewport_target_is_explicit_with_multiple_panels`, `Layout_slots_are_1_through_5`, `Preview_lock_survives_selection_change`, `Preferences_persist_without_dirtying_scene`; `Assert.Equal(beforeOtherViewport, afterOtherViewport); Assert.False(sceneBecameDirty)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorWorkspaceOperationTests`.
- [ ] Extrair comandos de painel/layout de EditorSession; preservar múltiplos painéis e IDs por instância. Aplicar framing/navigation por controller, não input simulado. Editar tema/UI scale/snap com seus serviços. Mouse hover e pintura não são comandos persistentes.
- [ ] Verde com o mesmo filtro e testes existentes de workspace, framing, preview e preferences.
- [ ] Commit: `feat: automate editor workspace selection and viewport state`.

### Tarefa 17 — Jobs, cancelamento e logs

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorJobService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorLogBuffer.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorJobOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorJobServiceTests.cs`.

**Interfaces:** `EditorJobSnapshot Start(string kind, Func<CancellationToken, Task<JsonElement>> work)`, `EditorJobSnapshot Get(string jobId)`, `EditorJobSnapshot Cancel(string jobId)`, `JsonElement List(string cursor, int limit)`; logs `void Append(string level, string text, string jobId)` e `JsonElement Read(string cursor, int limit, string level, string jobId)`. Jobs pertencem à instância/projeto e são consultáveis por novo vínculo válido.

- [ ] Escrever `Request_disconnect_does_not_cancel_job`, `Cancellation_requested_is_not_cancelled`, `Concurrent_status_remains_responsive`, `Log_retention_reports_gap`, `Shutdown_observes_worker_failures`; `Assert.Equal("cancel_requested", pending.State); Assert.True(cursorGapReported); Assert.False(secretAppearsInLog)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorJobServiceTests`.
- [ ] Implementar máquina de estados e buffer com sequência/cursor e limite de 10.000 registros. Separar CancellationToken de request e de job. Não usar logger estático como identidade de projeto; filtrar/atribuir origem na captura da sessão. Documentar efeitos já aplicados quando cancelamento não os reverte.
- [ ] Verde com o mesmo filtro.
- [ ] Commit: `feat: track editor jobs and diagnostics independently of requests`.

### Tarefa 18 — Imports, reimport e caches

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorImportOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\asset\EditorImportJobRunner.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorImportOperationTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\managers\asset\AssetImportExecutionService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\AssetImportSettingsView.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`.

**Interfaces:** `EditorJobSnapshot Start(string assetId, JsonElement scope, bool force)`; settings via `JsonElement ReadSettings(string assetId, JsonElement scope)` e `void ApplySettings(string assetId, JsonElement scope, JsonElement patch)`. `scope` usa o objeto discriminado da matriz.

- [ ] Escrever `Import_settings_roundtrip_scopes`, `Reimport_refreshes_loaded_asset`, `Cancel_before_publish_keeps_previous_asset`, `Render_resource_commit_returns_to_frame_thread`; `Assert.Equal(previousBytes, cancelledBytes); Assert.Equal(frameThreadId, commitThreadId)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorImportOperationTests`.
- [ ] Integrar imports com jobs e autoria existente, capturar entradas antes de processamento e publicar recursos na thread correta. Reusar exclusão de escrita do projeto; preservar import anterior quando falhar antes do commit. Mostrar etapa não cancelável explicitamente.
- [ ] Verde com o mesmo filtro e testes existentes de import settings/asset invalidation.
- [ ] Commit: `feat: expose tracked import jobs and scoped import settings`.

### Tarefa 19 — Builds, fila e artefatos

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorBuildOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorBuildJobRunner.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorBuildOperationTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorBuildQueueService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorPlatformBuildExecutor.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\BuildDialog.cs`.

**Interfaces:** `EditorJobSnapshot Start(string queueItemId, string unsavedPolicy)`, `JsonElement GetQueue()`, `void ApplyQueue(JsonElement change)`; resultado de job declara entradas salvas, plataformas, perfis, output absoluto e artefatos.

- [ ] Escrever `Build_does_not_implicitly_save_live_scene`, `Queue_snapshot_is_stable_during_execution`, `Unsupported_cancel_is_not_reported_as_cancelled`, `Play_preparation_excludes_conflicting_build`; `Assert.Equal(beforeDisk, afterRejectedBuild); Assert.Equal("operation_busy", conflicting.ErrorCode)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorBuildOperationTests`.
- [ ] Extrair execução síncrona da fila para coordenação com snapshots/jobs, preservando exclusões atuais de caches. Cobrir adicionar/remover item, copiar settings entre plataformas, resetar cenas para projeto e acompanhar resultados. Manter dependências de frame no frame; cancelar processos filhos apenas por mecanismo suportado.
- [ ] Verde com o mesmo filtro e testes existentes de build queue/config/copy settings; executores de teste, sem compilar todas as plataformas.
- [ ] Commit: `feat: expose responsive build queue jobs and explicit input policy`.

### Tarefa 20 — Play/Stop e fonte salva

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorPlayOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\play\EditorPlayOperations.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorPlayOperationTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\play\EditorPlaySessionService.cs`.

**Interfaces:** `EditorJobSnapshot Start(string platformId, bool waitForDebugger, string unsavedPolicy)`, `void Stop()`, `JsonElement GetState()`, `void ApplySettings(string platformId, JsonElement patch)`; API MCP padrão `fail`, UI conserva a política atual de salvar antes de Play passando `save` explicitamente.

- [ ] Escrever `Dirty_scene_mcp_play_fails_without_save_policy`, `Ui_play_keeps_existing_save_behavior`, `Play_returns_real_preparation_and_launch_outcome`, `Stop_does_not_dispose_editor`; `Assert.Equal("unsaved_changes", result.ErrorCode); Assert.True(uiSaved); Assert.False(editorDisposed)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorPlayOperationTests`.
- [ ] Compartilhar operação de Play sem chamar diretamente o handler de UI que salva. Capturar configurações, fonte salva e revisão usada; respeitar bloqueios durante preparação e capacidades de plataforma. Não inventar pause/step quando indisponíveis.
- [ ] Verde com o mesmo filtro e testes existentes de PlaySession/process/shutdown.
- [ ] Commit: `feat: automate play lifecycle with explicit save policy`.

### Tarefa 21 — Scripts, IDE e comandos de projeto

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorProjectCommandOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorProjectCommandOperationTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorCommandExecutionService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorGameSolutionService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`.

**Interfaces:** handler produz `project_commands_list`, `project_command_execute`, `scripts_build`, `scripts_reload`, `project_open_ide`. Comando usa ID do catálogo e IEditorCommandContext existente; nenhum novo contrato de argumentos livres é presumido para IEditorCommand.Execute.

- [ ] Escrever `Command_executes_in_bound_project_context`, `Build_scripts_reports_job_failure`, `Hot_reload_invalidates_component_schema`, `Unknown_command_is_not_shell_execution`; `Assert.Equal(boundRoot, observedRoot); Assert.Equal("validation_failed", unknown.ErrorCode)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorProjectCommandOperationTests`.
- [ ] Integrar Build Scripts/Open in IDE e extensões registradas. Comandos opacos são declarados mutáveis, sem garantia de undo/cancelamento, e invalidam revisão mesmo se falharem após efeito parcial. Comando síncrono que exige frame não deve ser lançado arbitrariamente numa thread de trabalho; resultado documenta essa restrição.
- [ ] Verde com o mesmo filtro e testes existentes de command execution/game solution/hot reload.
- [ ] Commit: `feat: expose project commands script workflows and IDE launch`.

### Tarefa 22 — Distribuição e configuração única

**Criar:** `C:\dev\helworks\helengine\tools\release\Install-EditorMcpConnector.ps1`; `C:\dev\helworks\helengine\tools\release\portable\Start-HelengineMcp.cmd`; `C:\dev\helworks\helengine\docs\editor-mcp.md`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp.tests\EditorMcpDistributionTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\tools\release\Build-Portable.ps1`; `C:\dev\helworks\helengine\tools\release\Build-Release.ps1`; `C:\dev\helworks\helengine\tools\release\Test-PortableRelease.ps1`.

**Interfaces:** instalador aceita `-InstallRoot` e `-ConnectorSource` absolutos, publica executável/launcher num caminho estável por usuário e imprime configurações de Codex/Claude. Na máquina atual, exemplo de destino: `C:\Users\Helena\AppData\Local\Helengine\mcp`; resolver usuário pela API do sistema, não fixar Helena no código. Testes injetam raiz em `$Artifacts`.

- [ ] Escrever `Packaged_connector_runs_without_global_dotnet`, `Install_path_survives_editor_version_change`, `Spaces_in_install_path_are_quoted`, `Generated_configs_contain_no_port_or_secret`; `Assert.False(configContainsEndpoint); Assert.True(handshakeSucceeded)`.
- [ ] Vermelho: `$Mcp`, `FullyQualifiedName~EditorMcpDistributionTests`.
- [ ] Publicar conector estável e coordenador local independente com os runtimes necessários, sem DLLs de renderização no conector. Atualização preserva launcher/configuração do stdio e não mistura binários de versões. O editor inicia o coordenador sob demanda. Não definir aqui porta numérica nem mecanismo de autenticação ainda pendente. Não sobrescrever arquivos de configuração reais durante testes nem modificar configurações de Helena sem autorização de execução.
- [ ] Verde com o mesmo filtro e teste de pacote mínimo para editor/conector. Não executar release de todas as plataformas como pré-requisito.
- [ ] Commit: `feat: distribute stable local MCP connector and setup instructions`.

### Tarefa 23 — Paridade funcional e validação real dos clientes

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor.mcp.tests\EditorMcpEndToEndTests.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorAutomationCoverageTests.cs`; `C:\dev\helworks\helengine\docs\validation\editor-mcp-client-validation.md`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor.automation\editor-tools.json`; `C:\dev\helworks\helengine\docs\superpowers\plans\2026-10-15-editor-mcp-coverage.md` com evidências e estado de conclusão.

**Interfaces:** catálogo, registry de handlers e matriz devem ter relação verificável por nome. Nenhuma ferramenta anunciada pode faltar no dispatcher sem retornar indisponibilidade de capacidade explícita. Nenhuma operação funcional de UI pode ficar sem ferramenta ou justificativa revisada.

- [ ] Escrever `Every_catalog_tool_has_schema_and_handler`, `Every_functional_menu_action_has_coverage`, `Two_clients_two_editors_survive_rebind`, `Lost_response_does_not_duplicate_mutation`, `Full_authoring_flow_saves_and_reopens`; `Assert.Empty(unmappedTools); Assert.Empty(unmappedActions); Assert.Equal(expectedScene, reopenedScene)`.
- [ ] Vermelho: `$Mcp`, `FullyQualifiedName~EditorMcpEndToEndTests`; `$Editor`, `FullyQualifiedName~EditorAutomationCoverageTests`.
- [ ] Auditar menus, atalhos, inspectors, painéis especializados, toolbar e extensões contra a matriz. Exercitar fluxos reais pelos transportes, com fixture de editor e dois servidores. Novas lacunas são implementadas na tarefa proprietária, não ocultadas por allowlist genérica.
- [ ] Verde com os filtros anteriores e suites dos domínios alterados. No Codex CLI e Claude Code, registrar versão, interface, configuração, protocolo negociado e evidência de bind/read/write/undo/save/rebind com portas diferentes. Testar interfaces desktop/IDE realmente usadas antes de declarar seu suporte. Se faltar acesso ao cliente, registrar limitação e não marcar aceite correspondente.
- [ ] Commit: `test: verify full editor MCP coverage and client binding workflows`.

## 5. Auto-revisão do plano

| Requisito da especificação | Tarefas |
| --- | --- |
| HTTP por editor, loopback, porta automática, credencial, coordenador e descoberta | 01–03, 07–09, 22 |
| Configuração fixa, bind explícito, clientes compartilhando conexão e encaminhamento direto | 03, 08, 22–23 |
| Schema/catálogo, capacidades e paginação | 01, 07–08, 10–16, 23 |
| Estado vivo, frame, erros e shutdown | 04, 09, 17 |
| Revisões, handles, alterações de UI, hot reload | 05, 10–16, 21 |
| Undo/redo e política de salvamento | 05, 10–14, 19–20 |
| Jobs, cancelamento, resultados incertos e deduplicação | 06, 17–21 |
| Matriz de todos os domínios | 10–16, 18–21, 23 |
| Distribuição e validação real | 22–23 |

Os nomes de tipos e interfaces da seção 2 são a fonte única para os handlers. Diferenças entre comportamento atual e novo foram identificadas para Play, filas síncronas, revisão/cursor e comandos opacos. A tarefa 23 não substitui testes funcionais por presença de nomes no catálogo.

Não foram executados restore, compilação, testes de produto, instalação de dependências ou mudanças em configuração de clientes ao escrever este plano. A evidência desta etapa é documental e de leitura de fonte.

## 6. Fontes técnicas e handoff

- [SDK oficial C#: pacotes e responsabilidades](https://github.com/modelcontextprotocol/csharp-sdk).
- [ModelContextProtocol.AspNetCore 2.2.0: alvo net9.0](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore/2.2.0).
- [SDK C# v2: transports e modo stateless](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/transports/transports.html).
- [Codex: configuração MCP](https://learn.chatgpt.com/docs/extend/mcp?surface=cli).
- [Claude Code: configuração MCP](https://code.claude.com/docs/en/mcp).

Fontes consultadas em 2026-10-15. Metadados do pacote indicam compatibilidade de alvo; somente a tarefa 01 comprova execução nesta composição. O índice de código estava desatualizado; caminhos e comportamento citados foram conferidos por leitura de fonte.

Execução por subagentes autorizada por Helena, com revisão por tarefa e limite de dois subagentes, ambos `gpt-6-luna` com esforço `xhigh`. Novas decisões arquiteturais continuam exigindo seu feedback.
