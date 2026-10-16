# Tarefa 02 — registro local e credenciais por instância

## Resultado

Implementados o registro local privado, o armazenamento separado de credenciais e os caminhos de produção em `%LocalAppData%\Helengine\EditorMcp\{instances,credentials}`. Os nomes de arquivo derivam do SHA-256 do ID exato; o conteúdo continua validando o ID real. Descritores não contêm segredo. O diretório e os arquivos recebem DACL protegida, com apenas o SID do usuário atual e sem herança ou grant adicional para SYSTEM.

Arquivos da tarefa:

- `C:\dev\helworks\helengine\.worktrees\editor-mcp\engine\helengine.editor.automation\discovery\EditorInstanceRegistry.cs`
- `C:\dev\helworks\helengine\.worktrees\editor-mcp\engine\helengine.editor.automation\discovery\EditorLocalCredentialStore.cs`
- `C:\dev\helworks\helengine\.worktrees\editor-mcp\engine\helengine.editor.automation\discovery\EditorRegistryPaths.cs`
- `C:\dev\helworks\helengine\.worktrees\editor-mcp\engine\helengine.editor.mcp.tests\EditorInstanceRegistryTests.cs`

## RED / investigação

A primeira execução do filtro novo falhou na compilação, como esperado antes da implementação: `CS0234`, namespace `helengine.editor.automation.discovery` ausente. Log: `C:\dev\helworks\builds\helengine\editor-mcp\task01-implementation-20261015-b\task02-red-registry.log`.

A prova determinística de substituição foi escrita antes da correção de remoção e falhou com `CS1501`: não havia ainda o caminho interno de sincronização usado pelo teste. Esse caminho é `internal` e visível apenas ao assembly de testes; a API pública permanece `Remove(string)` / `Delete(string)`. Log: `C:\dev\helworks\builds\helengine\editor-mcp\task01-implementation-20261015-b\task02-red-delete-by-handle.log`.

Na investigação de publicação concorrente, o caminho final já existia, mas o open direto recebeu sharing violation. A causa foi o handle do publicador permanecer aberto com `FileShare.None` depois do rename. O temporário do descriptor passou a permitir `ReadWrite|Delete`; após a correção, o teste de open direto passou. A descoberta enumerável não é tratada como snapshot durante mutação: a atomicidade é exercitada abrindo o caminho conhecido e lendo JSON completo, e `ReadCandidates` verifica a descoberta separadamente após a escrita estabilizar. Logs: `C:\dev\helworks\builds\helengine\editor-mcp\task01-implementation-20261015-b\task02-atomic-direct-final.log` (falha inicial) e `C:\dev\helworks\builds\helengine\editor-mcp\task01-implementation-20261015-b\task02-atomic-direct-sharefix.log` (passou), no diretório de artifacts citado acima.

A remoção por caminho tinha uma corrida entre a leitura de identidade e `File.Delete`. Agora a entrada é aberta com `CreateFileW` e `FILE_FLAG_OPEN_REPARSE_POINT`, a identidade é desserializada da mesma handle e `SetFileInformationByHandle(FileDispositionInfo)` marca somente aquele objeto para exclusão. O teste substitui o descritor e a credencial após a validação e confirma que ambas as novas entradas continuam presentes. Durante esse teste, o rename POSIX já havia removido o link do objeto antigo; uma disposição adicional retornou Win32 5. `FILE_STANDARD_INFO.NumberOfLinks` confirma o estado sem links; nesse caso a operação é concluída sem outro delete. Se houver links ou a API de metadados/disposição falhar, a falha permanece explícita. A mesma prova também confirma remoção normal enquanto o arquivo ainda está ligado.

A execução sem elevação foi negada ao criar fixtures sob a DACL privada, como esperado do sandbox; a ACL não foi enfraquecida. O filtro final foi executado com elevação autorizada, limitado às fixtures sob `C:\dev\helworks\builds\helengine\editor-mcp\task01-implementation-20261015-b`. Nenhum output ou fixture foi direcionado a TEMP.

## GREEN e ambiente

Comando final:

