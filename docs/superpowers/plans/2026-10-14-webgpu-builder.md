# WebGPU Builder Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Exportar o Cube Test existente do DemoDisc para um player WebAssembly com WebGPU, incluindo seus scripts C#, câmera, luz e UI.

**Architecture:** Um plugin de plataforma fornece perfis, cooking e execução CMake/Emscripten pelo contrato atual da engine. Um host C++ de navegador inicializa o core gerado e implementa renderização WebGPU, input portátil e áudio Web Audio. O conteúdo vem do cooking existente e é pré-carregado no filesystem virtual.

**Tech Stack:** .NET 9, xUnit, C++20, CMake/Ninja, Emscripten 6.0.12, Emdawnwebgpu `webgpu.h`, WGSL e JavaScript para a página e pontes do navegador.

**Spec:** `C:\dev\helworks\helengine\docs\superpowers\specs\2026-10-14-webgpu-builder-design.md`.

**Execution status:** aguardando revisão do plano e escolha do método de execução. Nenhum código de produto foi implementado.

## Global Constraints

- Raiz da plataforma: `C:\dev\helworks\helengine-webgpu`.
- Identificadores: plataforma `webgpu`, assembly/namespace `helengine.webgpu.builder`, perfis `debug` e `release`, gráfico `webgpu`, codegen `default`, C++ little endian.
- Usar Emscripten 6.0.12 com o port oficial `emdawnwebgpu`; API C `webgpu.h`.
- O primeiro runtime usa uma única thread e não exige SharedArrayBuffer.
- Usar `PlatformMaterialAsset`, `RendererFamilyId = "webgpu"`, contrato `CookedPlatformOwned` e símbolo de codegen `DESKTOP_PLATFORM`.
- O perfil declara forward rendering, uma luz visível e zero luzes com sombras, sem deferred, HDR ou normal mapping.
- Projeto de aceite: `C:\dev\helprojs\demodisc\project.heproj`; cena `C:\dev\helprojs\demodisc\assets\scenes\rendering\cube_test.helen`.
- Preservar referências authored, scripts, UI desktop e luz direcional do Cube Test.
- Toolchain: `C:\dev\helworks\tools\emsdk`; logs, testes nativos e saídas: `C:\dev\helworks\builds\helengine-webgpu`.
- Artefatos públicos: `index.html`, `helengine_webgpu.js`, `helengine_webgpu.wasm`, `helengine_webgpu.data`, com caminhos relativos.
- Preservar mudanças locais de terceiros. Corrigir falhas no produtor, sem remendar C++ gerado, remover scripts ou simular sucesso.
- Uma classe por arquivo e documentação XML substantiva nos membros C#. Executar somente as validações relevantes para cada tarefa.
- Não capturar screenshots sem autorização específica. Mudanças arquiteturais adicionais exigem feedback de Helena.

## Review Focus

1. Caminhos com espaços, subdiretórios de hospedagem e tentativa de escape de staging: cotar argumentos e rejeitar paths fora das raízes do build (tarefas 2 e 7).
2. Canvas sem área e mudanças de devicePixelRatio: suspender desenho sem recursos inválidos e manter input/viewport na mesma escala (tarefas 3 e 5).
3. Memória WASM crescendo durante uploads/áudio: nenhuma ponte JavaScript retém uma view obsoleta do heap (tarefas 4 e 6).
4. Callbacks assíncronos chegando após shutdown/device loss: não inicializar novamente nem acessar recursos liberados (tarefa 3).
5. O módulo DemoDisc conter scripts além do Cube Test: registrar erros reais de geração/compilação e preservar o contrato do módulo (tarefas 3 e 7).

## Sequência e fronteiras

Implementar as sete tarefas em ordem. O primeiro teste de toolchain comprova ambiente; o aceite de jogo só ocorre na tarefa 7. Usar testes de contrato C# para o builder e testes de navegador para comportamentos GPU/DOM reais. Não substituir esses últimos por testes que procuram strings no código fonte.

Os headers gerados são a autoridade para assinaturas dos overrides C++; os nomes abaixo de contratos da engine referem-se às APIs C# atuais. Não editar os headers para fazer o host compilar.

### Task 1: Definição de plataforma e material básico

