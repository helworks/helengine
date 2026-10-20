# Gravação de janela em H.265 a 60 FPS

Data: 2026-10-20. Estado: arquitetura e especificação escrita aprovadas por Helena; plano de implementação em revisão.

## Resultado solicitado e escopo aprovado

Gravar uma janela Windows usando a biblioteca própria de captura e os pacotes de vídeo da Helengine,
com saída MP4/H.265, 60 FPS constantes, bitrate alto configurável e uma CLI automatizável.
Helena aprovou o caminho nativo GPU com NVENC e uma primeira entrega somente de vídeo, sem áudio.

O objetivo posterior continua sendo gravar os menus das plataformas atuais. Esta entrega fornece
a gravação de um HWND já existente; não inclui iniciar emuladores, navegar nos menus, localizar a
janela de cada plataforma automaticamente, editar vídeos ou montar a gravação de todas as plataformas.

Não há OBS, Gevo ou HelenUI no caminho de execução. A captura mantém sua responsabilidade atual:
fornecer frames e timestamps. Cadência de saída, encoding, container e controle de gravação ficam
nas camadas de vídeo e gravação.

## Evidência existente

- `C:\dev\helworks\helengine\engine\helengine.capture.windows`: entrega texturas BGRA8/SDR
  independentes por HWND, timestamps SystemRelativeTime/QPC, limites de buffers e fechamento explícito.
- `C:\dev\helworks\helengine\engine\helengine.video.ffmpeg`: backend nativo FFmpeg já compilado
  com D3D11 e bibliotecas de codec/container; hoje oferece decodificação e probe.
- `C:\dev\helworks\helengine\engine\helengine.directx11.video`: fachada C# existente para esse backend.
- `C:\dev\helworks\helengine\engine\helengine.media.windows\FfmpegCompositionEncoder.cs`:
  encoder de composição existente que envia RGBA pela CPU a um processo FFmpeg.
- `C:\dev\helworks\helengine\engine\helengine.media.windows\FfmpegEncodeArguments.cs` e
  `C:\dev\helworks\helengine\engine\helengine.media\OutputProfile.cs`: perfis atuais de exportação
  de composição H.264 e ProRes, sem um contrato de entrada de texturas para gravação ao vivo.

A máquina consultada tem NVIDIA GeForce RTX 4090. A instalação
`C:\env\ffmpeg-8.1.1-full_build-shared` contém headers, import libraries e DLLs; o encoder
hevc_nvenc listado por ela aceita D3D11. Disponibilidade listada não substitui abrir o codec e
validar a gravação real nesta GPU e driver.

## Divisão entre componentes

1. Estender `C:\dev\helworks\helengine\engine\helengine.video.ffmpeg` com encoder HEVC de
   texturas D3D11 e mux MP4, por ABI C. Usar libavcodec/libavformat diretamente; o processo ffmpeg.exe
   não recebe frames de produção.
2. Estender `C:\dev\helworks\helengine\engine\helengine.directx11.video` com a fachada C# do
   encoder: opções tipadas, inicialização, submissão por índice e finalização determinística.
   O encoder não referencia a biblioteca de captura nem conhece HWNDs.
3. Adicionar `C:\dev\helworks\helengine\engine\helengine.recording.windows`: conecta captura,
   relógio, seleção dos frames, encoder e relatório de gravação. Depende das duas fachadas existentes,
   sem exigir editor, player, cenas, compositor ou mixer.
4. Adicionar `C:\dev\helworks\helengine\tools\window-recorder`: CLI .NET 9 x64 para listar
   janelas e gravar uma delas. Lógica de gravação fica na biblioteca; a CLI valida argumentos,
   apresenta metadados e encaminha parada.

Os perfis de exportação de composição existentes continuam pertencendo ao seu fluxo. A nova entrada
de encoding é reutilizável por outros produtores de texturas, sem exigir um documento de composição
fictício para gravar uma janela.

## Perfil inicial de vídeo

