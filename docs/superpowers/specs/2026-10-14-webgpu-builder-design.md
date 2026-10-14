# HelEngine: builder e runtime WebGPU

Data: 2026-10-14.
Estado: especificação aprovada por Helena, incluindo o Cube Test existente, em 2026-10-14.

## Objetivo e decisões aprovadas

Exportar projetos `.heproj` da HelEngine para executar no navegador, preservando o fluxo de scripts C# convertido para C++. Compilar o core e os módulos gerados para WebAssembly com Emscripten. Implementar WebGPU como primeiro backend gráfico da plataforma web.

Helena aprovou WebGPU após comparar esse caminho com WebGL 2. A entrega é um destino de build de jogos integrado ao editor e à CLI. O editor continua sendo a ferramenta que prepara o projeto.

O primeiro marco usa o Cube Test existente no projeto `C:\dev\helprojs\demodisc\project.heproj`, com a cena `C:\dev\helprojs\demodisc\assets\scenes\rendering\cube_test.helen`. O aceite inclui o cubo de material sólido, sua rotação em C#, câmera orbital controlável, luz direcional e UI desktop da cena. Uma demonstração isolada de triângulo comprova o ambiente, mas não cumpre esse marco. Texturas 3D serão verificadas posteriormente com uma cena existente de cubos texturizados.

## Base existente e restrições

- `C:\dev\helworks\helengine\engine\helengine.baseplatform` contém o contrato de builder, perfis, requisições e relatórios consumido pelo editor.
- `C:\dev\helworks\helengine-windows\builder` exemplifica a integração entre cooking, manifests, C++ gerado e build CMake.
- `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorPlatformCodeCookService.cs` já converte módulos de runtime para C++ usando o codegen selecionado pela engine.
- O codegen oficial é o submódulo `C:\dev\helworks\helengine\engine\vendor\csharpcodegen`. Erros de geração devem ser corrigidos no produtor; arquivos gerados não podem ser remendados.
- O host Windows depende de Win32 e DirectX 11. A plataforma web precisa implementar os contratos compartilhados de runtime em seu próprio host.
- O backend Vulkan gerenciado usa Silk.NET; ele serve como referência de conceitos e contratos, não como implementação executável no navegador.
- Há mudanças locais em andamento na engine. A implementação deve preservar essas mudanças e limitar alterações compartilhadas ao necessário para registrar e gerar a plataforma web.
- Não capturar screenshots sem autorização específica de Helena.
- A cena de validação foi indicada por Helena. Seus scripts estão no módulo de runtime `DemoDisc`, declarado em `C:\dev\helprojs\demodisc\assets\codebase\code.module.json`. A conversão desse módulo pode trazer dependências de outros scripts do projeto; incompatibilidades devem ser diagnosticadas, sem remover scripts silenciosamente.

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

Implementar os contratos atuais de `RenderManager3D`, `RenderManager2D` e visita de drawables com recursos WebGPU. A primeira implementação suporta câmera perspectiva/ortográfica, transformações, meshes indexados ou não, depth test, culling, base color e uma luz direcional sem sombras. A UI desktop do Cube Test exige sprites texturizados, texto com o atlas de fonte preparado, retângulos arredondados, blending, clipping e os contratos atuais de viewport/escala.

O material básico usa WGSL mantido pela plataforma, empacotado pelo builder, com bindings de câmera, objeto, cor e luz direcional. O caminho 2D acrescenta textura e sampler para sprites e atlas de fonte. A integração traduz os campos do material básico existente sem exigir reescrever scripts ou duplicar cenas para a web. Shaders customizados que dependem de outros backends recebem diagnóstico explícito de incompatibilidade.

O payload de material reutiliza `PlatformMaterialAsset` e o serializador da engine, com `RendererFamilyId` igual a `webgpu`. O contrato de geração usa `CookedPlatformOwned`. O perfil de codegen inclui `DESKTOP_PLATFORM` para preservar os controles de teclado condicionais dos scripts existentes do DemoDisc.

O perfil declara forward rendering, uma luz visível e zero luzes com sombras, sem deferred, HDR ou normal mapping nessa primeira entrega. Tipos de luz além da direcional recebem diagnóstico de incompatibilidade. Recursos gráficos obrigatórios incompatíveis devem falhar com identificação do material, asset ou configuração. Reduções opcionais respeitam os contratos existentes de capacidades da engine. A ferramenta não deve declarar sucesso enquanto descarta silenciosamente conteúdo necessário.

Assets e instâncias de runtime mantêm sua identidade. Buffers, texturas e pipelines são reutilizados entre frames e liberados conforme ownership do runtime. O desenho consome os dados preparados pela engine; não cria uma segunda simulação em JavaScript.

