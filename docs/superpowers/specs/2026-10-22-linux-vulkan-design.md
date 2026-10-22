# HelEngine Linux: player C++ com SDL3 e Vulkan

## Decisões aprovadas e objetivo

Helena aprovou um sibling `C:\dev\helworks\helengine-linux`, target `linux`, player C++ nativo consumindo o core gerado, SDL3 para janela/input e Vulkan para renderização. Os perfis são Debug, Release e Profiler. O primeiro marco executável é o menu do DemoDisc; o marco seguinte é validar as 14 cenas de rendering selecionadas no projeto.

O checkout no servidor será `/home/helena/dev/helworks/helengine-linux`. A implementação permanece na `main`, sem worktrees, exports do checkout ou cópias alternativas do projeto. A solicitação Apple está sendo desenhada em paralelo e não altera estas decisões Linux.

## Estado existente verificado

- `C:\dev\helworks\helengine-windows` já integra um builder C# ao player C++ gerado, mas seu host e renderer são Win32/DirectX11. Seu CMake rejeita a seleção Vulkan.
- `C:\dev\helworks\helengine\engine\helengine.vulkan` contém renderer C#; `VulkanContext` e `VulkanSwapchainSurface` exigem surfaces Win32. Serve como referência de comportamento, não como um renderer C++ pronto para Linux.
- O player gerenciado e o executável de build atuais dependem de WinForms; `EditorCliBuildRunner` também instancia DirectX11. Instalar Vulkan no servidor não resolve essa dependência.
- O servidor `helena@192.168.15.12` já tem compilador C++, CMake, Ninja, .NET 9.0.308 e PowerShell. A preparação anterior validou SDKs de 18 plataformas; não validou um player Linux do DemoDisc.

## Fronteiras e responsabilidades

O sibling Linux possui o builder, o host nativo e a implementação Vulkan C++. O engine principal mantém os contratos compartilhados, geração de código, formatos de assets e catálogo de repositórios. Não haverá dependência do sibling Windows para compilar o Linux.

O builder implementa os contratos existentes de plataforma, configura geração para Linux e solicita shaders SPIR-V. Recebe os diretórios de core gerado, scripts gerados e conteúdo cooked pelo pipeline existente. O executor nativo chama CMake/Ninja diretamente, sem shell Windows. Cancelamento ou falha do compilador encerram a build com diagnóstico e código de saída preservados.

O host SDL3 possui janela, eventos, tamanho lógico/em pixels, input, tempo e ciclo de execução. O provider da engine traduz os contratos gerados para os serviços nativos. O renderer possui instance/device Vulkan, filas, swapchain, comandos e recursos GPU. As classes de desenho não consultam diretamente APIs X11 ou Wayland; extensões de instance e criação da surface passam pela SDL3.

Recursos e serviços compartilhados são reaproveitados por contrato. Não será copiado o backend Windows inteiro nem extraído um novo framework desktop antes de haver um consumidor que exija essa mudança.

## Contratos de execução e rendering

O fluxo de frame é: processar eventos SDL, atualizar input e simulação, consumir a fila de render da engine, submeter comandos e apresentar. O renderer não repete atualizações dos componentes. Os pivôs de sprites, transforms e matrizes seguem os contratos atuais da engine.

A implementação começa com render passes e pipelines gráficos convencionais; recursos opcionais de Vulkan são habilitados somente após consulta de capacidades. A seleção registra dispositivo, versão e capacidades usadas. Não há troca silenciosa de API gráfica, nem requisito implícito de extensões de ray tracing.

Recursos de GPU têm ownership explícito. Uploads, descriptor sets e recursos de frame só são reutilizados ou destruídos quando a execução GPU correspondente terminar. Resize e swapchain out-of-date provocam reconstrução dos recursos dependentes da surface. Janela minimizada com extensão zero não cria swapchain inválida nem mantém um busy loop.

O primeiro marco cobre o necessário ao menu: sprites e pivôs, atlas de fontes, texto, clipping, transparência, ordem de desenho e apresentação. O marco 3D acrescenta meshes, câmera, depth/culling, materiais, texturas, buffers e passes exigidos pelas cenas selecionadas. Cada cena deve ter seu resultado registrado; suporte a um recurso não será inferido apenas porque o menu abriu.

