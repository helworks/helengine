# Window Frame Capture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Entregar uma biblioteca independente da Helengine para obter frames GPU de uma janela Windows por HWND e copiar seus pixels somente quando solicitado.

**Architecture:** Backend C++/WinRT x64 com sessão Windows Graphics Capture persistente, frames em texturas D3D11 próprias e orçamento de buffers limitado. Fachada C# net9.0 com P/Invoke e SafeHandle. Frames entregues mantêm seus recursos após o encerramento da sessão; os projetos de vídeo existentes não são alterados.

**Tech Stack:** .NET SDK 9.0.308, C# net9.0, MSVC v143, C++17, Windows SDK 10.0.26100.0, Windows Graphics Capture, D3D11, xUnit 2.9.3.

**Spec:** `C:\dev\helworks\helengine\docs\superpowers\specs\2026-10-20-window-frame-capture-design.md`, aprovada por Helena em 2026-10-20.

## Global Constraints

- Trabalhar no checkout real, na main, preservando mudanças preexistentes e sem worktrees.
- BufferCapacity padrão de três; capacidade positiva; fila e frames emprestados compartilham o orçamento.
- Esta primeira versão usa BGRA8/SDR. Não implementa HDR ou tone mapping e não promete fidelidade HDR.
- Cursor desligado por padrão; indicador de captura do Windows mantido por padrão.
- Nenhuma cópia CPU implícita, escala, conversão, duração inventada ou imposição de 60 FPS.
- Fachada sem dependência de editor, player, vídeo, FFmpeg, ImageSharp, Gevo, HelenUI ou MonoGame.
- Biblioteca C# net9.0, nullable desabilitado, documentação XML substantiva e uma classe por arquivo.
- Reaproveitar o mecanismo das fontes listadas na especificação; não alterar esses repositórios.
- O build usa sempre `C:\dev\helworks\builds\helengine-capture\windows`, sobrescrevendo a saída anterior.
- Scripts descartáveis, logs e capturas temporárias ficam somente em `C:\temp` e são removidos ao concluir.
- A integração abre apenas uma janela sintética pertencente ao próprio teste, com padrão de pixels conhecido.
- Sem CLI de gravação, áudio, encoder, automação de menus ou integração com os pacotes de vídeo nesta etapa.

Estado preexistente autorizado: alterações em `C:\dev\helworks\helengine\docs\superpowers\plans\2026-10-18-viewport-layout-processing.md` e arquivo não rastreado `C:\dev\helworks\helengine\engine\helengine.player\helengine.player.csproj.user`. Não incluir esses arquivos nos commits. Novas alterações externas no caminho da implementação ou mudança de branch exigem parar e consultar Helena.

## Review Focus

1. Consumidor mantém frames durante vários redimensionamentos: memória continua limitada e os pixels antigos não mudam. Testes nas tarefas 1 e 4.
2. Dispose coincide com callback em andamento ou falha do device: nenhuma publicação após encerramento, deadlock ou uso de recurso liberado. Testes nas tarefas 2 e 4.
3. Versões/tamanhos ABI incorretos e erro simultâneo em duas sessões: rejeição explícita e diagnósticos não trocados. Testes nas tarefas 2 e 3.
4. Linhas GPU com padding, dimensões inválidas ou tamanho de buffer que excede o limite: cópia exata ou erro, sem acesso fora dos limites. Testes nas tarefas 1 e 3.
5. API opcional de borda indisponível ou autorização negada: falha identificável somente quando a remoção é solicitada; opção padrão continua utilizável. Testes na tarefa 2.

## Organização e contratos compartilhados

Todos os caminhos abaixo são arquivos novos, exceto a atualização de estado desta especificação ao concluir a entrega.
Não alterar a solução do editor nem adicionar referências aos projetos existentes: o build específico produz e valida a biblioteca independentemente.

### API gerenciada

Namespace `helengine.capture.windows`. Os enums têm armazenamento int32 e valores explícitos.