| Parâmetro | Contrato proposto |
| --- | --- |
| Container / identificação | MP4 com vídeo HEVC/H.265, sample entry hvc1, faststart após finalização. |
| Hardware | hevc_nvenc obrigatório; sem fallback automático para CPU ou outro codec. |
| Cadência | 60/1 FPS; time base do encoder 1/60; PTS do frame k igual a k. |
| Pixels | HEVC Main, 8 bits, 4:2:0, opaco, SDR. |
| Cor | BGRA RGB full-range para YCbCr BT.709 limited-range na GPU; sinalização BT.709 consistente e conferida em decode. |
| Bitrate | VBR, alvo 40.000.000 bit/s e máximo 60.000.000 bit/s; ambos configuráveis e positivos, máximo >= alvo. |
| Qualidade / buffering | Preset p5, tune hq, GOP 120, sem lookahead e sem B-frames nesta primeira versão. |
| Resolução | Conteúdo do primeiro frame, sem escala; padding preto de no máximo um pixel à direita/embaixo para dimensões pares. |
| Áudio | Ausente. |

VBR não promete que um menu estático consumirá exatamente 40 Mbit/s: os valores configuram o
controle do encoder. Não prometer qualidade, bitrate médio efetivo ou desempenho sustentado antes
de medir. Opções inválidas ou não aceitas pelo codec falham com diagnóstico; não ignorar opções.

A resolução de entrada fica fixa no primeiro frame. Se a janela mudar de tamanho, encerrar a
gravação, finalizar o trecho já aceito quando possível e retornar motivo de redimensionamento com
status de falha. Não esticar, recortar nem reconfigurar silenciosamente um stream MP4 em andamento.

## Device, texturas e posse

O gravador cria um device D3D11 no adaptador NVIDIA selecionado e abre captura e encoder nesse mesmo
device. A seleção padrão usa o primeiro adaptador NVIDIA de hardware compatível encontrado, registra
nome/LUID e confirma abertura de hevc_nvenc. Falha de suporte é explícita; não tentar outro tipo de
encoder. O contrato da biblioteca permite receber um device válido do host.

O backend retém sua própria referência ao device e usa superfícies D3D11 de encoding próprias.
Submissão recebe uma textura emprestada do mesmo device, formato e dimensões validados. Copiar o
conteúdo e completar a submissão dos comandos necessários antes de devolver o empréstimo ao chamador.
Uma referência COM adicional não substitui o contrato de posse do frame da captura.

O frame da captura pode ser descartado depois da submissão; o encoder continua possuindo todos os
recursos necessários até que o hardware e o mux tenham terminado de usá-los. Não guardar um ponteiro
emprestado cuja textura possa ser liberada ou reutilizada pelo produtor.

A captura usa orçamento explícito de três frames. O gravador retém no máximo dois frames de captura
durante a seleção temporal. O encoder usa no máximo oito superfícies GPU de entrada em voo, com
backpressure; nenhuma fila ou pool cresce conforme a duração do vídeo ou a quantidade de resoluções.
As superfícies antigas não são acumuladas. Não chamar CopyPixels, ReadRgba, Map/readback ou pipe de
pixels CPU no caminho de produção. Readback continua permitido somente para validação explícita.

Sincronizar o contexto imediato usado pela captura, cópias e FFmpeg/NVENC, sem esperar resultados
do hardware segurando locks necessários aos callbacks. Isolar contexto do encoder e diagnósticos
por instância; exceções não atravessam a ABI. Finalização e liberação são idempotentes nas fachadas.

## Relógio e seleção a 60 FPS

1. Abrir captura e obter o primeiro frame com timeout de dez segundos para determinar dimensões.
2. Inicializar o encoder e verificar suporte antes de começar a duração solicitada. Drenar a fila
   disponível para obter conteúdo recente; a inicialização do codec não vira um congelamento inicial
   acrescentado ao vídeo.
3. Fixar a origem da gravação no QPC do host depois dessa preparação. Usar o último frame disponível
   com timestamp <= origem para o slot zero; aguardar um frame válido se ainda não existir esse caso.
4. O slot k representa origem + k/60 s. Comparar timestamps e calcular deadlines usando aritmética
   inteira/racional, sem somar sucessivos TimeSpan arredondados de 1/60 s.
5. Para cada slot, escolher o frame capturado mais recente cujo timestamp não ultrapasse o instante
   desse slot. Um frame futuro fica retido para um slot posterior, sem antecipar conteúdo.
6. Se não há atualização elegível, repetir o conteúdo anterior. Se a origem atualiza acima de 60 FPS,
   descartar entradas intermediárias explicitamente e contabilizá-las. Não fazer interpolação.
7. Submeter exatamente uma imagem por slot, com PTS crescente de uma unidade e duração de 1/60 s.

