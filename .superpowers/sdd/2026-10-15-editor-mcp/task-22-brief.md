### Tarefa 22 — Distribuição e configuração única

**Criar:** `C:\dev\helworks\helengine\tools\release\Install-EditorMcpConnector.ps1`; `C:\dev\helworks\helengine\tools\release\portable\Start-HelengineMcp.cmd`; `C:\dev\helworks\helengine\docs\editor-mcp.md`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp.tests\EditorMcpDistributionTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\tools\release\Build-Portable.ps1`; `C:\dev\helworks\helengine\tools\release\Build-Release.ps1`; `C:\dev\helworks\helengine\tools\release\Test-PortableRelease.ps1`.

**Interfaces:** instalador aceita `-InstallRoot` e `-ConnectorSource` absolutos, publica executável/launcher num caminho estável por usuário e imprime configurações de Codex/Claude. Na máquina atual, exemplo de destino: `C:\Users\Helena\AppData\Local\Helengine\mcp`; resolver usuário pela API do sistema, não fixar Helena no código. Testes injetam raiz em `$Artifacts`.

- [ ] Escrever `Packaged_connector_runs_without_global_dotnet`, `Install_path_survives_editor_version_change`, `Spaces_in_install_path_are_quoted`, `Generated_configs_contain_no_port_or_secret`; `Assert.False(configContainsEndpoint); Assert.True(handshakeSucceeded)`.
- [ ] Vermelho: `$Mcp`, `FullyQualifiedName~EditorMcpDistributionTests`.
- [ ] Publicar conector separado, sem DLLs de renderização; usar runtime incluído e manter ASP.NET para o editor. Atualização preserva launcher estável e não mistura binários de versões. Instruções instalam servidor stdio uma vez; não sobrescrever arquivos de configuração reais durante testes nem modificar configurações de Helena sem autorização de execução.
- [ ] Verde com o mesmo filtro e teste de pacote mínimo para editor/conector. Não executar release de todas as plataformas como pré-requisito.
- [ ] Commit: `feat: distribute stable local MCP connector and setup instructions`.


