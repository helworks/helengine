### Tarefa 21 — Scripts, IDE e comandos de projeto

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorProjectCommandOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorProjectCommandOperationTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorCommandExecutionService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorGameSolutionService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`.

**Interfaces:** handler produz `project_commands_list`, `project_command_execute`, `scripts_build`, `scripts_reload`, `project_open_ide`. Comando usa ID do catálogo e IEditorCommandContext existente; nenhum novo contrato de argumentos livres é presumido para IEditorCommand.Execute.

- [ ] Escrever `Command_executes_in_bound_project_context`, `Build_scripts_reports_job_failure`, `Hot_reload_invalidates_component_schema`, `Unknown_command_is_not_shell_execution`; `Assert.Equal(boundRoot, observedRoot); Assert.Equal("validation_failed", unknown.ErrorCode)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorProjectCommandOperationTests`.
- [ ] Integrar Build Scripts/Open in IDE e extensões registradas. Comandos opacos são declarados mutáveis, sem garantia de undo/cancelamento, e invalidam revisão mesmo se falharem após efeito parcial. Comando síncrono que exige frame não deve ser lançado arbitrariamente numa thread de trabalho; resultado documenta essa restrição.
- [ ] Verde com o mesmo filtro e testes existentes de command execution/game solution/hot reload.
- [ ] Commit: `feat: expose project commands script workflows and IDE launch`.


