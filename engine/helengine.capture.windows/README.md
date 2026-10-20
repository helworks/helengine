# Window frame capture

Biblioteca Windows x64 para acessar frames de uma janela por HWND. Os frames permanecem em texturas
D3D11 independentes na resolução fornecida pelo Windows Graphics Capture; leitura de pixels na CPU
acontece somente ao chamar `CopyPixels()`.

Projeto gerenciado: `C:\dev\helworks\helengine\engine\helengine.capture.windows\helengine.capture.windows.csproj`.
Projeto nativo: `C:\dev\helworks\helengine\engine\helengine.capture.windows.native\helengine.capture.windows.native.vcxproj`.

## Build e validação

Requisitos de build: .NET SDK 9.0.308, Visual Studio com MSVC v143 x64 e Windows SDK 10.0.26100.0.
Execução exige Windows x64 com as APIs Windows Graphics Capture usadas pela biblioteca, D3D11 e
o runtime Visual C++ correspondente ao build nativo. Não há dependência do player, editor ou FFmpeg.
Os testes de integração também usam o Windows Desktop Runtime .NET 9 e uma sessão gráfica interativa.

```powershell
& 'C:\dev\helworks\helengine\scripts\build-window-capture.ps1' -Action Build
& 'C:\dev\helworks\helengine\scripts\build-window-capture.ps1' -Action NativeTests
& 'C:\dev\helworks\helengine\scripts\build-window-capture.ps1' -Action ManagedTests
& 'C:\dev\helworks\helengine\scripts\build-window-capture.ps1' -Action IntegrationTests
& 'C:\dev\helworks\helengine\scripts\build-window-capture.ps1' -Action All
```

O script sobrescreve a saída em `C:\dev\helworks\builds\helengine-capture\windows`.
A DLL nativa é `C:\dev\helworks\builds\helengine-capture\windows\helengine.capture.windows.native.dll`.
A biblioteca C# fica em `C:\dev\helworks\builds\helengine-capture\windows\managed\bin\helengine.capture.windows\release\helengine.capture.windows.dll`,
junto de uma cópia da DLL nativa. Um ProjectReference ao projeto gerenciado leva a DLL nativa ao output
e ao publish do consumidor. O loader não contém caminho absoluto: distribuir as duas DLLs juntas.
`CaptureNativeOutputPath` permite fornecer a DLL já construída em outro ambiente de consumo.

Os testes nativos usam texturas sintéticas. Os testes de integração capturam somente suas próprias
janelas de cores, sem ativar outra aplicação. `-TestFilter 'FullyQualifiedName~NomeDoTeste'` restringe
os testes gerenciados sem mudar a pasta de saída. Logs temporários ficam em `C:\temp\helengine-window-capture`.

## Uso

```csharp
using helengine.capture.windows;

public static class FrameReader {
    public static void ReadFrames(IntPtr windowHandle, Action<CapturedWindowFrame> consume, CancellationToken cancellationToken) {
        using var session = WindowCaptureSession.Open(windowHandle, new WindowCaptureOptions());
        while (!cancellationToken.IsCancellationRequested) {
            try {
                if (!session.TryAcquireFrame(out var frame)) {
                    Thread.Sleep(1);
                    continue;
                }
                using (frame) {
                    consume(frame);
                }
            } catch (WindowCaptureClosedException) {
                break;
            }
        }
    }
}
```

Chamar `Open` fora da thread de UI se a espera pela inicialização não for aceitável. A sessão possui
um worker MTA persistente, sem exigir message loop do consumidor. Serializar aquisição e Dispose de
uma mesma sessão; não descartar um frame durante seu próprio readback ou uso dos ponteiros emprestados.

`Open(windowHandle, deviceHandle, options)` utiliza um ID3D11Device válido fornecido pelo consumidor
e retém sua própria referência COM. O device precisa permitir uso multithread. Para composição no mesmo
device, usar esse overload; não é feita transferência implícita de texturas entre devices.

Cada frame expõe `Width`, `Height`, `Format`, `Timestamp`, `SequenceNumber`, `TextureHandle` e
`DeviceHandle`. Os ponteiros são emprestados e só são válidos enquanto o frame estiver vivo.
Não chamar Release nesses ponteiros. Finalizar a submissão dos comandos GPU antes de Dispose;
para reter pixels independentemente do frame, copiar o conteúdo para uma textura própria.

`CopyPixels()` retorna um `byte[]` BGRA8/SDR com linhas compactadas, `Width * 4` bytes por linha,
sem padding. Esse array permanece válido após Dispose do frame. A superfície capturada segue os limites
fornecidos pelo Windows para aquele HWND; não pressupor equivalência ao client rect Win32.

## Cadência, memória e término

- Timestamp preserva o SystemRelativeTime original, baseado em QPC e expresso em TimeSpan.
- A biblioteca entrega novos frames na cadência do sistema, sem duplicar frames, impor FPS ou inventar duração.
- A capacidade padrão é três, contando os frames na fila e os que o consumidor ainda não descartou.
- Se a capacidade estiver ocupada, o frame novo é descartado; `OverflowDiscardedFrames` registra esse caso.
- A fila mantém a ordem de chegada. `SequenceNumber` avança também nos descartes conhecidos.
- `Statistics` distingue observações, entregas e descartes por overflow, redimensionamento ou encerramento.
- Esses contadores não medem frames que o compositor ou o pool do Windows nunca entregaram à biblioteca.
- Redimensionar pode mudar as dimensões de novos frames. Frames já entregues mantêm seus pixels e dimensões.
- Fechar a janela, falhar a captura ou descartar a sessão limpa os frames pendentes; frames já entregues continuam válidos.
- Após Dispose da sessão, `State` retorna Disposed e `Statistics` preserva o snapshot final.
- `TryAcquireFrame` retorna false somente para ausência temporária. Fechamento lança WindowCaptureClosedException;
  erros nativos lançam WindowCaptureException com Operation/HResult; sessão descartada lança ObjectDisposedException.

Cursor é desligado por padrão. `RemoveBorder=true` pede autorização ao Windows e falha explicitamente
se a autorização for negada ou a API não estiver disponível. O caminho padrão mantém a política de
indicador do sistema. Não há fallback para PrintWindow, BitBlt ou captura do desktop.

Esta etapa disponibiliza frames. Codificação H.265, gravação a 60 FPS, áudio e CLI de gravação ficam
para a integração posterior com a biblioteca de vídeo.