```csharp
public enum WindowCaptureState { Running = 0, WindowClosed = 1, Faulted = 2, Disposed = 3 }
public enum WindowCapturePixelFormat { Bgra8 = 1 }
public sealed class WindowCaptureOptions { /* BufferCapacity=3, CaptureCursor=false, RemoveBorder=false */ }
public static WindowCaptureSession Open(IntPtr windowHandle, WindowCaptureOptions options);
public static WindowCaptureSession Open(IntPtr windowHandle, IntPtr deviceHandle, WindowCaptureOptions options);
public bool TryAcquireFrame(out CapturedWindowFrame frame);
public WindowCaptureState State { get; }
public WindowCaptureStatistics Statistics { get; }
public byte[] CapturedWindowFrame.CopyPixels();
```

As assinaturas Open pertencem a WindowCaptureSession. Options tem propriedades init e validação antes de entrar na ABI; null não é substituído por opções padrão. A sobrecarga com device exige ponteiro COM válido e não nulo para ID3D11Device. Não tentar sondar endereços arbitrários de memória.

CapturedWindowFrame expõe `int Width`, `int Height`, `WindowCapturePixelFormat Format`, `TimeSpan Timestamp`, `ulong SequenceNumber`, `IntPtr TextureHandle` e `IntPtr DeviceHandle`; acesso aos ponteiros e CopyPixels após Dispose lança ObjectDisposedException. Metadados escalares permanecem consultáveis. TryAcquireFrame retorna false com frame=null somente na ausência temporária de frames; estado WindowClosed lança WindowCaptureClosedException, Faulted lança WindowCaptureException com o erro terminal e Disposed lança ObjectDisposedException. WindowCaptureException deriva de ExternalException e preserva Operation e HRESULT; WindowCaptureClosedException deriva de InvalidOperationException.

Statistics é um snapshot imutável com contadores ulong `ObservedFrames`, `DeliveredFrames`, `OverflowDiscardedFrames`, `ResizeDiscardedFrames` e `ShutdownDiscardedFrames`. State e Statistics permanecem consultáveis após Dispose, a partir do snapshot terminal armazenado antes da liberação do handle.

### ABI C v1

Em `C:\dev\helworks\helengine\engine\helengine.capture.windows.native\include\helengine_capture_windows.h`, usar packing explícito de 8 e estes layouts, na ordem indicada:

- `he_capture_options`: uint32 Size, Version, BufferCapacity, CaptureCursor, RemoveBorder; tamanho 20.
- `he_capture_error`: uint32 Size, Version; int32 HResult; wchar_t Operation[64], Message[512]; tamanho 1164 no Windows.
- `he_capture_frame_info`: uint32 Size, Version; void* Texture, Device; uint32 Width, Height, PixelFormat, Reserved; int64 TimestampTicks; uint64 SequenceNumber; tamanho 56 em x64.
- `he_capture_statistics`: uint32 Size, Version; uint64 ObservedFrames, DeliveredFrames, OverflowDiscardedFrames, ResizeDiscardedFrames, ShutdownDiscardedFrames; tamanho 48.

O chamador inicializa Size e Version=1; enums/flags desconhecidos, tamanhos divergentes, buffers insuficientes e parâmetros obrigatórios nulos falham com diagnóstico da operação. As exceções não atravessam a ABI. Erro é preenchido no buffer da própria chamada, inclusive quando a falha nasceu num callback anterior.
O enum nativo `he_capture_state : uint32_t` tem os mesmos valores 0/1/2/3 do contrato gerenciado. `PixelFormat=1` representa BGRA8 e não é o número bruto do DXGI_FORMAT.

Exportações cdecl, com símbolos sem decoração:

```cpp
int32_t he_capture_open(void* hwnd, void* device, const he_capture_options* options,
    void** session, he_capture_error* error);
int32_t he_capture_try_acquire(void* session, void** frame,
    he_capture_frame_info* info, he_capture_error* error);
int32_t he_capture_get_state(void* session, uint32_t* state, he_capture_error* error);
int32_t he_capture_get_statistics(void* session, he_capture_statistics* stats, he_capture_error* error);
int32_t he_capture_copy_pixels(void* frame, uint8_t* destination, uint64_t length, he_capture_error* error);
int32_t he_capture_stop(void* session, he_capture_statistics* finalStats, he_capture_error* error);
void he_capture_release_frame(void* frame) noexcept;
void he_capture_close(void* session) noexcept;
```