O mux pode negociar outra time base para o stream MP4. Reescalar os timestamps dos packets para essa
base preservando os intervalos exatos de 1/60 s; não reutilizar os inteiros PTS do codec como se a
time base do stream fosse necessariamente a mesma.

O arquivo tem 60 FPS constantes mesmo quando a janela produz somente 30 atualizações por segundo;
nesse caso contém repetições. Os timestamps da captura continuam sendo usados para escolha temporal,
não são reescritos nem anunciados como se a aplicação tivesse produzido 60 frames distintos.

Uma duração solicitada D > 0 produz ceil(D * 60) slots; a duração codificada é frame_count/60 e pode
exceder D em menos de um frame. O primeiro frame começa em PTS zero. Sem duração, gravar até parada
solicitada ou fechamento da janela. A duração real de gravação exclui preparação e finalização.

Não manter backlog ilimitado quando o encoder demora. Um atraso de saída acima de 250 ms encerra
explicitamente com motivo EncoderTooSlow; não acelerar o relógio do vídeo, anunciar FPS sustentado
nem prolongar silenciosamente a gravação para compensar uma máquina que não acompanhou o alvo.
Esse limite é uma política do gravador, separada da disponibilidade momentânea de novos frames de
uma janela estática. Reportar atraso máximo e repetições para tornar a distinção observável.

## Parada, arquivo e falhas

Duração atingida, pedido de parada e fechamento da janela são encerramentos normais. Ctrl+C faz
parada graciosa: parar de aceitar novos slots, drenar o encoder, escrever o trailer e finalizar o MP4.
Não matar o processo de encoding como o mecanismo normal de parada. Dispose sem finalização é
aborto e limpeza; não deve publicar um arquivo como se estivesse completo.
Parada ou fechamento antes de obter qualquer frame não publica um MP4 vazio: reportar NoFrames,
ausência de arquivo concluído e código de falha 3.

Na mudança de tamanho ou atraso excessivo, tentar concluir apenas os frames já aceitos pelo encoder,
salvar um MP4 válido se a finalização funcionar e retornar relatório de falha com o motivo. Isso não
é sucesso de uma gravação de duração maior. Se o encoder ou o mux falha de modo que não possa concluir,
retornar o erro original e não publicar arquivo incompleto como resultado válido.

Todo arquivo de trabalho fica sob `C:\temp\helengine-window-recording`. Instâncias concorrentes
possuem nomes de arquivo temporários exclusivos dentro dessa raiz; não criar históricos de builds.
O destino final é explicitamente fornecido pelo host e deve estar ausente: a primeira versão não
sobrescreve gravações existentes. Reservar/criar o destino sem corrida após validação e finalização;
limpar somente arquivos efetivamente criados por esta sessão em caso de falha de publicação.

No mesmo volume, usar transferência/rename sem sobrescrita quando aplicável. Em outro volume,
copiar o resultado finalizado para um destino criado exclusivamente e apagar essa cópia própria se
a cópia falhar. Não prometer publicação atômica entre volumes. Remover o arquivo de trabalho depois
da transferência bem-sucedida. Nunca apagar ou truncar um vídeo preexistente.

Preservar HRESULT/mensagem ou código e texto de FFmpeg por operação. Preflight deve identificar
HWND inválido, path inválido/existente, codec indisponível, GPU/device incompatível, opções inválidas
e falta de APIs. Falhas de acesso ou suporte não mudam a origem da captura nem fazem fallback para
outro codec, CPU, PrintWindow, BitBlt ou desktop.

## CLI e resultado observável

Comandos propostos da ferramenta:

```text
helengine.window-recorder windows
helengine.window-recorder record --hwnd 0x12345 --output C:\videos\menu.mp4 --duration 30 --fps 60 --bitrate 40M --max-bitrate 60M
```

O exemplo C:\videos\menu.mp4 é apenas um destino fornecido pelo usuário; a implementação não cria
gravação real nesse caminho durante testes. --duration é opcional; --fps aceita 60 nesta entrega;
valores numéricos usam parsing invariável. HWND aceita decimal ou hexadecimal. A listagem traz HWND,
PID, nome do processo e título das janelas visíveis de topo, sem capturar pixels nem escolher a
primeira janela por heurística de título. O record exige HWND explícito.

