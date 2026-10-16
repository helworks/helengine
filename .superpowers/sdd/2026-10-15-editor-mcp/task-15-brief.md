### Tarefa 15 — Projeto, plataformas, ambientes e perfis

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorProjectOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorProjectConfigurationOperations.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorProjectOperationTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\ProjectSettingsDialog.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\PlatformsDialog.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\EnvironmentsDialog.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\ProfilesDialog.cs`.

**Interfaces:** `JsonElement Read(string section)`, `void Apply(string section, JsonElement patch)` para `project`, `scene_routing`, `platforms`, `platform_groups`, `environments`, `profiles`, `runtime_loading`; validação deriva dos documentos e serviços existentes.

- [ ] Escrever `Configuration_updates_use_existing_persistence`, `Disabled_platform_is_rejected`, `Removing_in_use_environment_is_validated`, `Project_generation_change_invalidates_binding`; `Assert.Equal(expectedConfig, reopenedConfig); Assert.Equal("project_changed", result.ErrorCode)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorProjectOperationTests`.
- [ ] Extrair handlers de confirmação e compor os serviços existentes. Declarar persistência imediata quando real. Consultas mostram plataformas instaladas/habilitadas separadamente. Não instalar toolchains nem inventar operação de trocar projeto se o host não a oferece; a invalidação de geração continua obrigatória.
- [ ] Verde com o mesmo filtro e testes existentes de configurações/plataformas/ambientes/perfis.
- [ ] Commit: `feat: expose project configuration with explicit persistence`.


