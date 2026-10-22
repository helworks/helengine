# HelEngine Apple: macOS e iOS com Metal

Helena aprovou o sibling `C:\dev\helworks\helengine-apple`, C++ nativo, SDL3 e Metal. A base Apple e o renderer são compartilhados; os targets públicos são `macos` e `ios`. O host de validação é `distro@192.168.15.7`, Apple Silicon com Xcode instalado. O Linux continua no sibling próprio com Vulkan.

## Responsabilidades

O repositório Apple possui builder, host e renderer. O builder obedece aos contratos existentes de geração, manifests, conteúdo cooked e precisão numérica da engine. Não depende do backend Windows nem gera uma segunda implementação da simulação. macOS e iOS compartilham Metal, recursos, materiais e adaptação dos contratos do core; inicialização, sandbox, lifecycle, input e empacotamento ficam nos adaptadores correspondentes.

SDL3 fornece janela, eventos e a view respaldada por CAMetalLayer. A integração Metal permanece em C++/Objective-C++ conforme necessário ao SDK; não requer migrar a engine para Swift. O renderer mantém ownership de command queue/buffers, render targets, texturas, buffers e pipelines. Recursos não são reciclados antes da conclusão GPU. Janela minimizada, drawable indisponível e suspensão não provocam busy loop nem apresentação inválida.

O payload dos shaders respeita o layout e a reflexão da engine. O caminho de shader produz Metal Shading Language e biblioteca Metal com as ferramentas do SDK selecionado, usando compilação fora do frame loop. Não se usa Vulkan/MoltenVK como fallback. Recursos ausentes são erros explícitos, não desenhos brancos reportados como sucesso.

## Targets e perfis

- macOS: aplicativo nativo para Apple Silicon; executable e bundle possuem manifest de conteúdo. Expansão para Intel depende de toolchain e validação próprios e não será anunciada sem teste.
- iOS: aplicativo para dispositivo e build de simulator separados. Um build unsigned é evidência de compilação; instalação/distribuição requerem identidade e provisioning reais, nunca inventados pelo bootstrap.
- Debug, Release e Profiler preservam o significado dos perfis desktop: símbolos/diagnósticos, otimização e instrumentação selecionada. O profiler respeita os contratos existentes de escopos gerados e Tracy; não recebe label de suporte completo sem captura real.
- iOS responde a suspensão/retomada e pressão de memória; recursos do bundle são read-only, gravações ficam em diretório apropriado do sandbox. Escala e dimensões em pixels não se confundem com coordenadas lógicas de UI/touch.

## Integração e critérios de conclusão

O catálogo workspace registra um sibling Apple; o catálogo de plataforma expõe dois targets. Não há dois clones do core. A portabilidade do host de cooking e das bibliotecas nativas de shader é pré-requisito para afirmar builds completas do DemoDisc fora do Windows.

Primeiro validar host/renderer e builds macOS/iOS, depois o core gerado e o menu, e então as 14 cenas de rendering. Os 44 botões do menu e os IDs permanecem; conteúdo não é filtrado por suporte do backend. Não se altera a política de viewport de consoles antigos, nem se força double sobre precisão configurável.

Builds e logs temporários usam `/tmp/helengine/macos` e `/tmp/helengine/ios` no Mac, e `C:\temp\helengine\macos` e `C:\temp\helengine\ios` no host Windows, sempre sobrescrevendo. Dependências persistentes têm versão e hash registrados; não há `latest` durante builds. Trabalhar na main dos checkouts reais, sem worktrees nem exports. O desktop da usuária não é controlado; validação inicial usa teste Metal offscreen e compilação, sem screenshots de janelas.

A evidência distingue compilação, inicialização GPU, renderização offscreen, menu funcional e execução individual das cenas. Nenhum desses passos substitui os demais. Falhas do SDK, compilador, shader, asset ou runtime propagam mensagem contextual e código de saída.

## Fontes primárias

- https://wiki.libsdl.org/SDL3/README-macos
- https://wiki.libsdl.org/SDL3/README-ios
- https://wiki.libsdl.org/SDL3/SDL_Metal_CreateView
- https://developer.apple.com/metal/cpp/
- https://developer.apple.com/xcode/system-requirements/
