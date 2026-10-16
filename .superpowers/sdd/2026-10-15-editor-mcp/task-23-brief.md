### Tarefa 23 — Paridade funcional e validação real dos clientes

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor.mcp.tests\EditorMcpEndToEndTests.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorAutomationCoverageTests.cs`; `C:\dev\helworks\helengine\docs\validation\editor-mcp-client-validation.md`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor.automation\editor-tools.json`; `C:\dev\helworks\helengine\docs\superpowers\plans\2026-10-15-editor-mcp-coverage.md` com evidências e estado de conclusão.

**Interfaces:** catálogo, registry de handlers e matriz devem ter relação verificável por nome. Nenhuma ferramenta anunciada pode faltar no dispatcher sem retornar indisponibilidade de capacidade explícita. Nenhuma operação funcional de UI pode ficar sem ferramenta ou justificativa revisada.

- [ ] Escrever `Every_catalog_tool_has_schema_and_handler`, `Every_functional_menu_action_has_coverage`, `Two_clients_two_editors_survive_rebind`, `Lost_response_does_not_duplicate_mutation`, `Full_authoring_flow_saves_and_reopens`; `Assert.Empty(unmappedTools); Assert.Empty(unmappedActions); Assert.Equal(expectedScene, reopenedScene)`.
- [ ] Vermelho: `$Mcp`, `FullyQualifiedName~EditorMcpEndToEndTests`; `$Editor`, `FullyQualifiedName~EditorAutomationCoverageTests`.
- [ ] Auditar menus, atalhos, inspectors, painéis especializados, toolbar e extensões contra a matriz. Exercitar fluxos reais pelos transportes, com fixture de editor e dois servidores. Novas lacunas são implementadas na tarefa proprietária, não ocultadas por allowlist genérica.
- [ ] Verde com os filtros anteriores e suites dos domínios alterados. No Codex CLI e Claude Code, registrar versão, interface, configuração, protocolo negociado e evidência de bind/read/write/undo/save/rebind com portas diferentes. Testar interfaces desktop/IDE realmente usadas antes de declarar seu suporte. Se faltar acesso ao cliente, registrar limitação e não marcar aceite correspondente.
- [ ] Commit: `test: verify full editor MCP coverage and client binding workflows`.