Stdout contém um documento JSON final para automação; diagnósticos e progresso limitado vão para
stderr. O comando windows retorna uma lista JSON; record retorna o relatório de uma sessão.
Registrar: caminho final, motivo de término, duração solicitada/real/codificada, dimensões
da origem e do vídeo, codec/encoder, adaptador, taxa 60/1, bitrate alvo/máximo, frames codificados,
repetidos, descartados pela seleção temporal e descartes conhecidos da captura, além de atraso máximo.
Os campos são contadores da biblioteca; não prometer medir frames que o sistema não entregou.

Retornos: 0 para término normal com arquivo concluído; 2 para argumentos inválidos; 3 para falha
de captura/encoding, resize, atraso ou publicação. O JSON informa se um arquivo foi concluído mesmo
num encerramento com código 3. Erro não vira um relatório de sucesso com caminho vazio.

## Validação necessária

- Relógio determinístico: origem 30/60/120 FPS, janela estática, timestamps fora de ordem rejeitados,
  frame futuro não antecipado, duração fracionária, repetição e descarte com contadores coerentes.
- Native encoder: texturas sintéticas, validação de device/formato/dimensões, índice fora de ordem,
  drain completo, falha de abertura/opções, capacidade limitada e posse após liberação do produtor.
- Arquivo real: ffprobe confirma hevc, hvc1, Main/8 bits/4:2:0, 60/1, ausência de áudio, PTS e contagem.
  Decodificar frames de padrão conhecido para verificar cor, geometria, padding e ausência de conteúdo velho.
- Integração: janela sintética própria em movimento a 1920x1080 por dez segundos, alvo de 600 frames,
  duração codificada de dez segundos, cadência medida, repetições e atrasos reportados. Não anunciar
  suporte sustentado a 60 FPS em resoluções que não tenham sido medidas.
- Encerramento por tempo, pedido de parada, janela fechada, resize, encoder lento induzido e erro de
  destino existente: arquivo corretamente concluído ou ausência de publicação inválida, com retorno correto.
- CLI: parsing, caminhos Unicode/com espaços, seleção HWND, stdout JSON separado de stderr e saída
  de erro diferente de zero. Testar cópia entre volumes pela abstração de publicação, sem depender
  da existência de um segundo disco no ambiente de testes.
- Regressão proporcional: suíte da captura e contratos existentes de decode/probe afetados pelo build
  da DLL compartilhada. Limitações de hardware ou sessão gráfica são reportadas, não tratadas como aprovação.

Testes capturam somente suas próprias janelas. Nenhuma gravação do desktop ou de aplicações reais
é iniciada nesta etapa de especificação. Depois da implementação validada, a CLI estará disponível
para gravar a janela que Helena selecionar.

## Checkout, build e dependências

Trabalhar na main do checkout real, sem worktrees ou exports; preservar os arquivos pendentes
preexistentes. Respeitar uma classe por arquivo, documentação XML substantiva e os padrões C# locais.

Manter a saída já estabelecida em `C:\dev\helworks\builds\helengine-capture\windows`, sobrescrevendo
os artefatos anteriores correspondentes, sem nova pasta por data/tarefa/tentativa. O build da ferramenta
inclui as DLLs de captura, encoder e dependências FFmpeg compatíveis, com resolução relativa ao pacote,
sem caminho pessoal embutido no runtime. Usar o SDK FFmpeg compatível em headers, import libs e DLLs.

Logs, scripts descartáveis, fixtures em disco, vídeos de teste e demais temporários ficam somente
em `C:\temp\helengine-window-recording` e são limpos ao concluir a execução que os criou.

## Referências técnicas

- FFmpeg, implementação NVENC e formatos de entrada D3D11:
  https://www.ffmpeg.org/doxygen/trunk/nvenc_8c_source.html
- NVIDIA, FFmpeg com NVENC/HEVC:
  https://docs.nvidia.com/video-technologies/video-codec-sdk/13.1/ffmpeg-with-nvidia-gpu/index.html
- Contratos D3D11 locais do SDK consultado:
  `C:\env\ffmpeg-8.1.1-full_build-shared\include\libavutil\hwcontext_d3d11va.h`.

## Handoff

A especificação escrita foi aprovada por Helena com “vai” em 2026-10-20. O plano está em
`C:\dev\helworks\helengine\docs\superpowers\plans\2026-10-20-window-hevc-recording.md`;
revisar esse plano antes de implementar o encoder e a ferramenta.
