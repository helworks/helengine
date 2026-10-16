# Revisão da tarefa 02 — registro local e credenciais por instância

### Spec Compliance

- ✅ **Conforme no escopo verificável:** o diff implementa descritores e credenciais em diretórios separados, filenames derivados de SHA-256 com validação do ID real, DACL protegida para o SID atual, publish do descriptor por temporário no mesmo diretório com flush e rename POSIX, diagnóstico isolado de entradas inválidas, e remoção condicionada à identidade lida da mesma handle. Os testes cobrem identidades distintas no mesmo projeto, entrada corrompida com candidato saudável, junction no caminho solicitado, substituição durante cleanup e roundtrip de casing/Unicode.
- ⚠️ **Política de Windows mínimo pendente:** o código só verifica `OperatingSystem.IsWindows()` em `C:\dev\helworks\helengine\.worktrees\editor-mcp\engine\helengine.editor.automation\discovery\EditorRegistryPaths.cs:232` e chama a classe nativa 22 em `C:\dev\helworks\helengine\.worktrees\editor-mcp\engine\helengine.editor.automation\discovery\EditorInstanceRegistry.cs:332`. A documentação Microsoft do SDK declara `FileRenameInformationEx` disponível a partir do Windows 10 1709 ([FILE_INFORMATION_CLASS](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/wdm/ne-wdm-_file_information_class)); a macro RS1 no header só condiciona a forma de compilação do campo. Em host abaixo do piso documentado, Publish falha com `Win32Exception`, sem fallback não atômico. O único host testado foi build 26200; a política de suporte ainda depende da decisão de Helena. Não há evidência de execução em versões antigas nem de suporte uniforme entre filesystems.

### Strengths

- `C:\dev\helworks\helengine\.worktrees\editor-mcp\engine\helengine.editor.automation\discovery\EditorInstanceRegistry.cs:268` grava o snapshot sem segredo em temporário no mesmo diretório; `C:\dev\helworks\helengine\.worktrees\editor-mcp\engine\helengine.editor.automation\discovery\EditorInstanceRegistry.cs:312` faz rename sobre o temporário já flushed, com `REPLACE_IF_EXISTS|POSIX_SEMANTICS` e handle aberto com compartilhamento de leitura/escrita/delete.
- `C:\dev\helworks\helengine\.worktrees\editor-mcp\engine\helengine.editor.automation\discovery\EditorLocalCredentialStore.cs:36` cria token com 32 bytes de RNG, o armazena separado do descriptor e publica sem sobrescrever credencial existente. `C:\dev\helworks\helengine\.worktrees\editor-mcp\engine\helengine.editor.automation\discovery\EditorRegistryPaths.cs:133` e `C:\dev\helworks\helengine\.worktrees\editor-mcp\engine\helengine.editor.automation\discovery\EditorRegistryPaths.cs:144` montam DACLs protegidas com ACE apenas para o SID atual.
- `C:\dev\helworks\helengine\.worktrees\editor-mcp\engine\helengine.editor.automation\discovery\EditorRegistryPaths.cs:156` abre sem seguir reparse point e valida o objeto; `C:\dev\helworks\helengine\.worktrees\editor-mcp\engine\helengine.editor.automation\discovery\EditorRegistryPaths.cs:191` marca para remoção o arquivo identificado pela handle, evitando apagar substituto no mesmo caminho. O teste sincronizado em `C:\dev\helworks\helengine\.worktrees\editor-mcp\engine\helengine.editor.mcp.tests\EditorInstanceRegistryTests.cs:174` exercita a substituição durante cleanup.
- A descoberta em `C:\dev\helworks\helengine\.worktrees\editor-mcp\engine\helengine.editor.automation\discovery\EditorInstanceRegistry.cs:90` continua após JSON corrompido, confere que o ID do conteúdo corresponde ao filename e rejeita entradas reparse/diretórios em `C:\dev\helworks\helengine\.worktrees\editor-mcp\engine\helengine.editor.automation\discovery\EditorInstanceRegistry.cs:105` e `C:\dev\helworks\helengine\.worktrees\editor-mcp\engine\helengine.editor.automation\discovery\EditorInstanceRegistry.cs:127`.
- Evidência registrada em `C:\dev\helworks\helengine\.worktrees\editor-mcp\.superpowers\sdd\2026-10-15-editor-mcp\task-2-report.md`: filtro novo 6/6 passou no Windows build 26200, com publish direto e ACL real. Não reexecutei a suíte conforme o escopo de revisão.

### Issues

#### Critical (Must Fix)

- Nenhum.

#### Important (Should Fix)

- Nenhum defeito funcional bloqueante encontrado no diff.

#### Minor (Nice to Have)

- `C:\dev\helworks\helengine\.worktrees\editor-mcp\engine\helengine.editor.mcp.tests\EditorInstanceRegistryTests.cs:91`: a prova concorrente de atomicidade não garante que qualquer leitura aconteça durante as 24 substituições. O writer espera a primeira leitura e `ReadCount > 0` também passa se o leitor só observar o arquivo inicial; nesse escalonamento, a asserção não prova o contrato de publish atômico. Tornar a interleaving determinística com barreira/hook de teste ou exigir leitura observada enquanto o writer ainda publica e validar os snapshots dessa janela.
- `C:\dev\helworks\helengine\.worktrees\editor-mcp\engine\helengine.editor.mcp.tests\EditorInstanceRegistryTests.cs:147`: o assertion `Assert.Equal(2, Candidates.Count)` exigido pelo brief gera o aviso novo xUnit2013. Não afeta o resultado; se o build exigir saída sem warnings, suprimir o diagnóstico localmente mantendo a verificação explícita solicitada.

### Assessment

**Task quality:** Approved

**Reasoning:** Os invariantes centrais de armazenamento privado, atomicidade, identidade e remoção condicional estão implementados com handles/ACLs nativos e têm testes de filesystem, não mocks. Permanecem uma lacuna menor de determinismo na prova concorrente e a decisão explícita do piso de Windows, sem alegação de compatibilidade em sistemas não testados.
