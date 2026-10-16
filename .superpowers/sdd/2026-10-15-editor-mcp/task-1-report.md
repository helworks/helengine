# Tarefa 01 — Contrato, catálogo e prova do SDK

Status: concluída.

## Implementação

- Criados os projetos `engine/helengine.editor.automation` e `engine/helengine.editor.mcp.tests`, ambos em `net9.0` e com nullable desabilitado. A camada automation não referencia motor, UI nem SDK MCP; contém apenas a dependência aprovada `JsonSchema.Net`.
- Adicionados os seis DTOs do contrato, `EditorToolDefinition`, `EditorToolCatalog` e `EditorAutomationLimits`. Os DTOs usam nomes JSON `snake_case`; o contrato mantém `ContractVersion = 1` e estados de job documentados.
- `editor-tools.json` declara os 107 nomes da matriz com input/output schemas, campos obrigatórios e opcionais, schemas estritos, scopes e parents discriminados, metadados de mutação, persistência, undo, tarefa, origem funcional e teste comportamental. Nenhuma ferramenta declara implementação.
- Validação em runtime usa JsonSchema.Net 9.4.1. As APIs públicas são:

```csharp
public static IReadOnlyList<EditorToolDefinition> GetTools();
public static bool TryValidateInputArguments(string ToolName, JsonElement Arguments, out IReadOnlyList<string> Errors);
public static bool TryValidateAgainstSchema(JsonElement SchemaDocument, JsonElement Candidate, out IReadOnlyList<string> Errors);
```

`TryValidateInputArguments` valida o schema estático da ferramenta. Campos de `patch` variam conforme tipo/documento; os consumidores devem validar o próprio patch com `TryValidateAgainstSchema` e o schema atual retornado pelo grupo correspondente. Ambos executam avaliação da biblioteca, sem validador próprio. A carga do catálogo também compila os 107 schemas de entrada e de saída.

## Evidência RED/GREEN

RED válido, antes do catálogo: o teste SDK de transporte passou e os três testes do catálogo falharam porque `EditorToolCatalog` ainda não existia (3 falhas, 1 aprovação). Isso confirma ausência de comportamento; a falha anterior de rede `NU1301` não foi usada como vermelho. Log: `C:\dev\helworks\builds\helengine\editor-mcp\task01-red-20261015-a\task01-red-after-schema-tests.log`.

GREEN final, exit 0: 8 aprovados, 0 falhas, filtro exato `FullyQualifiedName~EditorToolCatalogTests|FullyQualifiedName~SdkCompatibilityTests`. Os testes verificam 107 ferramentas únicas, binding obrigatório, metadados, schemas strict/paginação/revisão, rejeição de propriedades extras/tipos inválidos/scope incompleto, scope composto válido, patch dinâmico pela biblioteca, limites, nomes JSON e chamada ping por HTTP e stdio reais.

```powershell
$Artifacts = 'C:\dev\helworks\builds\helengine\editor-mcp\task01-implementation-20261015-b'
$Dotnet = 'C:\dev\helworks\helengine\.worktrees\editor-mcp\dotnetw.ps1'
$Project = 'engine\helengine.editor.mcp.tests\helengine.editor.mcp.tests.csproj'
$Filter = 'FullyQualifiedName~EditorToolCatalogTests|FullyQualifiedName~SdkCompatibilityTests'
& $Dotnet test $Project --no-restore --filter $Filter --artifacts-path $Artifacts --logger trx --results-directory $Artifacts -v minimal
```

Log GREEN: `C:\dev\helworks\builds\helengine\editor-mcp\task01-implementation-20261015-b\task01-green-final.log`. O TRX final foi salvo no mesmo diretório de artifacts. O baseline original, executado pelo controlador antes da edição, passou 25/25; não repeti a suíte inteira do editor.

## Transporte, protocolos e runtime

