# Window recorder CLI

Executável: `C:\dev\helworks\builds\helengine-capture\windows\recorder\helengine.window-recorder.exe`.
Código: `C:\dev\helworks\helengine\tools\window-recorder`.

Compilar no checkout real com:

```powershell
powershell -NoProfile -File C:\dev\helworks\helengine\scripts\build-window-recording.ps1 -Action Build
```

Listar metadados das janelas visíveis de topo, sem capturar pixels:

```powershell
& C:\dev\helworks\builds\helengine-capture\windows\recorder\helengine.window-recorder.exe windows
```

Gravar o HWND escolhido explicitamente, substituindo o handle do exemplo:

```powershell
& C:\dev\helworks\builds\helengine-capture\windows\recorder\helengine.window-recorder.exe record --hwnd 0x12345 --output "C:\videos\menu.mp4" --duration 30 --fps 60 --bitrate 40M --max-bitrate 60M
```

O diretório de destino deve existir; o arquivo deve estar ausente. HWND aceita hexadecimal ou
decimal. `--duration` é opcional; sem ela grava até Ctrl+C ou fechamento da janela. Ctrl+C pede
parada graciosa e aguarda a finalização do MP4. `--fps` aceita apenas60, também o padrão.
Bitrates padrão40M/60M usam M=1.000.000 bit/s; máximo deve ser >=alvo. Números usam ponto
decimal mesmo em pt-BR. Caminhos Unicode e com espaços são aceitos como um argumento.

Saída MP4/hvc1 H.265 Main 8-bit420 SDR BT.709, vídeo60FPS constantes com repetições quando
a fonte atualiza mais devagar, sem áudio. NVENC NVIDIA é obrigatório; nenhuma opção é ignorada
para trocar codec/hardware. Resolução fixa, sem escala; tamanho ímpar recebe padding preto.

Stdout contém exatamente um documento JSON em camelCase: array de janelas em `windows`,
relatório final em `record`. Diagnósticos vão para stderr. Campos incluem `hasOutput`,
`succeeded`, `outputPath`, `stopReason`, `errorCode`, `errorMessage`, durações, dimensões,
adaptador/LUID, fpsNumerator/fpsDenominator, bitrates, encodedFrames, repeatedFrames,
resampledSourceDiscards, captureStatistics e maxLag.

| Exit | Resultado |
| --- | --- |
| 0 | Listagem concluída ou gravação normal com arquivo finalizado. |
| 2 | Argumentos inválidos. |
| 3 | Falha operacional, resize, atraso >250ms, codec ou publicação. |

Exit3 pode acompanhar um MP4 válido encurtado: conferir `hasOutput` e `stopReason`.
Sem frames, não publica MP4 vazio. Destino preexistente permanece intocado.
Temporários exclusivos ficam em `C:\temp\helengine-window-recording`; publicação entre
volumes usa cópia exclusiva e não é atômica. O pacote carrega DLLs relativamente a si mesmo,
incluindo captura, backend e dependências FFmpeg; requer runtime .NET9 x64 e NVIDIA compatível.

Verificar tudo com `powershell -NoProfile -File C:\dev\helworks\helengine\scripts\build-window-recording.ps1 -Action All`.
`NativeTests`, `ManagedTests` e `IntegrationTests` permitem verificações focadas com `-TestFilter`
nos testes xUnit. Testes usam somente janelas sintéticas próprias, inclusive 1920x1080/10s;
o comando não automatiza abertura de emuladores ou navegação dos menus.