**Files:**
- Create: `C:\dev\helworks\helengine-webgpu\builder\helengine.webgpu.builder.csproj`.
- Create: `C:\dev\helworks\helengine-webgpu\builder\WebGpuPlatformDefinitionFactory.cs`.
- Create: `C:\dev\helworks\helengine-webgpu\builder\WebGpuMaterialCooker.cs`.
- Create: `C:\dev\helworks\helengine-webgpu\builder.tests\helengine.webgpu.builder.tests.csproj`.
- Create: `C:\dev\helworks\helengine-webgpu\builder.tests\WebGpuPlatformDefinitionTests.cs` e `C:\dev\helworks\helengine-webgpu\builder.tests\WebGpuMaterialCookerTests.cs`.

**Interfaces:**
- Consumes: tipos de `C:\dev\helworks\helengine\engine\helengine.baseplatform` e serialização de `C:\dev\helworks\helengine\engine\helengine.files`.
- Produces: `public static PlatformDefinition WebGpuPlatformDefinitionFactory.Create()`.
- Produces: `public PlatformMaterialCookResult WebGpuMaterialCooker.Cook(PlatformMaterialCookRequest request)`.

- [ ] Escrever os testes `Profiles_expose_webgpu_cpp_and_desktop_controls`, `Solid_material_round_trips_with_webgpu_family` e `Custom_shader_is_rejected_with_asset_identity`. Conferir plataforma/perfis, `DESKTOP_PLATFORM`, endianness, contrato `CookedPlatformOwned`, `Lit`, base color e ausência de dependências HLSL/SPIR-V para o material básico. Usar o esquema `standard-shader` existente, validar seus campos e serializar `PlatformMaterialAsset` pelo produtor oficial.

```csharp
PlatformDefinition definition = WebGpuPlatformDefinitionFactory.Create();
Assert.Equal("webgpu", definition.PlatformId);
PlatformMaterialCookRequest request = new PlatformMaterialCookRequest(
    "cube-solid", "materials/rendering/cube_test/CubeTestSolid.hasset",
    "webgpu", "release", "webgpu", "standard-shader",
    new Dictionary<string, string> {
        ["use-custom-shader"] = "false", ["base-color"] = "#ffffffff",
        ["casts-shadow"] = "false", ["receives-shadow"] = "false"
    });
PlatformMaterialCookResult result = new WebGpuMaterialCooker().Cook(request);
using MemoryStream stream = new MemoryStream(result.CookedMaterialBytes);
PlatformMaterialAsset material = Assert.IsType<PlatformMaterialAsset>(helengine.files.AssetSerializer.Deserialize(stream));
Assert.Equal("webgpu", material.RendererFamilyId);
Assert.Equal(255, material.BaseColorA);
Assert.Empty(result.ReferencedShaderDependencies);
```

- [ ] Rodar `dotnet test C:\dev\helworks\helengine-webgpu\builder.tests\helengine.webgpu.builder.tests.csproj --filter "FullyQualifiedName~WebGpuPlatformDefinitionTests|FullyQualifiedName~WebGpuMaterialCookerTests"`. Confirmar a falha dos contratos ausentes antes da implementação.
- [ ] Implementar perfis `debug`/`release`, material básico e referências aos projetos compartilhados pelo parâmetro MSBuild `HelEngineRoot`, seguindo o padrão Windows. Usar as versões de xUnit/Test SDK já utilizadas nos testes do builder Windows.
- [ ] Repetir o comando; exigir todos os testes selecionados passando.
- [ ] Registrar somente os arquivos desta tarefa no histórico da nova plataforma, quando seu repositório existir.

### Task 2: Builder, toolchain, staging e pacote

**Files:**
- Create: `C:\dev\helworks\helengine-webgpu\builder\WebGpuPlatformAssetBuilder.cs`, `C:\dev\helworks\helengine-webgpu\builder\WebGpuBuildWorkspace.cs` e `C:\dev\helworks\helengine-webgpu\builder\WebGpuRuntimeManifestWriter.cs`.
- Create: `C:\dev\helworks\helengine-webgpu\builder\IWebGpuNativeBuildExecutor.cs`, `C:\dev\helworks\helengine-webgpu\builder\WebGpuNativeBuildExecutor.cs`, `C:\dev\helworks\helengine-webgpu\builder\WebGpuNativeBuildResult.cs` e `C:\dev\helworks\helengine-webgpu\builder\WebGpuToolchainResolver.cs`.
- Create: `C:\dev\helworks\helengine-webgpu\scripts\setup-toolchain.ps1`, `C:\dev\helworks\helengine-webgpu\CMakeLists.txt` e `C:\dev\helworks\helengine-webgpu\platform-plugin.json`.
- Create: `C:\dev\helworks\helengine-webgpu\builder.tests\WebGpuBuildWorkspaceTests.cs`, `C:\dev\helworks\helengine-webgpu\builder.tests\WebGpuNativeBuildExecutorTests.cs` e `C:\dev\helworks\helengine-webgpu\builder.tests\WebGpuRuntimeManifestWriterTests.cs`.