O teste iniciou Kestrel real em `127.0.0.1` com porta 0, mapeou `/mcp` em modo stateless e conectou com o cliente oficial do SDK em Streamable HTTP. O teste stdio iniciou um processo filho real usando `StdioClientTransport`. Em ambos houve `ping`/`pong` completo.

Negociação registrada pelo teste: HTTP e stdio negociaram `2026-07-28`; runtime observado pelo processo de teste e pelo host filho foi `9.0.20`. A linha diagnóstica `STDIO_RUNTIME=9.0.20` foi capturada pelo callback `StandardErrorLines`; o ping bem-sucedido comprova que stdout permaneceu válido para JSON-RPC. As notas oficiais da release SDK 2.2.0 documentam o suporte HTTP híbrido às revisões `2025-11-25` e `2026-07-28`; `2026-07-28` é a revisão publicada na especificação estável, não um protocolo draft.

SDK instalado pelo wrapper: `9.0.308`. Publish final Release executado com `--no-self-contained`; o runtimeconfig publicado registra `tfm=net9.0`, `Microsoft.NETCore.App 9.0.0` e `Microsoft.AspNetCore.App 9.0.0`, comprovando publicação framework-dependent sem trocar o runtime do projeto. Log: `C:\dev\helworks\builds\helengine\editor-mcp\task01-implementation-20261015-b\framework-dependent-publish.log`. Runtimeconfig: `C:\dev\helworks\builds\helengine\editor-mcp\task01-implementation-20261015-b\publish\helengine.editor.mcp.tests\release\helengine.editor.mcp.tests.runtimeconfig.json`.

## Dependências resolvidas e fontes

Versões diretas: `JsonSchema.Net 9.4.1`, `ModelContextProtocol 2.2.0`, `ModelContextProtocol.AspNetCore 2.2.0`, `ModelContextProtocol.Core 2.2.0` (transitiva), `xunit 2.9.3`, `xunit.runner.visualstudio 3.1.4` e `Microsoft.NET.Test.Sdk 17.14.1`. A lista resolvida completa (38 entradas: referência ao projeto automation e 37 pacotes NuGet) está em `C:\dev\helworks\builds\helengine\editor-mcp\task01-implementation-20261015-b\resolved-packages-from-assets.log`, extraída do `project.assets.json` produzido pelo restore. Entre os transitivos estão `JsonPointer.Net 7.0.3`, `Json.More.Net 3.0.1`, `Humanizer.Core 3.0.10`, `Microsoft.Extensions.AI.Abstractions 10.8.3`, `Microsoft.Extensions.* 10.0.10` e `System.Text.Json 10.0.10`; execução e publish acima provaram compatibilidade com o host .NET 9.

Fontes oficiais verificadas:

