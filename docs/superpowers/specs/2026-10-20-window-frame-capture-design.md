# Captura de frames de janelas no Windows

Data: 2026-10-20. Estado: especificação aprovada por Helena; implementação ainda não iniciada.

## Objetivo e decisões aprovadas

Criar uma biblioteca própria da Helengine para acessar os frames de uma janela do Windows.
Reaproveitar a implementação de captura já estabelecida, sem depender dos aplicativos Gevo ou HelenUI.
Helena aprovou um backend C++/WinRT, uma biblioteca C# e frames mantidos na GPU.

Esta é a primeira etapa. A futura gravação dos menus de todas as plataformas usará estes frames e
a biblioteca de vídeo para produzir H.265 a 60 FPS com bitrate alto. Essa gravação, a CLI, o áudio,
os encoders, a seleção de emuladores e a integração com `helengine.video` não fazem parte desta entrega.
Capturar a janela de um emulador continua sendo uma operação do host Windows; esta entrega não adiciona
backends de captura aos consoles emulados.

## Base existente

Fontes consultadas diretamente, inclusive alterações locais presentes nos repositórios:

- `F:\dev\gevo\helengine\native\GevoAeroCaptureSession.hpp`: criação da captura por HWND,
  pool livre de DispatcherQueue, identificação de fechamento e liberação dos objetos WinRT.
- `F:\dev\gevo\helengine\native\GevoAeroCaptureWorker.hpp`: inicialização COM em MTA persistente
  e propagação das falhas de inicialização.
- `F:\dev\gevo\helengine\native\GevoAeroCaptureSource.hpp`: aquisição das texturas, conferência
  de ContentSize e recriação do pool após mudança de tamanho.
- `F:\dev\gevo\helengine\native\GevoAeroCaptureBorderPolicy.hpp`: configuração explícita da
  borda de captura e tratamento da autorização do Windows.
- `C:\dev\helenui\plugins\screenshot-cli\src\ScreenshotCli\Capture\WindowsGraphicsCaptureBackendSession.cs`:
  referência para leitura de pixels respeitando RowPitch. Seu caminho de imagens na CPU não será o
  caminho normal da nova biblioteca.
- `C:\dev\helworks\helengine\engine\helengine.directx11.video\VideoFrame.cs` e
  `C:\dev\helworks\helengine\engine\helengine.video.ffmpeg\helengine.video.ffmpeg.vcxproj`:
  precedentes locais para recursos nativos expostos a C# e backend Windows em DLL.

Extrair apenas o mecanismo de captura. Não transportar código de miniaturas, redução para 720x480,
ícones, animação, composição de planos ou dependência de MonoGame. Não modificar os repositórios de origem.

## Organização e dependências

Projetos de produção:

- `C:\dev\helworks\helengine\engine\helengine.capture.windows`: biblioteca C# net9.0,
  com nullable desabilitado, documentação XML substantiva e uma classe por arquivo.
- `C:\dev\helworks\helengine\engine\helengine.capture.windows.native`: DLL C++17/WinRT x64,
  construída com o toolchain MSVC e Windows SDK já usados no checkout.

A API gerenciada usa P/Invoke e handles seguros para possuir sessões e frames. A ABI C utiliza
estruturas com tamanho/versão explícitos, inteiros de largura definida, handles opacos e códigos de erro;
não exporta objetos C++, exceções ou tipos WinRT. Erros nativos incluem HRESULT e mensagem associados à
operação, sem usar uma mensagem global compartilhada entre threads.

O backend depende das APIs Windows Graphics Capture e Direct3D 11. A fachada não exige referência ao
editor, ao player, a `helengine.video`, a FFmpeg, a ImageSharp ou aos aplicativos de origem.
O primeiro alvo é Windows x64 com suporte à captura por HWND; consultar a disponibilidade das APIs
usadas e rejeitar explicitamente sistemas incompatíveis, sem escolher outro método de captura.

## Contrato público proposto

Tipos e responsabilidades:

| Tipo | Responsabilidade |
| --- | --- |
| `WindowCaptureSession` | Abrir uma janela por HWND, adquirir frames e encerrar a captura. |
| `WindowCaptureOptions` | Capacidade de buffers, captura do cursor e política da borda. |
| `CapturedWindowFrame` | Possuir uma textura e seus metadados até Dispose; permitir readback explícito. |
| `WindowCaptureState` | Distinguir Running, WindowClosed, Faulted e Disposed. |
| `WindowCaptureStatistics` | Expor frames observados, entregues e descartados pela própria biblioteca. |

Operações principais:

- `WindowCaptureSession.Open(hwnd, options)` cria um device D3D11 próprio com suporte a BGRA.
- Uma sobrecarga recebe um ponteiro ID3D11Device existente, para consumidores que precisam usar o mesmo
  device. A biblioteca valida o ponteiro e retém sua própria referência COM; não toma posse da referência
  original do chamador. Não há suporte implícito à transferência de texturas entre devices diferentes.
