# Windows window recording

Grava um HWND explícito usando Windows Graphics Capture e o encoder de texturas GPU da Helengine.
Pacote: `C:\dev\helworks\helengine\engine\helengine.recording.windows`.

Requer Windows com suporte a WGC/D3D11, .NET 9 x64 e adaptador/driver NVIDIA compatível com
`hevc_nvenc` do FFmpeg distribuído. A abertura real do codec confirma suporte; não existe fallback
para CPU, outro codec, outra janela ou desktop. Não usa OBS, Gevo ou HelenUI.

```csharp
using helengine.recording.windows;

using var stop = new CancellationTokenSource();
var recorder = new WindowRecorder();
var result = await recorder.RecordAsync(new WindowRecordingOptions {
    WindowHandle = selectedHwnd,
    OutputPath = @"C:\videos\menu.mp4",
    Duration = TimeSpan.FromSeconds(30),
    Bitrate = 40_000_000,
    MaxBitrate = 60_000_000
}, stop.Token);
```

O diretório de destino deve existir e o arquivo deve estar ausente. O overload com device aceita
um `IntPtr` válido de ID3D11Device NVIDIA fornecido pelo host, retendo uma referência própria.
Sem device fornecido, a biblioteca seleciona o primeiro adaptador NVIDIA de hardware e informa
nome/LUID. A captura e o encoder usam o mesmo device.

O perfil é MP4/hvc1 com faststart, HEVC Main 8-bit 4:2:0, SDR BT.709 limited, 60/1 FPS,
VBR alvo 40/máximo 60 Mbit/s, p5/hq, GOP 120, sem B-frames/lookahead e sem áudio. Bitrates são
configuráveis; um menu estático pode consumir muito menos que o alvo. A conversão de BGRA full
para NV12 ocorre na GPU. Não há readback nem processo FFmpeg recebendo pixels em produção.

A duração começa depois da preparação do codec e da atualização do frame inicial. Cada slot
usa a observação mais recente que não está no futuro; fontes mais lentas ou estáticas geram
repetições, fontes mais rápidas podem gerar descartes intermediários. Uma duração D produz
ceil(D*60) frames, com PTS consecutivo a partir de zero. A duração codificada exata é N/60;
o campo TimeSpan do relatório usa precisão de 100ns. Uma janela que produz 30 atualizações/s
continua produzindo vídeo a 60 FPS, com repetições informadas.

A resolução fica fixa. Dimensões ímpares recebem no máximo um pixel preto à direita/embaixo,
sem escala; em vídeo4:2:0 o chroma é compartilhado com os pixels vizinhos. Resize e atraso acima
de 250 ms encerram com falha, tentando finalizar o prefixo já aceito. O pool do encoder tem limite
de oito superfícies NV12 e uma superfície fixa BGRA para cópia/conversão; captura usa budget 3 e o
seletor retém no máximo dois leases. Nenhuma dessas estruturas cresce com a duração.

CancellationToken significa parada graciosa: a biblioteca para de aceitar slots, drena o codec
e escreve o trailer. Fechamento da janela depois de ao menos um frame também conclui normalmente.
Stop/fechamento antes de qualquer saída e timeout inicial de dez segundos retornam NoFrames,
sem MP4 vazio. Falhas preservam mensagem e código nativo; erros de cleanup ficam separados.

`HasOutput` indica arquivo válido publicado. `Succeeded` exige encerramento normal, portanto
um resultado Resized ou EncoderTooSlow pode ter HasOutput=true e Succeeded=false. O relatório
inclui durações, dimensões, adapter, perfil, bitrates, frames/repetições/descartes, estatísticas
da captura e atraso máximo. Argumentos inválidos lançam ArgumentException antes do trabalho.

Arquivos de trabalho exclusivos ficam em `C:\temp\helengine-window-recording` e são removidos
ao terminar. A publicação só ocorre após drain/trailer: rename sem sobrescrita no mesmo volume,
ou CreateNew/cópia entre volumes, removendo somente a cópia criada pela sessão se falhar.
Não há garantia de publicação atômica entre volumes; arquivos preexistentes são preservados.

Build e testes:

```powershell
powershell -NoProfile -File C:\dev\helworks\helengine\scripts\build-window-recording.ps1 -Action Build
powershell -NoProfile -File C:\dev\helworks\helengine\scripts\build-window-recording.ps1 -Action All
```

O build usa .NET SDK 9.0.308, MSVC v143 e headers/import libs/DLLs compatíveis do SDK FFmpeg.
O parâmetro `-FFmpegSdkRoot` seleciona o SDK; default local `C:\env\ffmpeg-8.1.1-full_build-shared`.
Saída fixa: `C:\dev\helworks\builds\helengine-capture\windows`. Testes gravam somente suas
próprias janelas; ffprobe/ffmpeg.exe e readback aparecem apenas na validação, nunca no pipeline.

Esta entrega captura janelas Windows, incluindo janelas de emuladores. Não inicia plataformas,
navega menus ou anuncia captura nativa em outros sistemas operacionais.

Validação nesta máquina (RTX 4090): 1920x1080 por dez segundos, 600 frames codificados,
atraso máximo de 17,6 ms na execução final. A fixture gerou 220 repetições; isso não promete
60 atualizações distintas da aplicação. All passou em 92/92 testes sem skips. Outras resoluções
e combinações de driver/GPU não foram medidas nesta entrega.
