# HelEngine: builder e runtime WebGPU

Data: 2026-10-14.
Estado: direção aprovada em conversa; especificação aguardando revisão de Helena.

## Objetivo e decisões aprovadas

Exportar projetos `.heproj` da HelEngine para executar no navegador, preservando o fluxo de scripts C# convertido para C++. Compilar o core e os módulos gerados para WebAssembly com Emscripten. Implementar WebGPU como primeiro backend gráfico da plataforma web.

Helena aprovou WebGPU após comparar esse caminho com WebGL 2. A entrega é um destino de build de jogos integrado ao editor e à CLI. O editor continua sendo a ferramenta que prepara o projeto.

O primeiro marco é uma cena real da engine: câmera, mesh texturizado, material básico e componente C# que responde ao input e modifica o estado da cena. Uma demonstração isolada de triângulo comprova o ambiente, mas não cumpre esse marco.

## Base existente e restrições

- `C:\dev\helworks\helengine\engine\helengine.baseplatform` contém o contrato de builder, perfis, requisições e relatórios consumido pelo editor.
- `C:\dev\helworks\helengine-windows\builder` exemplifica a integração entre cooking, manifests, C++ gerado e build CMake.
- `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorPlatformCodeCookService.cs` já converte módulos de runtime para C++ usando o codegen selecionado pela engine.
- O codegen oficial é o submódulo `C:\dev\helworks\helengine\engine\vendor\csharpcodegen`. Erros de geração devem ser corrigidos no produtor; arquivos gerados não podem ser remendados.
- O host Windows depende de Win32 e DirectX 11. A plataforma web precisa implementar os contratos compartilhados de runtime em seu próprio host.
- O backend Vulkan gerenciado usa Silk.NET; ele serve como referência de conceitos e contratos, não como implementação executável no navegador.
- Há mudanças locais em andamento na engine. A implementação deve preservar essas mudanças e limitar alterações compartilhadas ao necessário para registrar e gerar a plataforma web.
- Não capturar screenshots sem autorização específica de Helena.

O índice estrutural do host Windows estava desatualizado. As conclusões acima foram conferidas no código atual, por leitura dos arquivos.

## Organização proposta

Raiz da nova plataforma: `C:\dev\helworks\helengine-webgpu`.

| Área | Responsabilidade |
| --- | --- |
| `builder` | Metadados de plataforma, cooking de materiais, staging, manifests e execução do build Emscripten |
| `src/platform/web` | Inicialização do navegador, ciclo de frames, input, áudio e acesso ao conteúdo |
| `src/platform/webgpu` | Device, surface, recursos GPU, renderização e tratamento de falhas |
| `shaders` | WGSL dos materiais básicos suportados pela primeira versão |
| `web` | Página com canvas, carregamento e mensagens de erro |
| `scripts` | Preparação da toolchain e execução local do pacote exportado |
| `builder.tests` | Verificação dos contratos de builder e empacotamento |

Identificadores propostos: plataforma `webgpu`, assembly `helengine.webgpu.builder`, namespace `helengine.webgpu.builder`, perfis `debug` e `release`, perfil gráfico `webgpu`, codegen `default` com saída C++ e little endian.

As classes C# seguem o estilo da engine: uma classe por arquivo, documentação XML substantiva e responsabilidades separadas. A implementação não introduz uma nova abstração gráfica global.

## Fluxo de build

1. Editor/CLI seleciona `webgpu` e resolve o perfil do projeto.
2. O fluxo compartilhado prepara cenas e assets e gera o core e os módulos C++ de runtime.
3. O builder valida manifests, configurações e recursos suportados antes de compilar.
4. O builder monta os manifests nativos e uma árvore de conteúdo em seu diretório de trabalho.
5. CMake/Ninja, com a toolchain Emscripten, compila o host web, o backend WebGPU e o C++ gerado.
6. O conteúdo preparado é incluído no pacote de preload do Emscripten. O runtime acessa esse conteúdo no filesystem virtual depois do carregamento.
7. O builder publica os artefatos esperados e retorna o relatório pelo contrato atual. A orquestração compartilhada continua responsável pelo estado terminal da invocação.

Artefatos da primeira versão: `index.html`, `helengine_webgpu.js`, `helengine_webgpu.wasm` e `helengine_webgpu.data`. Os caminhos usados pela página são relativos para permitir hospedagem em subdiretórios.

Arquivos de trabalho, fontes geradas e logs ficam em `C:\dev\helworks\builds\helengine-webgpu` ou na fatia de cache escolhida pelo wrapper. A toolchain preparada fica em `C:\dev\helworks\tools\emsdk`. O builder reutiliza `NativeObjectCacheRoot` quando fornecido. Ele não limpa diretórios arbitrários nem modifica saídas de outras plataformas.

## Toolchain e portabilidade

Usar Emscripten 6.0.12, versão estável consultada nesta data, com o port oficial `emdawnwebgpu` incluído nessa toolchain. Preferir a API C `webgpu.h`, utilizável em C++, para reduzir dependência de alterações do wrapper C++. As versões usadas devem ser registradas nos diagnósticos de build.

Nesta sessão `cmake`, `ninja` e `dotnet` foram encontrados; `emcc` e `emcmake` não estavam no PATH. A preparação deve localizar ou instalar a toolchain no workspace e produzir um diagnóstico acionável se o download ou a ativação falhar.

Compilar primeiro o conjunto mínimo de core gerado exigido pela cena de validação, com RTTI e exceções compatíveis com as opções do codegen. O primeiro runtime usa uma única thread e não exige SharedArrayBuffer. A memória WASM pode crescer; dados entregues a JavaScript devem ser copiados ou suas views recriadas após crescimento de memória.