Open aceita device=null para criar o próprio device. Open/getters/copy/stop retornam 0 para sucesso e -1 para falha. Acquire retorna 0 para vazio, 1 para frame, 2 para WindowClosed e -1 para falha; saídas frame e ponteiros ficam nulos quando não há entrega. Stop encerra a captura e limpa a fila, mas conserva o objeto nativo até close; seu snapshot final já inclui os descartes dessa limpeza. Close faz stop idempotente e destrói o objeto; também serve ao SafeHandle no caminho de finalização. Os void handles representam posse única; SafeHandle garante uma única liberação. Não prometer que liberar duas vezes um ponteiro nativo já destruído seja permitido.

### Execução e artefatos

Criar o script permanente `C:\dev\helworks\helengine\scripts\build-window-capture.ps1`, com `-Action Build|NativeTests|ManagedTests|IntegrationTests|All` e `-TestFilter <string>` opcional para os testes gerenciados. NativeTests usa somente texturas sintéticas; IntegrationTests exige a sessão gráfica e captura exclusivamente a janela do teste.

O script localiza MSBuild/v143 com vswhere, valida o SDK .NET fixado pelo repositório e o Windows SDK acima, e preserva códigos de saída. Usar o MSBuild instalado em `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe` quando essa instalação for selecionada. Ausência de toolchain é erro explícito; não instalar componentes automaticamente.

Executáveis/DLLs nativos ficam diretamente em `C:\dev\helworks\builds\helengine-capture\windows`; objetos ficam nos subdiretórios convencionais obj por projeto. Artefatos .NET ficam sob `C:\dev\helworks\builds\helengine-capture\windows\managed` usando --artifacts-path e UseCommonOutputDirectory=false. Nenhuma saída de build vai para o checkout. O script não apaga recursivamente uma raiz de build: sobrescreve seus arquivos específicos.

Logs, resultados temporários e DOTNET_CLI_HOME específico deste trabalho ficam sob `C:\temp\helengine-window-capture`; o cache NuGet persistente existente pode ser reutilizado. Não criar nomes por tentativa. Limpar somente os temporários criados por esta execução, sem apagar conteúdo preexistente de terceiros.