**Interfaces:**
- Consumes: `PlatformDefinition` e `Cook` da tarefa 1; `PlatformBuildRequest`, manifests, reporters e cancellation do contrato atual.
- Produces: `WebGpuPlatformAssetBuilder : IPlatformAssetBuilder`, incluindo `Descriptor`, `Definition`, `CookMaterial` e a assinatura existente de `BuildAsync`.
- Produces: `WebGpuNativeBuildResult IWebGpuNativeBuildExecutor.Build(string repositoryRoot, string buildRoot, string generatedCoreRoot, string stagedCodeRoot, string contentRoot, string profileId, CancellationToken cancellationToken)`.
- Produces: resultado com propriedades `JavaScriptPath`, `WebAssemblyPath`, `DataPath`; `void WebGpuRuntimeManifestWriter.Write(string generatedCoreRoot, PlatformBuildManifest manifest)`.

- [ ] Escrever testes com um executor injetado: `Missing_payload_fails_before_native_compile`, `Cancellation_propagates`, `Missing_wasm_is_not_success`, `Staging_escape_is_rejected`, `Arguments_preserve_paths_with_spaces`, `AssetsOnly_stages_without_native_compile` e `Manifest_preserves_startup_and_module_identity`. Usar diretórios de teste em `C:\dev\helworks\builds\helengine-webgpu\managed-tests`.
- [ ] Rodar o projeto de testes com filtro das três classes desta tarefa e confirmar falhas antes de implementar.
- [ ] Implementar validação anterior ao staging, cópia de payloads por manifest, emissão de manifests próprios e relatório completo. Usar o cache recebido na requisição. Não apagar toda a saída do usuário para corrigir estado de build.
- [ ] Implementar resolução/instalação da toolchain fixa no workspace e logs separados de configure/build. Invocar CMake pela toolchain Emscripten com `ProcessStartInfo.ArgumentList`; fornecer `--use-port=emdawnwebgpu` na compilação e link. Exigir WASM/JS/data não vazios antes de retornar sucesso. A página será produzida na tarefa 7.
- [ ] Configurar CMake para consumir fontes geradas oficiais, runtime compartilhado e módulos de código, com C++20 e RTTI/exceções coerentes com o codegen; habilitar filesystem e preload do conteúdo. `debug` inclui diagnósticos; `release` usa otimização e Closure. Falta de fontes ou entrypoint deve falhar, sem gerar um player de demonstração como substituto.
- [ ] Rodar testes selecionados e `C:\dev\helworks\helengine-webgpu\scripts\setup-toolchain.ps1`; confirmar `emcc --version` como 6.0.12. Conservar falhas de download/compilação e seus logs.
- [ ] Registrar os arquivos da tarefa.

### Task 3: Device WebGPU e portabilidade do core

**Files:**
- Create: `C:\dev\helworks\helengine-webgpu\src\platform\webgpu\webgpu_device_context.hpp` e `C:\dev\helworks\helengine-webgpu\src\platform\webgpu\webgpu_device_context.cpp`.
- Create: `C:\dev\helworks\helengine-webgpu\src\platform\web\web_runtime_error.hpp`, `C:\dev\helworks\helengine-webgpu\src\platform\web\web_runtime_error.cpp` e `C:\dev\helworks\helengine-webgpu\src\platform\web\web_frame_clock.hpp`/`C:\dev\helworks\helengine-webgpu\src\platform\web\web_frame_clock.cpp`.
- Create: `C:\dev\helworks\helengine-webgpu\tests\browser\device_lifecycle.spec.js` e `C:\dev\helworks\helengine-webgpu\tests\browser\package.json`.
- Modify: `C:\dev\helworks\helengine-webgpu\CMakeLists.txt`.