- [JsonSchema.Net 9.4.1 no NuGet](https://www.nuget.org/packages/JsonSchema.Net/9.4.1) declara compatibilidade com net9.0; a restauração aprovada e os testes executaram essa versão.
- [JsonSchema.Net Basics](https://docs.json-everything.net/schema/basics/) e [API `JsonSchema`](https://docs.json-everything.net/api/JsonSchema.Net/JsonSchema/) documentam o ciclo `JsonSchema.Build`/`Evaluate` e `EvaluationResults.IsValid` usado pelo catálogo.
- [Release oficial do MCP C# SDK v2.2.0](https://github.com/modelcontextprotocol/csharp-sdk/releases/tag/v2.2.0) documenta suporte HTTP às revisões 2025-11-25 e 2026-07-28; [especificação oficial 2026-07-28](https://modelcontextprotocol.io/specification/2026-07-28/basic/index) é a revisão negociada nos testes.
- [Pacote MCP oficial 2.2.0 no NuGet](https://www.nuget.org/packages/ModelContextProtocol/2.2.0) declara compatibilidade com .NET 9 ou superior.

## Self-review

- `git diff --cached --check` passou; o stage contém somente 16 arquivos novos dos dois projetos desta tarefa. A revisão não encontrou `nullable` ativado, modificadores `private` redundantes, funções locais, tuplas ou marcador de ferramenta implementada.
- Schemas de entrada e saída foram compilados pela biblioteca ao carregar o catálogo; os testes reais demonstram rejeição/aceitação das formas exigidas. Mutação de editor exige `expected_revision` e `operation_id`, exceto os atos de binding; páginas enumeradas fixam 1–500 e default 100.
- O catálogo descreve contrato e cobertura futura; não registra handlers nem afirma que os 107 domínios já foram implementados. `BehaviorTest` aponta a fixture planejada por tarefa, não substitui os testes comportamentais das tarefas de domínio.
- Em um GREEN intermediário, a asserção nova de serialização criou um `EditorOperationRequest` com `JsonElement` não inicializado (`Undefined`); corrigi o fixture para fornecer `{}` e rodei novamente o filtro, que passou 7/7. Depois os testes adicionais de patch dinâmico levaram o GREEN final a 8/8.
- `dotnet list package` não consegue localizar os assets fora do `obj` padrão quando o wrapper usa `--artifacts-path`; uma chamada também usou `--no-restore`, que esse subcomando não aceita. Não usei esses resultados como evidência: as versões resolvidas foram lidas diretamente do `project.assets.json` na pasta de artifacts informada acima.
- Limitação intencional: `patch` não pode enumerar um conjunto estático de campos porque o documento/tipo fornece o schema atual. Os consumidores futuros precisam invocar a API dinâmica documentada antes de despachar o patch.

Commit desta tarefa: `7b3f5eb858540aab4820707645af81de3f0e4bfb feat: define editor automation contract and validate MCP SDK`.

## Correção da revisão independente — round 1

`EditorToolCatalog.GetTools()` agora retorna uma coleção somente leitura com novas projeções de cada definição em toda chamada. As definições ainda são objetos públicos editáveis por compatibilidade, mas suas alterações e trocas de `JsonElement` ficam isoladas naquela projeção; a coleção estática e os schemas compilados para validação permanecem intactos. A cópia inclui os dois schemas e todos os metadados.

O teste `Catalog_returns_detached_tool_definitions` altera nome, schemas e metadado em uma definição retornada, então confirma que uma nova consulta mantém os valores originais e que o validador continua rejeitando um campo desconhecido. RED reproduzido antes da correção: 7 passaram e o teste novo falhou porque o catálogo não continha mais `list_editors` após a mutação. Log: `C:\dev\helworks\builds\helengine\editor-mcp\task01-implementation-20261015-b\review-round1-red-catalog.log`.

Também corrigi a observação minor da revisão: a asserção agora examina cada objeto da fonte JSON embarcada e confirma ausência da propriedade `implemented`; não inspeciona mais nomes de propriedades CLR. As duas chamadas a `Assert.Single` usam o overload com predicado, evitando os avisos xUnit2031 que surgiram no teste RED.

GREEN direcionado, exit 0: 8/8 testes `EditorToolCatalogTests`, incluindo snapshot independente e ausência de marcador no JSON. Comando: `dotnetw.ps1 test engine\helengine.editor.mcp.tests\helengine.editor.mcp.tests.csproj --no-restore --filter FullyQualifiedName~EditorToolCatalogTests --artifacts-path C:\dev\helworks\builds\helengine\editor-mcp\task01-implementation-20261015-b --logger trx --results-directory C:\dev\helworks\builds\helengine\editor-mcp\task01-implementation-20261015-b -v minimal`. Log: `C:\dev\helworks\builds\helengine\editor-mcp\task01-implementation-20261015-b\review-round1-green-catalog.log`; o TRX está na mesma pasta de artifacts. Transporte SDK e publish não foram repetidos, pois a correção só toca projeções do catálogo e testes de catálogo.

Self-review: `git diff --check` passou; alteração de código restrita a `EditorToolCatalog.cs` e `EditorToolCatalogTests.cs`. Commit de correção: `cf9bdfdd99879714dd7ab1cae1d1f660a1e92b9b fix: isolate editor tool catalog snapshots`.