- `TryAcquireFrame(out frame)` é não bloqueante e entrega o próximo frame da fila, uma única vez.
  Retorna false somente quando não existe frame disponível e a sessão continua em execução.
  Fechamento e falhas são resultados explícitos, identificáveis pelo estado e por exceções específicas;
  não são confundidos com a ausência temporária de frames.
- `CapturedWindowFrame.CopyPixels()` faz readback explícito e retorna um buffer independente de bytes
  BGRA8, com linhas compactadas de `Width * 4` bytes. O buffer permanece válido após Dispose do frame.
- Dispose é idempotente tanto para a sessão quanto para o frame.

Cada frame expõe largura, altura, formato BGRA8, timestamp original em TimeSpan, número de sequência
e ponteiro ID3D11Texture2D emprestado, válido até Dispose do frame. O ponteiro do device correspondente
também fica disponível como empréstimo enquanto o frame estiver vivo.
Nenhuma operação de captura pressupõe um Core, uma cena ou um render loop da Helengine.

O chamador deve serializar aquisição e encerramento de uma mesma sessão. O backend sincroniza seus
callbacks e o acesso ao contexto D3D11. Um frame não permite readback concorrente com seu próprio Dispose.
Quem usa o ponteiro emprestado não chama Release nele e deve concluir a submissão de seus comandos antes
de liberar o frame. AddRef não prolonga o empréstimo: a biblioteca pode reciclar a textura após Dispose.
Quem precisa de uma cópia independente deve copiar o conteúdo para sua própria textura antes desse limite.

## Fluxo e posse dos recursos

1. Validar o HWND, suas opções e o suporte às APIs necessárias. A identidade inicial é a janela
   explicitamente recebida; não procurar outra janela por título nem reabrir automaticamente um HWND.
2. Inicializar os objetos WinRT em worker MTA e criar uma sessão persistente com
   Direct3D11CaptureFramePool.CreateFreeThreaded. Open aguarda o resultado da inicialização e deve
   ser chamado fora da thread de UI quando o aplicativo não puder aguardar.
3. Ao chegar um frame, obter sua textura, ContentSize e SystemRelativeTime. Conferir as dimensões
   antes de copiar a região válida para uma textura própria da biblioteca, sem escala ou conversão.
4. Fechar o frame original do Windows antes de recriar o pool. A textura entregue não depende da
   validade de uma superfície devolvida ao pool do Windows.
5. Enfileirar a textura e os metadados. TryAcquireFrame transfere a posse desse frame para o chamador.
6. Dispose devolve a capacidade ocupada pelo frame e libera ou recicla sua textura.

A fila e os frames emprestados compartilham um orçamento de buffers, padrão de três, configurável
por BufferCapacity com valor positivo. A fila entrega os frames na ordem de chegada. O pool do Windows
é separado e também tem tamanho fixo. Cada sessão possui seu próprio worker MTA; não transportar a
fila global nem o limite de cinco miniaturas do Gevo.
Se o orçamento estiver ocupado, descartar o frame novo e incrementar o contador de overflow;
não bloquear o callback, criar fila ilimitada ou sobrescrever uma textura ainda em uso.
O número de sequência avança em cada frame observado no pool, inclusive quando descartado pela biblioteca.

Esses contadores descrevem apenas o que a biblioteca observou e descartou; não medem perdas internas
do compositor ou do pool do Windows. Não prometer captura de todos os frames produzidos pela aplicação.

Frames já entregues continuam válidos quando a janela fecha ou a sessão é descartada. Eles retêm
as referências necessárias ao device e à textura. Encerrar a sessão libera os frames ainda na fila,
remove callbacks e encerra os recursos de captura sem invalidar os frames em posse do chamador.
Não se espera pelo consumidor para encerrar a sessão.

O device próprio possui proteção multithread do contexto. O device fornecido pelo chamador também
precisa permitir essa proteção; rejeitar um device criado com restrições incompatíveis. A sincronização
interna protege cópia/readback e teardown, sem manter locks ao chamar código do consumidor.
Falhas em callbacks são armazenadas como falha terminal da sessão e propagadas na próxima aquisição.
Fechamento ou falha terminal descarta os frames ainda enfileirados e torna o término imediatamente
observável; não entrega conteúdo antigo após o término. Estatísticas distinguem descartes por overflow,
redimensionamento e encerramento. Frames já entregues continuam seguindo seu contrato de posse.

## Pixels, relógio e alterações da janela

- Preservar o conteúdo fornecido pelo Windows Graphics Capture para o HWND, sem cortes adicionais,
  bordas acrescentadas, overlays ou redimensionamento. Não afirmar que os limites recebidos equivalem
  ao client rect Win32; documentar que a região é a superfície de captura fornecida pelo sistema.