**Interfaces:**
- Consumes: `webgpu.h`, callbacks do Emdawnwebgpu e canvas `#helengine-canvas`.
- Produces: `void WebGpuDeviceContext::Initialize(const char* canvasSelector, std::function<void()> ready, std::function<void(const std::string&)> failed)`, `bool Resize(uint32_t width, uint32_t height)`, `void Shutdown()`, `WGPUDevice GetDevice() const` e `WGPUTextureFormat GetSurfaceFormat() const`.
- Produces: `double WebFrameClock::Advance(double timestampMilliseconds)` e `void Reset()`; o primeiro frame e o primeiro frame após reset não acumulam o tempo de suspensão.

- [ ] Configurar o runner de navegador em `C:\dev\helworks\helengine-webgpu\tests\browser\package.json` com Playwright e script `test` executando `playwright test`; usar o navegador/adapter WebGPU real disponível, com falha explícita se a prova GPU não puder rodar. Escrever testes `missing_adapter_reports_error`, `late_ready_after_shutdown_is_ignored`, `device_loss_stops_presentation`, `zero_canvas_suspends_and_resize_resumes` e `hidden_tab_resets_clock`. Rodar via `npm test -- device_lifecycle.spec.js` nessa pasta; confirmar as falhas iniciais. Os testes não tiram screenshots.
- [ ] Implementar aquisição assíncrona de adapter/device, configuração da surface, estado terminal de shutdown/device loss e clock do host. O teste de ausência de adapter pode simular essa falha; a prova GPU usa um device real.
- [ ] Compilar o core gerado e módulos do DemoDisc como entradas WASM reais, mantendo manifests e registros de tipos. Guardar o log em `C:\dev\helworks\builds\helengine-webgpu\cube-test\core-portability.log`.
- [ ] Quando houver erro de portabilidade, localizar o produtor e registrar arquivo/linha/causa. Corrigir falhas comuns em código mantido; alterações de ABI, ownership, geração ou seleção de módulos precisam de feedback antes de editar. Não substituir o core por uma simulação JavaScript.
- [ ] Rodar novamente os testes e a compilação do core. Este passo comprova device e compilação, sem alegar que a cena já funciona.
- [ ] Registrar os arquivos da tarefa e quaisquer correções aprovadas no repositório que as possui.

### Task 4: Renderer 3D e material do Cube Test

**Files:**
- Create: `C:\dev\helworks\helengine-webgpu\src\platform\webgpu\webgpu_render_manager_3d.hpp`/`C:\dev\helworks\helengine-webgpu\src\platform\webgpu\webgpu_render_manager_3d.cpp`.
- Create: `C:\dev\helworks\helengine-webgpu\src\platform\webgpu\webgpu_runtime_model.hpp`, `C:\dev\helworks\helengine-webgpu\src\platform\webgpu\webgpu_runtime_material.hpp` e implementações correspondentes nessa mesma pasta.
- Create: `C:\dev\helworks\helengine-webgpu\shaders\basic_mesh.wgsl`.
- Create: `C:\dev\helworks\helengine-webgpu\tests\browser\mesh_rendering.spec.js`.
- Modify: `C:\dev\helworks\helengine-webgpu\CMakeLists.txt`.

**Interfaces:**
- Consumes: device/surface da tarefa 3; headers gerados de `RenderManager3D`, `IRenderVisitor3D`, `ModelAsset`, `PlatformMaterialAsset`, `RuntimeModel` e `RuntimeMaterial`.
- Produces: `WebGpuRenderManager3D`, com overrides dos contratos atuais `BuildModelFromRaw`, `BuildModelFromCooked`, `BuildMaterialFromCooked`, `ReleaseModel`, `ReleaseMaterial`, `GetCapabilityProfile`, `Draw`, `OnWindowResize` e `Visit(IDrawable3D)`.
- Produces: `void BeginFrame()` e `void EndFrame()` para coordenar os passes 3D e 2D no mesmo frame.

