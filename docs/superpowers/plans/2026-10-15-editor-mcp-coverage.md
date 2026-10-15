# Matriz de operações — MCP do Helengine Editor

Complemento do plano: [C:\dev\helworks\helengine\docs\superpowers\plans\2026-10-15-editor-mcp.md](C:/dev/helworks/helengine/docs/superpowers/plans/2026-10-15-editor-mcp.md).

Status: catálogo proposto para implementação e revisão. Nenhuma ferramenta desta matriz é declarada implementada. A auditoria final confronta esta matriz com os menus, atalhos, inspectors, toolbar, painéis e comandos reais; encontrar operação adicional exige acrescentá-la, não declarar paridade por contagem de nomes.

## Regras comuns dos schemas

- `B`: `binding_id` obrigatório em todo acesso a editor. `list_editors` e `bind_editor` são as exceções.
- `S`: `scene_handle` obrigatório para operar em cena viva; ID numérico de entidade sozinho nunca é suficiente.
- `W`: `expected_revision` e `operation_id` obrigatórios para mutações. Conferir revisão e executar na mesma passagem pela thread do frame. Consultas e bind/unbind não exigem W.
- `P`: `cursor` opcional, `limit` inteiro de 1 a 500, padrão 100. Resposta: `items`, `next_cursor`, `truncated`, `revision`. Cursor inclui contexto/revisão; cursor de outra consulta é inválido e revisão incompatível exige reiniciar a paginação.
- `scope`: objeto discriminado. `{ "kind": "common" }`; `{ "kind": "platform", "platform_id": "..." }`; ou `{ "kind": "environment", "platform_id": "...", "environment_id": "..." }`. Campos incompatíveis com o kind são rejeitados.
- `parent`: `{ "kind": "root" }` ou `{ "kind": "entity", "entity_id": "..." }`. Criação pode omitir parent e declarar raiz como padrão no schema; reparent exige parent explícito. Limpar override exige scope diferente de common.
- `unsaved_policy`: `fail`, `save` ou `discard`, padrão `fail`, somente quando a operação realmente pode substituir/fechar o documento. Play/build aceitam `fail`/`save`; `discard` não é opção para executá-los.
- `patch`: propriedades declaradas no schema consultado para aquele documento/tipo; rejeitar propriedades extras, tipos inválidos, referências incompatíveis e caminhos não editáveis. Não é JSON Patch de ponteiros arbitrários no runtime.
- Arrays mantêm ordem; valores numéricos precisam ser finitos. Enum usa valores anunciados no schema. Propriedade referência usa ID e tipo esperado, nunca endereço de memória.
- Resultados seguem `EditorOperationResult` do plano. `Data` identifica valores, revisões e efeitos realmente aplicados; jobs retornam `EditorJobSnapshot`. Datas UTC em ISO 8601.
- Paths de origem/destino são absolutos e canônicos. Escrita de assets permanece sob a raiz de assets; export e output de build usam destino absoluto explícito validado pelo serviço correspondente.
- Toda ferramenta tem schema completo e `additionalProperties: false` na fronteira de sua entrada. O SDK anuncia ferramentas separadas; o handler genérico interno não vira um `execute` irrestrito no MCP.

## Operações e responsabilidade