## Input, áudio e página

O primeiro marco inclui teclado e mouse pelo canvas, com conversão para os contratos atuais de input e coordenadas coerentes com o tamanho de renderização. Touch, gamepad e pointer lock são extensões posteriores, não requisitos desse marco.

Áudio básico integra `IAudioBackend` a Web Audio para assets PCM preparados pelo cooker, com reprodução, parada, loop, ganho e controle de buses. A página oferece uma ação de iniciar que permite desbloquear áudio com interação da usuária. Formatos ou operações que o backend não suporta devem produzir diagnóstico explícito. O Cube Test foi preparado sem áudio; sua execução não comprova esse backend. A validação de áudio usa um asset PCM separado, sem acrescentar música à cena de referência.

A página contém canvas, estado de carregamento, ação de iniciar e área de erro legível. O pacote usa localhost no desenvolvimento ou HTTPS na hospedagem. Falta de WebGPU, adapter indisponível, device perdido, arquivo ausente ou shader inválido interrompem a inicialização/execução com explicação; não ativam outro backend automaticamente.

## Limites e continuidade

Este marco estabelece o builder e um player web real. Paridade com todos os recursos do Windows, física 3D, vídeo, persistência, editor no navegador, iluminação avançada e fallback WebGL 2 exigem marcos posteriores. Projetos que precisem de recursos ausentes recebem diagnósticos, em vez de um pacote apresentado como plenamente compatível.

O projeto de validação é o DemoDisc indicado por Helena, em `C:\dev\helprojs\demodisc\project.heproj`, selecionando `cube_test` como cena inicial pelo fluxo atual de configurações da plataforma. A implementação deve preservar as referências ao modelo `Engine/Models/Cube`, ao material `C:\dev\helprojs\demodisc\assets\materials\rendering\cube_test\CubeTestSolid.hasset` e aos componentes `CubeTestSpinComponent` e `DemoDiscOrbitCameraComponent`. A UI desktop e a luz direcional fazem parte do aceite. O build deve selecionar as dependências da cena pelo cooking existente e não abrir o menu completo do DemoDisc para esse teste.

A inscrição da nova plataforma e suas configurações de build no DemoDisc são parte da integração necessária. O conteúdo authored da cena deve ser preservado. Como esse projeto está fora da raiz gravável da sessão, qualquer escrita necessária em `C:\dev\helprojs\demodisc` deve respeitar o mecanismo de aprovação de acesso do ambiente. Saídas e logs da validação ficam em `C:\dev\helworks\builds\helengine-webgpu\cube-test`.

## Critérios de aceite e validação

1. O editor e a CLI descobrem a plataforma pelo contrato atual e geram um pacote `webgpu` do DemoDisc com o Cube Test selecionado como cena inicial.
2. A compilação inclui o core gerado e o módulo C# convertido; um bootstrap isolado não satisfaz o aceite.
3. O navegador carrega o Cube Test pelo manifest e desenha seu cubo de material sólido, luz direcional e UI desktop com WebGPU.
4. `CubeTestSpinComponent` gira o cubo com o tempo da engine; `DemoDiscOrbitCameraComponent` responde aos controles existentes mapeados para teclado/mouse.
5. Resize e perda de foco preservam o estado correto do runtime e input.
6. Áudio PCM funciona depois da ação de iniciar, inclusive parada e ganho de bus, em uma verificação separada porque o Cube Test é silencioso.
7. Toolchain ausente, asset faltante, material incompatível e falha GPU são comunicados sem sucesso falso.
8. Uma segunda compilação usa o cache escolhido; nenhum build modifica a saída de outra plataforma.

Rodar testes focados dos novos contratos, um build nativo WASM e a verificação funcional no navegador. Registrar comandos, artefatos e falhas encontradas. A verificação visual pode usar observação autorizada do navegador sem captura de screenshots.

## Fontes consultadas

- Emscripten e WebGPU: https://emscripten.org/docs/porting/multimedia_and_graphics/WebGPU-support.html
- Port Emdawnwebgpu e estabilidade das APIs: https://dawn.googlesource.com/dawn/+/refs/heads/main/src/emdawnwebgpu/pkg/README.md
- Emscripten 6.0.12: https://github.com/emscripten-core/emscripten/releases/tag/6.0.12
- Especificação WebGPU: https://www.w3.org/TR/webgpu/

## Próxima etapa

A especificação foi aprovada. Produzir o plano escrito com arquivos, interfaces e validações e submetê-lo à revisão antes da implementação, conforme a skill de brainstorming em uso.