- [ ] Escrever testes `solid_mesh_has_depth_and_expected_base_color`, `directional_light_changes_shading`, `wrong_renderer_family_fails`, `duplicate_asset_id_with_conflicting_data_fails` e `repeated_draw_reuses_gpu_resources`. Verificar pixels por readback numérico de uma textura de teste, sem capturar a tela. Medir contadores de recursos em build de diagnóstico.
- [ ] Rodar `npm test -- mesh_rendering.spec.js` e confirmar as falhas antes de implementar.
- [ ] Implementar recursos/pipelines WGSL, layout de vértice, transformações e projeção da engine, depth/culling e uma luz direcional. Reutilizar o planejamento de câmeras/luzes. O perfil retorna forward=true, demais opções gráficas avançadas=false, visibleLights=1 e shadowedLights=0.
- [ ] Ler os modelos e materiais preparados usando os serializers gerados existentes; os métodos de leitura de conteúdo validam paths e tipos. Alinhar uniform buffers conforme WebGPU; não usar o layout de structs C++ por suposição.
- [ ] Implementar ownership e liberação de recursos. Qualquer upload que use o heap WASM não retém views JavaScript entre crescimento de memória. Adicionar a prova `heap_growth_does_not_corrupt_upload` ao mesmo arquivo de teste.
- [ ] Rodar os testes e compilar WASM nas configurações debug/release. A cena completa continua dependendo da tarefa 5.
- [ ] Registrar os arquivos da tarefa.

### Task 5: UI 2D, input e host da cena real

**Files:**
- Create: `C:\dev\helworks\helengine-webgpu\src\platform\webgpu\webgpu_render_manager_2d.hpp`/`C:\dev\helworks\helengine-webgpu\src\platform\webgpu\webgpu_render_manager_2d.cpp` e `C:\dev\helworks\helengine-webgpu\src\platform\webgpu\webgpu_runtime_texture.hpp`/`C:\dev\helworks\helengine-webgpu\src\platform\webgpu\webgpu_runtime_texture.cpp`.
- Create: `C:\dev\helworks\helengine-webgpu\src\platform\web\web_input_backend.hpp`/`C:\dev\helworks\helengine-webgpu\src\platform\web\web_input_backend.cpp` e `C:\dev\helworks\helengine-webgpu\src\platform\web\web_application.hpp`/`C:\dev\helworks\helengine-webgpu\src\platform\web\web_application.cpp`.
- Create: `C:\dev\helworks\helengine-webgpu\src\main.cpp`, `C:\dev\helworks\helengine-webgpu\shaders\ui.wgsl`, `C:\dev\helworks\helengine-webgpu\tests\browser\scene_input_ui.spec.js`.
- Modify: `C:\dev\helworks\helengine-webgpu\CMakeLists.txt`.

**Interfaces:**
- Consumes: as tarefas 3/4, manifests da tarefa 2 e headers de `RenderManager2D`, `IRenderVisitor2D`, `IInputBackend`, `InputFrameState`, `Core`, `CoreInitializationOptions`, `RuntimeSceneCatalog` e `RuntimeSceneLoadService`.
- Produces: `WebInputBackend : IInputBackend`, com `InputFrameState CaptureFrame()`; estado de gamepads desconectados é a representação atual da engine.
- Produces: `WebGpuRenderManager2D` com contratos `BuildTextureFromRaw`, `BuildTextureFromCooked`, `DrawSprite`, `DrawText`, `DrawRoundedRect`, release/flush e visita de drawables.
- Produces: `void WebApplication::Start()`, `void Frame(double timestampMilliseconds)` e `void Stop()`; `main()` chama `Start` e retorna controle ao navegador.

- [ ] Escrever testes `atlas_text_and_icons_draw_in_order`, `rounded_panel_clips_children`, `dpi_resize_preserves_pointer_alignment`, `keydown_then_blur_releases_key` e `held_key_is_not_repeated_as_new_press`. Rodar `npm test -- scene_input_ui.spec.js` e confirmar falhas.
- [ ] Implementar textura RGBA/font atlas, sprites, texto, retângulos arredondados e scissor usando os contratos existentes de comandos/batching 2D. Manter ordem e referências da UI da cena, sem recriá-la em HTML.
- [ ] Converter key/mouse/wheel/foco do canvas para `InputFrameState`, com DPI e coordenadas iguais às usadas pelo renderer. Habilitar `DESKTOP_PLATFORM` no perfil da tarefa 1 para preservar A/D, W/S e Q/E dos scripts existentes. Não alterar `DemoDiscOrbitCameraComponent` para adaptar o backend.
- [ ] Inicializar content source, core, renderers e input explicitamente; registrar módulos gerados, fornecer catalog/settings e carregar a cena inicial pelo manifest após preload e device pronto. Seguir a ordem de inicialização do host Windows, usando apenas seus contratos compartilhados.
- [ ] Avançar `Core.Update(double)` e renderização via callbacks do navegador; coordenar resize, focus e shutdown com a tarefa 3. Adicionar o teste `cube_spin_uses_engine_elapsed_time` e exigir mudança real de orientação do componente C# convertido.
- [ ] Rodar testes selecionados e compilar o player com o core real. Conservar qualquer erro de fonte/asset/registro em vez de desenhar objetos substitutos.
- [ ] Registrar os arquivos da tarefa.