| ID | Ferramentas exatas | Entrada específica além de B/S/W/P | Resultado e semântica | Tarefa |
| --- | --- | --- | --- | --- |
| CON-01 | `list_editors` | Sem B; `project_root` opcional para filtrar | Instâncias verificadas, nome, raiz, geração, endereço e disponibilidade; sem credencial | 02, 08 |
| CON-02 | `bind_editor` | Sem B; `instance_id` | Binding e identidade/geração verificadas. Não altera bindings existentes | 03, 08 |
| CON-03 | `get_binding`, `unbind_editor` | B | Estado ou liberação daquele vínculo; não fecha editor | 03, 08 |
| CON-04 | `editor_get_state`, `editor_get_capabilities` | B | Projeto, cena, revisão, dirty, modos, schemas disponíveis, capacidades por plataforma e restrições | 09 |
| CON-05 | `editor_close` | B+W; `unsaved_policy` | Pedido de fechamento aceito ou erro; não garantir resposta final após o processo encerrar | 09, 10 |
| OP-01 | `operation_get` | B; `operation_id` | Resultado conhecido/em execução/desconhecido; sem replay | 06 |
| SCN-01 | `scenes_list`, `scene_get` | List: B+P; get: B+S | Catálogo de cenas salvas e snapshot da cena viva, inclusive ainda sem path | 10 |
| SCN-02 | `scene_create`, `scene_open`, `scene_reload`, `scene_close` | B+W; open: `absolute_path`; reload/close: S; `unsaved_policy`; `save_path` quando a cena anterior ainda não tem destino | Geração/handle novo ou nenhuma cena; respeita dirty e políticas | 10 |
| SCN-03 | `scene_save`, `scene_save_as` | B+S+W; save_as: `absolute_path`; save exige caminho já conhecido | Path salvo, revisão e dirty atualizado; nada de diálogo invisível | 10 |
| SCN-04 | `scene_settings_get`, `scene_settings_update` | B+S; update: W+`patch` | Configurações de cena; update participa do histórico existente | 10 |
| SCN-05 | `scene_export` | B+S+W; `absolute_path` | Resultado da exportação existente; identifica origem viva/salva efetivamente usada | 10 |
| ENT-01 | `entities_list`, `entity_get` | B+S; list: P+`parent`/`query` opcionais; get: `entity_id` | Lista sem parent percorre a cena; com parent filtra filhos diretos. Hierarquia, transform, ativação, origem blueprint e editabilidade | 11 |
| ENT-02 | `entity_kinds_list` | B+S | Tipos suportados; Empty, Cube, Plane, Camera, Spot/Point/Directional/Ambient Light atuais | 11 |
| ENT-03 | `entity_create`, `entity_instantiate_asset` | B+S+W; create: `kind`, `name`; instantiate: `asset_id`; `parent` opcional significa raiz quando ausente | ID criado e mudanças de seleção declaradas; modelo/blueprint seguem serviço existente | 11 |
| ENT-04 | `entity_duplicate`, `entity_delete`, `entity_reparent` | B+S+W+`entity_id`; reparent: `parent` explícito | IDs resultantes, hierarquia e histórico; rejeitar ciclos e conteúdo herdado protegido | 11 |
| ENT-05 | `entity_update`, `entity_override_clear` | B+S+W+`entity_id`+`scope`; update: `patch`; clear: `property_path` | Nome, ativação, transform e existência conforme schema; comum e overrides isolados | 11 |
| CMP-01 | `component_types_list`, `components_list`, `component_get`, `component_schema_get` | B+S; componentes: `entity_id`; get: `component_id`; schema: `type_id`; listas com P | Tipos built-in/projeto, schema_generation, propriedades editáveis e valores | 12 |
| CMP-02 | `component_add`, `component_remove` | B+S+W+`entity_id`; add: `type_id`; remove: `component_id` | Identidade do componente e histórico; compatibilidade/readonly preservados | 12 |
| CMP-03 | `component_property_set`, `component_override_clear` | B+S+W+`entity_id`+`component_id`+`scope`+`schema_generation`+`property_path`; set: `value` | Valor próprio/efetivo e revisão; rejeita schema obsoleto | 12 |
| AST-01 | `assets_list`, `asset_get`, `asset_dependencies_get` | B; lists: P+`query`/`kind` opcionais; get/deps: `asset_id`; deps: P | IDs, paths, metadados, dependências, estado de import e editabilidade | 13 |
| AST-02 | `asset_templates_list`, `asset_create`, `asset_folder_create` | B; escritas: W+`absolute_path`; asset_create: `template_id` | Arquivo/pasta criada e identidade; templates vêm do catálogo existente | 13 |
| AST-03 | `asset_move`, `asset_duplicate`, `asset_delete` | B+W+`asset_id`; move/duplicate: `destination_path`; delete: `dependency_policy` = `fail_if_referenced` ou `allow_missing_references` explícito | Move preserva identidade; duplicate gera identidade própria; delete informa referências afetadas | 13 |
| AST-04 | `asset_reference_resolve` | B; `reference` com identidade/path/hash disponíveis e `expected_kind` | Referência canônica e relatório de resolução/reparo; não inventar fallback MCP | 13 |
| DOC-01 | `asset_document_get`, `asset_document_schema_get`, `asset_document_update` | B+`asset_id`+`scope`; update: W+`schema_generation`+`patch` | Materiais, animações e demais documentos editáveis; persistência declarada por tipo | 14 |
| DOC-02 | `blueprint_save` | B+S+W; `entity_id`, `absolute_path` | Blueprint por serializer/autoria existentes; preserva proteção de conteúdo herdado | 14 |
| DOC-03 | `mesh_modifiers_get`, `mesh_modifier_add`, `mesh_modifier_update`, `mesh_modifier_remove`, `mesh_modifier_move` | B+S+`entity_id`+`component_id`+`scope`; escritas: W; add: `type_id`; demais: `modifier_id`; update: `patch`; move: `destination_index` | Pilha ordenada, preview atualizado e histórico quando suportado | 14 |
| PRJ-01 | `project_configuration_get`, `project_configuration_schema_get`, `project_configuration_update` | B+`section`; update: W+`patch`; section é um dos sete valores definidos na tarefa 15 | Projeto, roteamento, plataformas/grupos, ambientes, perfis e runtime loading; persistência real explícita | 15 |
| PRJ-02 | `platform_capabilities_get` | B; `platform_id` opcional | Instalado/habilitado, suporte a Play/build e opções efetivas | 15, 20 |
| SEL-01 | `selection_get`, `selection_set` | B+S; set: W+`target_kind` (`entity`, `asset`, `none`) e ID correspondente | Estado de seleção compartilhado; não altera alvos de chamadas em fila | 16 |
| VWP-01 | `viewports_list`, `viewport_get`, `viewport_update`, `viewport_frame` | B; alvo individual: `panel_id`; update: W+`patch`; frame: S+W+`entity_ids` | Câmera/projeção/modos, navegação, gizmo e snap; somente viewport indicado | 16 |
| WSP-01 | `workspace_panels_list`, `workspace_panel_open`, `workspace_panel_update`, `workspace_panel_close` | B; mutações: W; open: `panel_type`; update/close: `panel_id`; update: `patch` | Painéis, posição/dock, visibilidade e IDs estáveis na execução | 16 |
| WSP-02 | `workspace_layout_save`, `workspace_layout_load` | B+W+`slot` inteiro entre 1 e 5 | Slots existentes e restauração de layout; inclui múltiplos painéis | 16 |
| WSP-03 | `preferences_get`, `preferences_update` | B; update: W+`patch` | Tema/UI scale e preferências existentes; sem marcar cena dirty por preferência visual | 16 |
| WSP-04 | `preview_get`, `preview_update` | B+`panel_id`; update: W+`patch` | Fonte/lock e controles disponíveis do preview; não presumir captura de tela | 16 |
| HIS-01 | `history_get`, `history_undo`, `history_redo` | B+S; undo/redo: W+`expected_history_cursor` | Cursor, descrição e disponibilidade; aplica somente próximo item da pilha compartilhada | 05 |
| JOB-01 | `jobs_list`, `job_get`, `job_cancel` | B; list: P; get/cancel: `job_id`; cancel: W | Estado real, cancelabilidade e resultados; consulta permitida por outro vínculo da mesma instância/projeto | 17 |
| LOG-01 | `logs_read` | B+P; `level`, `job_id` opcionais | Entradas com origem/cursor, perda por retenção explicitada e nenhum segredo | 17 |
| IMP-01 | `asset_import_settings_get`, `asset_import_settings_schema_get`, `asset_import_settings_update` | B+`asset_id`+`scope`; update: W+`patch` | Configurações de import por tipo, plataforma e ambiente; persistência/invalidations existentes | 18 |
| IMP-02 | `asset_import`, `asset_reimport` | B+W+`asset_id`+`scope` | Job de import, etapas/cancelamento/erro e recurso anterior preservado até publicação | 18 |
| BLD-01 | `build_configuration_get`, `build_configuration_update` | B+`platform_id`+`profile_id`; update: W+`patch` | Configuração atual usando serviços existentes | 19 |
| BLD-02 | `build_queue_get`, `build_queue_add`, `build_queue_remove` | B; add/remove: W; add: `platform_id`, `profile_id`, `output_path`, `scene_ids`; remove: `queue_item_id` | Fila e validação de destinos; remoção de item em execução respeita estado/cancelamento | 19 |
| BLD-03 | `build_settings_copy`, `build_scenes_reset` | B+W; copy: `source_platform_id`, `destination_platform_id`; reset: `platform_id` | Mesmas ações Copy Settings e Reset Scenes to Project da interface | 19 |
| BLD-04 | `build_start`, `build_queue_start` | B+W+`unsaved_policy`; build_start: `queue_item_id` | Job e snapshot imutável dos inputs; fonte salva declarada | 19 |
| PLY-01 | `play_get_state`, `play_settings_get`, `play_settings_update` | B; settings: `platform_id`; update: W+`patch` | Estado, settings e capacidades do player instalado | 20 |
| PLY-02 | `play_start`, `play_stop` | B+W; start: S+`platform_id`, `wait_for_debugger` bool padrão false, `unsaved_policy` | Preparação e lançamento acompanhados como job; Stop não fecha editor | 20 |
| EXT-01 | `project_commands_list`, `project_command_execute` | B; list: P; execute: W+`command_id` | Comandos e menus contribuídos; execução usa contexto do projeto vinculado | 21 |
| EXT-02 | `scripts_build`, `scripts_reload`, `project_open_ide` | B+W; build: `platform_id` quando exigido pelo fluxo atual | Build/reload acompanhados e erros preservados; IDE usa solução do projeto | 21 |