Qualquer incompatibilidade do runtime C++ com wasm32 deve ser investigada no código fonte que a produz. Uma mudança de contrato compartilhado ou estratégia de geração exige feedback de Helena antes de ser aplicada.

## Inicialização e ciclo de execução

A página apresenta carregamento enquanto baixa WASM e assets. A inicialização obtém adapter e device WebGPU de forma assíncrona, configura o canvas e somente depois inicializa o core e carrega a cena inicial do manifest.

O ciclo usa callbacks sincronizados com os frames do navegador. Cada frame entrega input ao core, avança a simulação pelos contratos atuais e renderiza a cena. O host não bloqueia o event loop aguardando adapter, device ou recursos. Perda de foco libera estados pressionados; retorno de uma aba oculta não injeta um intervalo de simulação equivalente a todo o tempo de suspensão.

Resize atualiza o tamanho real do canvas, os attachments e os viewports de câmera. Uma surface com dimensão zero suspende o desenho até voltar a um tamanho válido. Teardown encerra o loop e libera os recursos pertencentes ao host.

## Renderização do primeiro marco

Implementar os contratos atuais de `RenderManager3D` e visita de drawables com recursos WebGPU. A primeira implementação suporta câmera perspectiva/ortográfica, transformações, meshes indexados ou não, depth test, culling, base color, textura difusa e transparência básica.

O material básico usa WGSL mantido pela plataforma, empacotado pelo builder, com bindings de câmera, objeto, cor, textura e sampler. A integração traduz os campos do material básico existente sem exigir reescrever scripts ou duplicar cenas para a web. Shaders customizados que dependem de outros backends recebem diagnóstico explícito de incompatibilidade.

O perfil declara forward rendering, sem deferred, HDR, normal mapping, luzes ou sombras nessa primeira entrega. Recursos gráficos obrigatórios incompatíveis devem falhar com identificação do material, asset ou configuração. Reduções opcionais respeitam os contratos existentes de capacidades da engine. A ferramenta não deve declarar sucesso enquanto descarta silenciosamente conteúdo necessário.

Assets e instâncias de runtime mantêm sua identidade. Buffers, texturas e pipelines são reutilizados entre frames e liberados conforme ownership do runtime. O desenho consome os dados preparados pela engine; não cria uma segunda simulação em JavaScript.

## Input, áudio e página

O primeiro marco inclui teclado e mouse pelo canvas, com conversão para os contratos atuais de input e coordenadas coerentes com o tamanho de renderização. Touch, gamepad e pointer lock são extensões posteriores, não requisitos desse marco.

Áudio básico integra `IAudioBackend` a Web Audio para assets PCM preparados pelo cooker, com reprodução, parada, loop, ganho e controle de buses. A página oferece uma ação de iniciar que permite desbloquear áudio com interação da usuária. Formatos ou operações que o backend não suporta devem produzir diagnóstico explícito.

A página contém canvas, estado de carregamento, ação de iniciar e área de erro legível. O pacote usa localhost no desenvolvimento ou HTTPS na hospedagem. Falta de WebGPU, adapter indisponível, device perdido, arquivo ausente ou shader inválido interrompem a inicialização/execução com explicação; não ativam outro backend automaticamente.

## Limites e continuidade

Este marco estabelece o builder e um player web real. Paridade com todos os recursos do Windows, física 3D, vídeo, persistência, editor no navegador, iluminação avançada e fallback WebGL 2 exigem marcos posteriores. Projetos que precisem de recursos ausentes recebem diagnósticos, em vez de um pacote apresentado como plenamente compatível.

O primeiro projeto de validação será uma fixture pequena de `.heproj` com câmera, mesh texturizado e componente C# controlado pelo teclado. Isso permite verificar geração e execução sem depender dos recursos de um projeto grande. A exportação de um jogo existente escolhido por Helena é a etapa seguinte de compatibilidade.

## Critérios de aceite e validação

1. O editor e a CLI descobrem a plataforma pelo contrato atual e geram um pacote `webgpu` para a fixture.
2. A compilação inclui o core gerado e o módulo C# convertido; um bootstrap isolado não satisfaz o aceite.
3. O navegador carrega a cena pelo manifest e desenha seu mesh e textura com WebGPU.
4. O componente da fixture responde ao teclado e modifica uma transformação visível da cena.
5. Resize e perda de foco preservam o estado correto do runtime e input.
6. Áudio PCM funciona depois da ação de iniciar, inclusive parada e ganho de bus.
7. Toolchain ausente, asset faltante, material incompatível e falha GPU são comunicados sem sucesso falso.
8. Uma segunda compilação usa o cache escolhido; nenhum build modifica a saída de outra plataforma.

Rodar testes focados dos novos contratos, um build nativo WASM e a verificação funcional no navegador. Registrar comandos, artefatos e falhas encontradas. A verificação visual pode usar observação autorizada do navegador sem captura de screenshots.

## Fontes consultadas

- Emscripten e WebGPU: https://emscripten.org/docs/porting/multimedia_and_graphics/WebGPU-support.html
- Port Emdawnwebgpu e estabilidade das APIs: https://dawn.googlesource.com/dawn/+/refs/heads/main/src/emdawnwebgpu/pkg/README.md
- Emscripten 6.0.12: https://github.com/emscripten-core/emscripten/releases/tag/6.0.12
- Especificação WebGPU: https://www.w3.org/TR/webgpu/

## Próxima etapa

Helena revisa esta especificação, especialmente os limites do primeiro marco. Após aprovação, produzir o plano escrito com arquivos, interfaces e validações e submetê-lo à revisão antes da implementação, conforme a skill de brainstorming em uso.