### Task 6: Áudio PCM por Web Audio

**Files:**
- Create: `C:\dev\helworks\helengine-webgpu\src\platform\web\web_audio_backend.hpp`/`C:\dev\helworks\helengine-webgpu\src\platform\web\web_audio_backend.cpp`.
- Create: `C:\dev\helworks\helengine-webgpu\web\audio.js` e `C:\dev\helworks\helengine-webgpu\tests\browser\audio.spec.js`.
- Modify: `C:\dev\helworks\helengine-webgpu\src\platform\web\web_application.cpp` e `C:\dev\helworks\helengine-webgpu\CMakeLists.txt`.

**Interfaces:**
- Consumes: `IAudioBackend`, `AudioAsset` e `AudioPlaybackRequest` dos headers gerados.
- Produces: `WebAudioBackend : IAudioBackend` com os métodos atuais `Play`, `Stop`, `SetBusGain`, `SetBusPaused`, `IsPlaying` e `Update`.
- Produces: `Promise<void> unlockAudio()` para a ação de iniciar; a ponte copia o PCM durante a chamada, sem conservar views do heap WASM.

- [ ] Escrever testes `gesture_unlocks_audio`, `pcm_voice_stops_and_reports_completion`, `bus_gain_and_pause_are_applied`, `loop_stays_active_until_stop` e `heap_growth_preserves_pcm_buffer`. Usar um pequeno PCM de teste, sem acrescentar áudio ao Cube Test.
- [ ] Rodar `npm test -- audio.spec.js` e confirmar falhas iniciais.
- [ ] Implementar vozes Web Audio, ganho/pausa de buses, loop, conclusão e desbloqueio por gesto. Operações ou formatos inválidos comunicam erro com asset/voice; usar o comportamento de pausa dos contratos atuais, preservando posição de reprodução.
- [ ] Conectar o backend ao core e rodar os testes de navegador. Registrar verificação funcional separada do Cube Test silencioso.
- [ ] Registrar os arquivos da tarefa.

### Task 7: Registro, página e aceite completo com DemoDisc

**Files:**
- Create: `C:\dev\helworks\helengine-webgpu\web\index.html`, `C:\dev\helworks\helengine-webgpu\web\bootstrap.js`, `C:\dev\helworks\helengine-webgpu\scripts\serve.ps1` e `C:\dev\helworks\helengine-webgpu\README.md`.
- Create: `C:\dev\helworks\helengine-webgpu\tests\browser\cube_test.spec.js` e `C:\dev\helworks\helengine-webgpu\builder.tests\WebGpuPackagePublicationTests.cs`.
- Modify: `C:\dev\helworks\helengine-webgpu\builder\WebGpuBuildWorkspace.cs`.
- Modify: `C:\dev\helworks\helengine\user_settings\platforms.json` para acrescentar somente a instalação WebGPU.
- Modify after filesystem approval if needed: `C:\dev\helprojs\demodisc\project.heproj`, `C:\dev\helprojs\demodisc\settings\platforms.json`, `C:\dev\helprojs\demodisc\settings\platform.webgpu.json`, `C:\dev\helprojs\demodisc\settings\build_config.json` e `C:\dev\helprojs\demodisc\user_settings\build_config.json`.
- Modify after filesystem approval if needed: configurações por plataforma do material existente em `C:\dev\helprojs\demodisc\assets\materials\rendering\cube_test`, usando os serviços atuais de authoring/material settings.