```powershell
$Dotnet = 'C:\dev\helworks\helengine\.worktrees\editor-mcp\dotnetw.ps1'
$TestProject = 'C:\dev\helworks\helengine\.worktrees\editor-mcp\engine\helengine.editor.mcp.tests\helengine.editor.mcp.tests.csproj'
$Artifacts = 'C:\dev\helworks\builds\helengine\editor-mcp\task01-implementation-20261015-b'
$env:HELENGINE_EDITOR_MCP_ARTIFACTS = $Artifacts
& $Dotnet test $TestProject --no-restore '-p:RestorePackagesPath=C:\dev\helworks\helengine\.nuget\packages' --filter 'FullyQualifiedName~EditorInstanceRegistryTests' --artifacts-path $Artifacts --logger trx --results-directory (Join-Path $Artifacts 'task02-green-final') -v minimal
```

Saída final: exit code 0, `Passed: 6, Failed: 0, Skipped: 0`, duração 617 ms. TRX: `C:\dev\helworks\builds\helengine\editor-mcp\task01-implementation-20261015-b\task02-green-final\Helena_DESKTOP-HELENA_2026-10-15_17_00_36.trx`; log: `C:\dev\helworks\builds\helengine\editor-mcp\task01-implementation-20261015-b\task02-green-final.log`.

O teste de ACL executa `GetEffectiveRightsFromAclW` para o SID atual e `NetworkService`, valida direitos concretos `ReadData|WriteData|Delete`, verifica retorno nativo e abre o arquivo real com `FileAccess.ReadWrite` como dono. Há teste real de junction (`mklink /J`), arquivo JSON corrompido com diagnóstico sem ocultar registro saudável, identidades distintas sobre o mesmo projeto e roundtrip de Unicode/casing.

Ambiente observado: SDK .NET `9.0.308`; alvo e host de teste `net9.0` / `.NETCoreApp,Version=v9.0`; VSTest `17.14.1 (x64)`; Windows `Microsoft Windows NT 10.0.26200.0`. A tarefa não adiciona dependências ou altera alvos de runtime.

A publicação substitutiva usa `SetFileInformationByHandle` com `FILE_RENAME_INFO_EX` (`REPLACE_IF_EXISTS|POSIX_SEMANTICS`), temporário no mesmo diretório e flush antes do rename. O open de remoção usa `FILE_FLAG_OPEN_REPARSE_POINT`; atributos e link count são obtidos pela handle (`GetFileInformationByHandleEx`, `FileAttributeTagInfo`/`FileStandardInfo`). Referências oficiais:

- [CreateFileW e FILE_FLAG_OPEN_REPARSE_POINT](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew)
- [GetEffectiveRightsFromAclW usado no teste de ACL](https://learn.microsoft.com/en-us/windows/win32/api/aclapi/nf-aclapi-geteffectiverightsfromaclw)
- [GetFileInformationByHandleEx e classes FileStandardInfo/FileAttributeTagInfo](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getfileinformationbyhandleex)
- [FILE_DISPOSITION_INFO](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_disposition_info)
- [SetFileInformationByHandle](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfileinformationbyhandle)
- [FILE_RENAME_INFORMATION](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntifs/ns-ntifs-_file_rename_information)
- [FILE_INFORMATION_CLASS](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/wdm/ne-wdm-_file_information_class)
- [.NET para Windows: requisitos e sistemas suportados](https://learn.microsoft.com/en-us/dotnet/core/install/windows)

## Self-review e limitação

`git diff --check` passou. A revisão confirmou somente os três arquivos de discovery e o teste da tarefa02 como alterações de código. O filtro compila e executa em `net9.0`; permanece um aviso xUnit2013 para `Assert.Equal(2, Candidates.Count)`, mantido conforme o brief pede explicitamente essa verificação.

A API nativa `FILE_RENAME_INFO_EX` tem piso documentado Windows 10 1709. .NET 9 documenta uma base de suporte de Windows mais ampla; a execução real ocorreu apenas no build 26200. A compatibilidade mínima pretendida ainda aguarda decisão de Helena. Nenhuma garantia de suporte em builds Windows mais antigos ou uniformidade entre filesystems é declarada neste relatório; erros de recursos nativos indisponíveis continuam explícitos, sem fallback não atômico.

Commit desta tarefa: `950dc45faeb7285c7d020b5b6e9ad644dff4c6a0 feat: discover local editor instances with private credentials`. `git diff --cached --check` passou antes do commit; o worktree ficou limpo após o commit.