Não transformar referência ausente/errada em raiz. Criar sem parent é a única operação que pode declarar raiz como padrão. Propriedade “zerada” não equivale a remover override.

## Origem funcional auditada e extrações necessárias

| Origem atual — caminho absoluto | Ações que precisam de cobertura | IDs |
| --- | --- | --- |
| `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\EditorTitleBar.cs` | File/New/Open/Save/Save As/Scene Settings/Preferences; Add e quatro luzes; Build/Settings/Platforms/Profiles/Build Scripts/Open in IDE; Tools/Environments/Export; UI/Show/Save/Load | SCN, ENT, PRJ, BLD, EXT, WSP |
| `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs` | Atalhos save/undo/redo/focus/delete/duplicate; transições; seleção; Play/Stop; callbacks de configuração, fila e export | HIS, SCN, ENT, SEL, VWP, PLY, PRJ, BLD |
| `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\asset\AssetBrowserPanel.cs` | New File por template, New Folder, Add to scene para Model/Blueprint | AST, ENT |
| `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\asset\AssetBrowserView.cs` | Navegação, seleção e operações de arquivo disponíveis; serviços de move/duplicate já separados | AST, SEL |
| `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\ComponentPropertiesView.cs` | Componentes, propriedades, referências, read-only e escopos | CMP, ENT |
| `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\MaterialAssetView.cs` | Campos de materiais, referências e variantes por plataforma/ambiente | DOC |
| `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\AnimationClipAssetView.cs` | Campos de clips e variantes suportadas | DOC |
| `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\AssetImportSettingsView.cs` | Settings e Apply de importadores de cada tipo | IMP |
| `C:\dev\helworks\helengine\engine\helengine.editor\managers\scene\MeshComponentModifierStackService.cs` | Criar/editar/remover/ordenar pilha e atualizar preview | DOC |
| `C:\dev\helworks\helengine\engine\helengine.editor\serialization\blueprint\BlueprintSaveService.cs` | Gravar blueprint por caminho de autoria válido | DOC |
| `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\BuildDialog.cs` | Fila, output, scenes, copy/reset settings e execução | BLD |
| `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\PreviewPanel.cs` | Fonte, lock e estado do preview | WSP |
| `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\EditorTitleBarUiMenuAction.cs` | Seis tipos de painel; slots 1 a 5 de salvar/carregar | WSP |
| `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorCommandExecutionService.cs` | Catálogo e execução de comandos do projeto | EXT |