- Esta primeira versão usa BGRA8/SDR. Não implementa HDR ou tone mapping e não promete fidelidade HDR.
- Cursor desligado por padrão, seguindo a implementação de origem; pode ser habilitado por opção.
- Usar SystemRelativeTime como timestamp original QPC expresso em unidades de TimeSpan. Não substituir
  por DateTime, relógio do callback ou incremento artificial de 1/60 s. Não atribuir duração inventada
  a um frame: ela será determinada pelo consumidor a partir dos timestamps e de sua política de saída.
- Capturar na cadência entregue pelo sistema. Ausência de novos frames não significa falha nem autoriza
  devolver o último frame como se fosse novo. A captura não impõe nem promete 60 FPS.
- Em mudança de tamanho, respeitar ContentSize e os limites reais da textura. Se a superfície não
  comportar o novo tamanho, liberar o frame, recriar o pool e aguardar outro; registrar o descarte
  conhecido. Nenhum frame entrega pixels fora da região válida.
- Novos frames podem ter dimensões diferentes dos anteriores. Frames já entregues preservam dimensões
  e pixels. Slots livres com tamanho antigo são liberados antes da substituição; os antigos ainda
  emprestados continuam contando no mesmo orçamento, sem criar uma reserva ilimitada por resolução.
- Janela minimizada ou temporariamente sem atualizações pode não produzir frames. Não gerar imagens
  pretas de substituição e não trocar a origem por uma captura do desktop.
- No readback, respeitar RowPitch, validar dimensões e overflow do tamanho do buffer e copiar somente
  os pixels válidos. Esse caminho pode sincronizar CPU/GPU e nunca é chamado implicitamente na captura.

## Borda e falhas

A política padrão mantém o indicador de captura controlado pelo Windows. A opção explícita de remover
a borda reaproveita o mecanismo de autorização da origem; negação ou API indisponível produz erro,
sem fingir que a opção foi aplicada. A autorização do sistema, quando necessária, não é omitida.

HWND inválido, captura recusada, API indisponível, erro de device e falhas de alocação permanecem
identificáveis. Não usar PrintWindow, BitBlt ou captura da tela como fallback.
Inicialização parcial desfaz seus próprios recursos. Dispose remove inscrições e impede publicação
posterior de frames. Um handle seguro gerenciado preserva a liberação de recursos caso o consumidor
esqueça Dispose; isso não substitui o contrato de liberação determinística.

## Validação e critérios de aceite

Adicionar testes em `C:\dev\helworks\helengine\engine\helengine.capture.windows.tests` e um alvo
de integração nativo somente se necessário para testar a ABI e o gerenciamento de recursos.
Não disparar gravações ou capturas de aplicativos reais nesta etapa: a integração abre apenas uma
janela sintética pertencente ao próprio teste, com padrão de pixels conhecido.

Critérios:

1. Capturar e ler pixels da janela sintética confirma BGRA, dimensões e ausência de escala.
2. Repetir aquisições confirma timestamps preservados e ausência de entrega duplicada de um frame.
3. Redimensionar confirma recriação correta e validade de frames antigos ainda em posse do teste.
4. Fechar a janela e descartar a sessão confirmam término explícito e validade dos frames emprestados.
5. Saturar o orçamento confirma limite de buffers e contador explícito de descartes.
6. HWND inválido, falha nativa induzida e recursos parcialmente inicializados confirmam erro e limpeza.
7. Dispose repetido não libera o mesmo recurso duas vezes; acesso a frame descartado falha claramente.
8. Criar a sessão sem engine/player e com um device fornecido confirma os dois modos de uso.

Testes de Windows Graphics Capture exigem Windows com sessão gráfica interativa e APIs disponíveis.
Caso o ambiente não ofereça essas condições, reportar a limitação; testes de contratos não substituem
a validação real de captura. Medições de desempenho, cadência ou memória só serão relatadas após medição.

O build usa sempre `C:\dev\helworks\builds\helengine-capture\windows`, sobrescrevendo a saída anterior.
Scripts descartáveis, logs e capturas temporárias ficam somente em `C:\temp` e são removidos ao concluir.
Trabalhar no checkout real, na main, preservando mudanças preexistentes e sem worktrees.

## Referências técnicas

- Microsoft, Screen capture: https://learn.microsoft.com/en-us/windows/uwp/audio-video-camera/screen-capture
- Microsoft, Direct3D11CaptureFrame.SystemRelativeTime:
  https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture.direct3d11captureframe.systemrelativetime
- Microsoft, CreateFreeThreaded:
  https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture.direct3d11captureframepool.createfreethreaded

## Próximo passo

Helena revisa esta especificação. Após aprovação, preparar o plano de implementação e sua validação,
respeitando o escopo da biblioteca de frames e as regras do checkout compartilhado.