## Task 1: Recursos GPU, orçamento de frames e leitura explícita

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows.native\helengine.capture.windows.native.vcxproj`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows.native\include\helengine_capture_windows.h`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows.native\src\CaptureGpuContext.hpp`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows.native\src\CaptureFrame.hpp`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows.native\src\CaptureFrameBudget.hpp`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows.native\src\CaptureFrameQueue.hpp`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows.tests\native\CaptureNativeTests.vcxproj`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows.tests\native\FrameResourceTests.cpp`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows.tests\native\NativeTestMain.cpp`.
- Create: `C:\dev\helworks\helengine\scripts\build-window-capture.ps1`.

**Interfaces:**
- Consumes: ABI/layouts acima; ID3D11Device e ID3D11Texture2D; nenhuma sessão WGC nesta tarefa.
- Produces: `CaptureGpuContext::Create(ID3D11Device*) -> shared_ptr<CaptureGpuContext>`; `CaptureFrameQueue(uint32_t capacity, shared_ptr<CaptureGpuContext>)`; `TryPush(ID3D11Texture2D*, int32_t width, int32_t height, int64_t timestampTicks) -> bool`; `TryAcquire() -> unique_ptr<CaptureFrame>`; `DiscardPending() -> void`; `Snapshot() -> he_capture_statistics`; `CaptureFrame::Info() -> he_capture_frame_info`; `CopyPixels(uint8_t*, uint64_t) -> void`. Cada TryPush corresponde a um frame observado e avança a sequência; tamanho não positivo/fora da textura retorna false e soma ResizeDiscardedFrames, capacidade ocupada retorna false e soma OverflowDiscardedFrames. O descarte da fila soma ShutdownDiscardedFrames.

- [ ] **1. Escrever testes nativos que falham:** `PreservesTimestampAndCopiesOnlyContent` usa textura sintética BGRA com padding e timestamp 123456789; compara bytes, dimensões e timestamp sem normalização. `BudgetCountsQueuedAndLeasedFrames` usa capacidade 3: mantém três leases, push adicional retorna false, OverflowDiscardedFrames=1, liberar um lease permite nova entrega. `HeldFramesSurviveQueueDestructionAndResize` mantém um lease enquanto novas entradas mudam de tamanho; depois destrói a fila e confirma os mesmos pixels/dimensões no lease antigo. `RejectsInvalidSizesAndAbiLayouts` verifica sizeof/offsetof, capacidade 0 rejeitada, região maior que a textura descartada com ResizeDiscardedFrames=1 e cópia para buffer insuficiente rejeitada sem escrever.
- [ ] **2. Executar o RED:** `& 'C:\dev\helworks\helengine\scripts\build-window-capture.ps1' -Action NativeTests`. Criar o runner e o build junto deste teste; o resultado esperado é falha por contratos ausentes, não por toolchain indisponível. O runner retorna código não zero ao falhar uma asserção.
- [ ] **3. Implementar os tipos acima:** shared_ptr mantém contexto/device vivos; cada CaptureFrame possui textura própria e token do orçamento. A fila possui apenas frames pendentes, sem ciclo de shared_ptr. Reservar capacidade antes da alocação e devolver em todos os caminhos de erro. Criar somente a região ContentSize com CopySubresourceRegion. Inicialmente liberar a textura ao terminar o lease é suficiente; não criar cache por resolução. Habilitar/proteger o contexto multithread e rejeitar D3D11_CREATE_DEVICE_SINGLETHREADED. No readback, copiar linhas respeitando RowPitch e validar comprimento antes de escrever. O runner usa WARP explicitamente, sem captura de janela ou fallback no produto.
- [ ] **4. Executar o GREEN:** repetir NativeTests; todos os testes de recursos passam, incluindo o caso que mantém lease após destruição da fila.
- [ ] **5. Commit restrito:** adicionar somente os arquivos novos desta tarefa e registrar `feat: add bounded GPU capture frame resources`. Conferir a lista staged antes do commit; não usar git add -A.

## Task 2: Sessão Windows Graphics Capture e ABI de produção

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows.native\src\CaptureSession.hpp`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows.native\src\CaptureWorker.hpp`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows.native\src\CaptureBorderPolicy.hpp`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows.native\src\CaptureFailure.hpp`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows.native\src\CaptureExports.cpp`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows.tests\native\SessionContractTests.cpp`.
- Modify: `C:\dev\helworks\helengine\engine\helengine.capture.windows.native\helengine.capture.windows.native.vcxproj`.
- Modify: `C:\dev\helworks\helengine\engine\helengine.capture.windows.tests\native\CaptureNativeTests.vcxproj`.
- Modify: `C:\dev\helworks\helengine\scripts\build-window-capture.ps1`.

**Interfaces:**
- Consumes: tipos GPU e fila da tarefa 1; fontes Gevo listadas na especificação.
- Produces: todas as exportações ABI v1 descritas acima; `CaptureSession::Open(HWND, ID3D11Device*, const he_capture_options&) -> unique_ptr<CaptureSession>`; `TryAcquire(unique_ptr<CaptureFrame>&) -> int32_t`; `Stop() -> void`; `Close() noexcept`; `State() -> he_capture_state`; `Fail(HRESULT, wstring operation, wstring message) -> void`; `CaptureBorderPolicy::RequireAllowed(AppCapabilityAccessStatus) -> void`. Open usa um construtor interno `CaptureSession(shared_ptr<CaptureGpuContext>, const he_capture_options&)` e depois inicia o worker; os testes nativos podem exercitar esse mesmo objeto antes de iniciar WGC, sem criar exportação exclusiva de teste.

- [ ] **1. Escrever testes que falham:** `OpenRejectsInvalidWindow` confirma session=null e HRESULT/Operation corretos; `RejectsAbiVersionAndFlags` verifica Size/Version e flags inválidos sem iniciar WGC; `ErrorsRemainPerOperation` chama operações inválidas em duas threads e confirma seus buffers independentes. `TerminalFailureClearsPendingWithoutInvalidatingLease` injeta Fail no tipo interno com frame sintético: estado Faulted, fila vazia e lease ainda legível. `CloseWaitsForInFlightPublication` usa uma barreira determinística no seam interno de publicação para provar que nenhum frame é publicado após Close. `StopReturnsPostCleanupStatistics` enfileira dois frames, encerra e exige ShutdownDiscardedFrames=2 no snapshot final. `BorderPolicyRejectsDeniedStatus` passa DeniedByUser, DeniedBySystem e Allowed ao validador; testar ausência da propriedade opcional somente no caminho RemoveBorder=true por uma sonda interna substituível nos testes.
- [ ] **2. Executar o RED:** NativeTests deve falhar nos contratos recém-adicionados.
- [ ] **3. Implementar a sessão e exportações:** worker MTA próprio por sessão cria/encerra os objetos WinRT; aguardar sua inicialização e propagar falhas. Usar CreateForWindow e CreateFreeThreaded, pool com 2 buffers, cursor conforme opções. Preservar SystemRelativeTime e ContentSize; fechar cada frame original antes de recriar o pool. Callback entrega somente à fila interna e nunca chama consumidor. Descartar tamanhos inválidos com contador de resize, recriar o pool quando necessário e incrementar sequência em cada frame observado. Item.Closed e falhas mudam o estado terminal antes de limpar a fila. Não reiniciar captura usando HWND reutilizado. Close remove callbacks, espera callbacks em voo sem manter locks que eles precisam, termina o worker e libera recursos parciais; não espera leases do consumidor. Capturar exceções na fronteira C, preencher o erro da chamada e manter um diagnóstico terminal próprio por sessão.
- [ ] **4. Executar o GREEN:** NativeTests passa, incluindo inicialização inválida e teardown. Build produz `C:\dev\helworks\builds\helengine-capture\windows\helengine.capture.windows.native.dll`; conferir com dumpbin /exports os oito símbolos he_capture. Testes determinísticos de falhas não introduzem exportações de injeção na DLL de produção.
- [ ] **5. Commit restrito:** `feat: expose Windows window capture sessions through native ABI`.

## Task 3: Biblioteca C# com posse determinística

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows\helengine.capture.windows.csproj`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows\WindowCaptureSession.cs`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows\WindowCaptureOptions.cs`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows\CapturedWindowFrame.cs`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows\WindowCaptureState.cs`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows\WindowCapturePixelFormat.cs`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows\WindowCaptureStatistics.cs`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows\WindowCaptureException.cs`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows\WindowCaptureClosedException.cs`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows\Interop\CaptureNativeApi.cs`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows\Interop\CaptureSessionHandle.cs`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows\Interop\CaptureFrameHandle.cs`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows\Interop\CaptureNativeOptions.cs`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows\Interop\CaptureNativeError.cs`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows\Interop\CaptureNativeFrameInfo.cs`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows\Interop\CaptureNativeStatistics.cs`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows\Interop\CapturePixelBufferUtils.cs`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows.tests\helengine.capture.windows.tests.csproj`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows.tests\CaptureContractTests.cs`.
- Modify: `C:\dev\helworks\helengine\scripts\build-window-capture.ps1`.