**Interfaces:**
- Consumes: builder/player das tarefas anteriores, schemas de configuração e registro atuais da engine.
- Produces: pacote publicável com os quatro artefatos da especificação; bootstrap chama o módulo Emscripten após verificar WebGPU e associa a ação iniciar ao áudio.
- Produces: `serve.ps1 -Root <absolute-output-path> -Port <port>` que serve apenas a raiz escolhida no localhost, com MIME de WASM e erro explícito se a porta não puder ser usada.

- [ ] Escrever testes `publication_requires_all_four_nonempty_artifacts`, `relative_subdirectory_urls_load`, `loading_and_runtime_errors_are_visible` e `cube_test_scene_identity_is_preserved`. Conferir manifest/scene id `10000000000000000000000000000010`, referência `Engine/Models/Cube`, cubo girando e câmera respondendo a A/D, W/S e Q/E.
- [ ] Rodar o filtro `WebGpuPackagePublicationTests` e `npm test -- cube_test.spec.js`; confirmar as falhas antes de implementar página/registro.
- [ ] Acrescentar o plugin ao registro local, escolher o engineVersion pelo contrato atual e cadastrar WebGPU no projeto sem alterar as entradas de outras plataformas. Configurar somente o Cube Test para esse build, com perfil `release`, ambiente `release`, gráfico `webgpu`, codegen `default` e output `C:\dev\helworks\builds\helengine-webgpu\cube-test\player`.
- [ ] Usar a seleção de cenas e o cooking atuais, preservando o arquivo authored do Cube Test, os scripts e o material. A configuração fica dividida corretamente: cenas compartilhadas em settings; opções/saída locais em user_settings. Se o acesso externo ao workspace for negado pelo auto-review, completar o trabalho interno e informar exatamente quais escritas ficaram bloqueadas.
- [ ] Implementar página responsiva com canvas `helengine-canvas`, carregamento, iniciar e erro; paths relativos funcionam tanto na raiz quanto em subdiretório. A falta de WebGPU não ativa outro backend. Documentar build, preparação da toolchain e execução local com caminhos absolutos.
- [ ] Executar o wrapper atual com o Build Waiter, requerendo os quatro artefatos. Comando de aceite:

```powershell
dotnet run --project C:\dev\helworks\helengine\tools\build-waiter\helengine.buildwaiter.csproj -- --output C:\dev\helworks\builds\helengine-webgpu\cube-test\player --require index.html --require helengine_webgpu.js --require helengine_webgpu.wasm --require helengine_webgpu.data -- powershell -NoProfile -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\build-platform.ps1 -Project C:\dev\helprojs\demodisc\project.heproj -Platform webgpu -Output C:\dev\helworks\builds\helengine-webgpu\cube-test\player -BuildProfile release -CacheRoot C:\dev\helworks\builds\helengine-webgpu\cache
```

- [ ] Servir o pacote no localhost e executar o teste do Cube Test num navegador com WebGPU real. Conferir scene load, cubo girando, luz, texto/ícones, controle de câmera, resize, blur e ausência de erros não tratados. Validar GPU por testes/readback numérico, sem screenshots da tela.
- [ ] Repetir uma vez o mesmo build para comprovar cache e estado terminal atual. Rodar a suíte do novo builder e somente testes compartilhados afetados por mudanças reais. Não alegar sucesso se nenhum navegador/GPU compatível estiver disponível.
- [ ] Registrar versões, comandos, logs, artefatos e limitações em `C:\dev\helworks\builds\helengine-webgpu\cube-test\validation.md`; fazer uma revisão final do diff e registrar somente as mudanças da tarefa nos respectivos repositórios.

## Revisão do plano

Cobertura conferida: definição/cooking (1), execução/cache/artefatos (2/7), portabilidade/device/lifecycle (3), mesh/material/luz (4), UI/input/scripts (5), áudio (6), registro/página/cena real (7). As cinco condições do Review Focus têm testes explícitos nas tarefas indicadas. Falhas que demandem uma arquitetura diferente continuam sujeitas a feedback de Helena.

Recomendação de execução: **Native**, implementando as tarefas sequencialmente nesta sessão. Elas dependem fortemente das mesmas interfaces de core, build e recursos GPU; a sequência reduz retrabalho entre tarefas. Uma revisão independente final só é iniciada após escolha explícita desse método, conforme as instruções de execução aplicáveis. Para subagentes eventualmente autorizados, usar `gpt-6.1-sol` com raciocínio `high`, conforme as instruções de Helena.