Este levantamento confirma pontos de entrada, não todas as ramificações de cada formulário. A tarefa de domínio deve enumerar campos e ações de sua origem antes de fechar seus schemas; se houver algo fora da família de schema prevista, acrescentar operação específica e teste. Não marcar o domínio completo apenas porque há uma ferramenta com nome genérico.

## Persistência, histórico e evidência de conclusão

Cada definição em `C:\dev\helworks\helengine\engine\helengine.editor.automation\editor-tools.json` registra `persistence` (`none`, `scene_dirty`, `immediate_disk`, `job_output`, `type_defined`, `command_defined`), `undoable`, tarefa proprietária, origem funcional e teste comportamental correspondente. As duas últimas categorias declaram variação por documento/comando e retornam o efeito concreto quando conhecido, ou `unknown` quando um comando opaco não permite determiná-lo. Não afirmar undo universal; `undoable` é false quando não há garantia para toda a ferramenta.

Ao concluir uma tarefa, atualizar a evidência por ID de cobertura com nome do teste, resultado e commit. Para clientes reais, usar `C:\dev\helworks\helengine\docs\validation\editor-mcp-client-validation.md`, contendo cliente/interface/versão, protocolo observado, cenário, resultado e limitação. Esse relatório será criado na implementação, não é evidência já existente.

Critérios finais:

1. Todo nome de ferramenta desta matriz tem schema e handler ou capacidade explicitamente indisponível por condição real do editor; ausência de implementação não é uma capacidade indisponível aceitável para concluir a entrega.
2. Toda ação funcional da origem auditada tem operação equivalente e teste. Drag de janela, hover, abrir/fechar um dropdown e desenho de pixels podem ser classificados como apresentação; o efeito de domínio acionado por eles continua coberto.
3. Fixtures de editor validam estado e arquivos após escrita, undo, save e reload; testes de schema sozinhos não provam paridade.
4. Codex e Claude Code executam bind e rebind sem alterar configuração após nova porta. Interfaces adicionais só são declaradas suportadas quando exercitadas.