**Interfaces:**
- Consumes: ABI v1 e DLL da tarefa 2.
- Produces: API gerenciada do contrato compartilhado. Publicar a DLL nativa como conteúdo de runtime ao usar ProjectReference e CopyToPublishDirectory, sem endereço absoluto embutido no loader.

- [ ] **1. Escrever testes xUnit que falham:** `OptionsHaveApprovedDefaults` compara capacidade 3 e flags false; `RejectsNullOptionsZeroWindowAndNonPositiveCapacity` verifica ArgumentNullException/ArgumentException/ArgumentOutOfRangeException antes de chamar o backend. `NativeLayoutsMatchX64Abi` compara Marshal.SizeOf com 20, 1164, 56 e 48 e offsets dos ponteiros/timestamp. `InvalidWindowPreservesNativeDiagnostic` usa HWND sintético inválido não nulo, confirma Operation e HResult da falha nativa. `CannotReadPixelsIntoOverflowedAllocation` verifica o cálculo checked de width*height*4 no helper interno de tamanho, usado por CopyPixels. Marcar estes testes Trait("Category", "Contract").
- [ ] **2. Executar o RED:** `& 'C:\dev\helworks\helengine\scripts\build-window-capture.ps1' -Action ManagedTests`. Build deve apontar tipos ainda ausentes; o script filtra Category=Contract.
- [ ] **3. Implementar a fachada:** SafeHandle separa lifetime de frame e sessão; P/Invoke recebe SafeHandle nas operações que exigem posse durante a chamada. Inicializar explicitamente structs v1; usar buffers UTF-16 de tamanho fixo para erros. Converter status, HRESULT e mensagens sem fallback silencioso. Frame mantém metadados escalares, valida estado antes de expor ponteiros e aloca byte[] apenas em CopyPixels. `CapturePixelBufferUtils.GetByteCount(int width, int height) -> int` valida dimensões e calcula checked(width*height*4). Dispose chama stop, guarda o snapshot pós-limpeza e marca o estado gerenciado Disposed; finally/SafeHandle libera o handle mesmo se stop reportar erro, sem ocultá-lo. Retornos de erro não adotam handles parciais. Não adicionar SharpDX como dependência da biblioteca. O csproj usa net9.0, x64, implicit usings, nullable disable e UseCommonOutputDirectory=false; declarar documentação XML e compatibilidade Windows. Testes seguem as versões existentes: Microsoft.NET.Test.Sdk 17.14.1, xunit 2.9.3, xunit.runner.visualstudio 3.1.4.
- [ ] **4. Executar o GREEN:** ManagedTests passa; Build confirma que os outputs e a DLL de runtime ficam na raiz de saída aprovada e que compilar a biblioteca não referencia engine/editor/video. Validar build e cópia com caminho de checkout contendo espaços apenas se houver falha de quoting observada; não criar cópia/worktree para testar.
- [ ] **5. Commit restrito:** `feat: add managed window frame capture API`.