Shaders usam SPIR-V e os contratos de recursos, constant buffers e materiais da engine. O cooker preserva nomes, bindings e layouts autorais; nenhuma conversão corrige shaders ou C++ gerados por substituições posteriores. O layout GPU continua obedecendo ao formato definido no contrato de shader, independentemente da precisão escalar selecionada para a engine.

O runtime respeita a política numérica imutável da build. Defaults ou conversões locais não promovem silenciosamente cálculos configuráveis para double. ABI, persistência, relógios e formatos gráficos mantêm suas precisões especificadas.

## Perfis, pacote e dependências

- Debug: símbolos, diagnósticos de runtime e nome da cena no overlay de debug; validação Vulkan quando disponível e explicitamente configurada.
- Release: otimização e política de diagnóstico do perfil, sem instrumentação de profiler habilitada por acidente.
- Profiler: otimização, símbolos e integração Tracy com os pontos de instrumentação existentes, incluindo escopos gerados quando selecionados.

O pacote executável contém `helengine_linux` e os recursos/runtime necessários identificados pelo manifesto. Bibliotecas fornecidas pelo sistema e bibliotecas distribuídas com o pacote são declaradas. SDL3 e dependências adicionais têm versão/revisão e hash fixados em lock; não há atualização automática por `latest` durante uma build.

O catálogo de bootstrap ganha a entrada Linux quando o sibling real existir. A criação de um remoto GitHub e seu push são operações separadas da compilação local. O código é compilado a partir dos checkouts reais; somente conteúdo e saídas geradas normais do pipeline entram na área de build.

Temporários, logs e outputs usam uma raiz fixa por plataforma: `C:\temp\helengine\linux` no host Windows e `/tmp/helengine/linux` no servidor Linux. Uma execução sobrescreve a anterior. Não há diretórios por data, tentativa ou build ID. Sondas descartáveis são removidas após a validação.

## Integração com o cook e o DemoDisc

A build completa no Linux depende da separação do host de build e dos importadores Windows identificados na preparação do servidor. Esse trabalho é um pré-requisito explícito para afirmar que o DemoDisc pode ser reconstruído integralmente ali. Uma prova nativa com conteúdo previamente cooked não será apresentada como validação desse pipeline completo.

O DemoDisc conserva todos os 44 botões do menu, sem filtrá-los conforme as cenas empacotadas. A seleção continua menu + 14 cenas de rendering, sem incluir física ou Tilt por conveniência. IDs de cena e contratos de assets permanecem estáveis. Trabalho de ray tracing já adiado não será convertido silenciosamente em um novo requisito de hardware Vulkan.

O layout do Linux segue o comportamento desktop dinâmico existente; a política Cook obrigatória dos consoles antigos não é propagada para o novo target.

## Falhas e validação

Ausência de loader Vulkan, dispositivo adequado, recursos de shader, asset obrigatório ou extensão requerida produz erro contextual com o requisito e a operação que falhou. Inicialização parcial libera os recursos adquiridos. Shader ou material incompatível não vira desenho em branco com sucesso reportado.

A validação é separada em quatro evidências:

1. Contratos de builder, perfil, geração, layout de shader e argumentos de processo, incluindo paths com espaços/Unicode e propagação de falhas.
2. Build nativa e inicialização Vulkan/SDL em sessão isolada; registrar se a GPU utilizada é hardware ou software. Compilação e inicialização isoladas não comprovam apresentação no desktop real.
3. Menu funcional: fonte, logo, transparência, clipping, resize e input, com retorno de cena e botões preservados.
4. Registro individual das 14 cenas: carregar, atualizar, desenhar e retornar ao menu; falhas e recursos pendentes continuam visíveis no resultado.

A plataforma só é descrita como validada no Wayland ou X11 após execução naquele caminho. Não se controla o desktop da Helena nem se fazem screenshots de suas janelas sem autorização. Testes de contratos e builds não dependem de automação da interface.

## Escopo posterior

Desktop wallpaper/overlay e múltiplas janelas com paridade completa com Windows não fazem parte do primeiro player Linux. Compartilhamento de um renderer Vulkan nativo com plataformas Apple depende da escolha arquitetural Apple; não se introduz essa dependência antecipadamente. macOS/iOS e os workers Xcode terão desenho próprio, mantendo os contratos de conteúdo e do core compatíveis.