## Task 4: Captura sintética de ponta a ponta e entrega reutilizável

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows.tests\SyntheticCaptureWindow.cs`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows.tests\WindowCaptureIntegrationTests.cs`.
- Create: `C:\dev\helworks\helengine\engine\helengine.capture.windows\README.md`.
- Modify: `C:\dev\helworks\helengine\engine\helengine.capture.windows.tests\helengine.capture.windows.tests.csproj`.
- Modify: `C:\dev\helworks\helengine\scripts\build-window-capture.ps1`.
- Modify: `C:\dev\helworks\helengine\docs\superpowers\specs\2026-10-20-window-frame-capture-design.md` somente no estado da entrega, após validação real.

**Interfaces:**
- Consumes: API C# da tarefa 3; testes nativos das tarefas 1 e 2.
- Produces: SyntheticCaptureWindow com HWND próprio, message loop dedicado e operações de pintar/redimensionar/fechar; suíte Category=Integration; comandos de build/teste e exemplo mínimo de uso documentados.

- [ ] **1. Escrever os testes de integração:** `CapturesKnownBgraPatternWithoutScaling` pinta regiões vermelho/verde/azul e confere bytes BGRA em pixels internos, comparando tamanho com ContentSize recebido. `DeliversNewSequencesAndSourceClock` muda o padrão, confirma sequências estritamente crescentes e timestamps não decrescentes na faixa do QPC do sistema, sem exigir igualdade a 60 FPS. `ResizeKeepsHeldFramePixelsStable` guarda um frame, redimensiona várias vezes, descarta novas entregas após leitura e confirma conteúdo/dimensões do frame antigo. `BudgetOverflowIsReportedAndRecovers` usa capacidade 1, mantém o frame e produz outra atualização: overflow aumenta e liberar o frame permite aquisição. `WindowCloseAndSessionDisposePreserveHeldFrame` cobre as duas ordens e verifica estado, CopyPixels e Dispose idempotente. `ExternalDeviceReferenceRemainsOwnedByCaller` fornece um device de teste, fecha a sessão e verifica que o device do chamador continua utilizável. `NoUpdatesNeverReturnPreviousLeaseAgain` verifica que uma mesma SequenceNumber nunca é entregue duas vezes. Desabilitar paralelismo da coleção de captura; não depender de janelas de terceiros ou captura do desktop.
- [ ] **2. Executar o RED:** `& 'C:\dev\helworks\helengine\scripts\build-window-capture.ps1' -Action IntegrationTests`. Esperar falhas específicas dos contratos ainda não satisfeitos; ausência de ambiente gráfico é uma limitação explícita, não sucesso nem motivo para marcar testes como aprovados.
- [ ] **3. Implementar somente o fixture e ajustes necessários aos contratos:** janela Win32 de teste com loop próprio e pintura previsível; helpers de espera com timeout de 10 segundos, sem sleeps ilimitados. Preferir P/Invoke Win32 para não adicionar dependência de UI à biblioteca. Para o teste de device, SharpDX 4.2.0 pode ser referência somente do projeto de testes, conforme precedente local. Registrar os filtros por categoria e propagar qualquer falha. Documentar exemplo Open/while TryAcquireFrame/using frame; explicar BGRA, ponteiros emprestados, timestamps originais, ausência temporária, overflow, sessão descartada e requisitos Windows. Documentar os comandos do script para Build, NativeTests, ManagedTests, IntegrationTests e All. Documentar que remover a borda exige autorização do Windows e não é pré-requisito para o uso padrão.
- [ ] **4. Executar a validação final proporcional:** `& 'C:\dev\helworks\helengine\scripts\build-window-capture.ps1' -Action All`. Esperado: build Release x64; NativeTests, Contract e Integration aprovados com contagem real. Conferir git diff --check, nenhuma saída indevida no checkout e somente arquivos autorizados no diff. Registrar separadamente qualquer teste que não pôde executar; não declarar a captura pronta sem integração real. Remover temporários próprios em `C:\temp\helengine-window-capture` ao terminar. Sem testes gerais do editor/player, pois não há dependência ou alteração nesses projetos.
- [ ] **5. Atualizar o estado da especificação com a evidência obtida e fazer commit restrito:** `test: verify synthetic window capture lifecycle and document usage`. Se a integração estiver bloqueada, registrar o bloqueio em vez de alterar o estado para implementado e validado.

## Revisão e handoff

Autorrevisão do plano: recursos e leitura explícita estão na tarefa 1; sessão, suporte de APIs, borda e diagnóstico na tarefa 2; contratos C#/ABI e posse segura na tarefa 3; captura real sintética, publicação do runtime e documentação nas tarefas 3 e 4. Os cinco itens de Review Focus possuem testes atribuídos. Nenhuma tarefa implementa encoder, vídeo de menu ou altera os projetos de origem.

Método recomendado: execução direta nesta sessão, pois as quatro tarefas dependem dos mesmos contratos de posse e ABI. A alternativa é execução por subagentes, tarefa a tarefa, com revisão de cada etapa. Se Helena escolher delegação, usar gpt-6.1-sol com esforço high e trabalhar no mesmo checkout, sem worktrees. O método depende da escolha de Helena após a leitura deste plano; não iniciar implementação nesta fase.
