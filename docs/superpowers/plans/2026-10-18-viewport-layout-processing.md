# Viewport Layout Processing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** implementar Cook, OnLoad e Dynamic por viewport/plataforma, eliminando snapshots universais e a retenção de originais nos dois primeiros modos.

**Architecture:** separar matemática/aplicação, retenção opcional e coordenação do owner. Cook e OnLoad aplicam diretamente os valores; Dynamic/editor usam buffers de registros de valor por tipo de participante. O empacotador resolve o alvo efetivo com o backend antes do cook e usa o mesmo processamento do runtime, sem callbacks de gameplay.

**Tech Stack:** C#/.NET 9, xUnit, persistência HELE/ordinal existente, geração C++ oficial, backends Windows e PS1.

**Spec:** C:\dev\helworks\helengine\docs\superpowers\specs\2026-10-18-viewport-layout-processing-design.md, aprovada pela Helena em 2026-10-18.

## Global Constraints

- Enum byte: Dynamic = 0, OnLoad = 1, Cook = 2. Componentes novos começam em Dynamic; cenas antigas sem os campos novos são bloqueadas, conforme decisão posterior de Helena.
- A política é uma propriedade do viewport, selecionada pelo mecanismo existente de overrides de propriedade por plataforma.
- Nenhuma segunda cópia dessas propriedades é criada nos modos Cook e OnLoad.
- As propriedades atuais de tamanho, posição e estilo mantêm sua semântica de runtime nesta entrega.
- O cook não executa callbacks de gameplay e não altera os arquivos autorais.
- Cook sem métricas resolvidas falha identificando viewport e parâmetro; não troca de modo nem inventa resolução.
- Cook e OnLoad devem reter zero bytes de cópia de valores originais por entidade/componente.
- Reserva Dynamic: aproximadamente 6–7 KiB no modelo de 32 bits, a confirmar com ABI/alocador e população efetiva.
- A árvore de botões continua completa, independentemente das cenas empacotadas.
- Não inclui variantes automáticas de cena por resolução, implementação de SNES ou refatorações independentes dos renderers.
- Checkouts reais na main, sem worktree/export. Preservar as alterações locais da tarefa de caches e o arquivo pessoal do Visual Studio.
- Temporários exclusivamente em C:\temp; limpar os criados nesta tarefa. Saída final fixa por plataforma, sem histórico.
- Não editar C++ gerado. Executar testes contra as fontes/revisões atuais, sem DLL antiga do engine.
- Novos membros seguem as regras de documentação XML e organização dos respectivos repositórios.

## Review Focus

1. Leitura antecipada de bounds e ScenePrepared repetido: nenhum fit duplicado ou cache transitório em Cook/OnLoad (tarefas 2 e 4).
2. Remoção/substituição/reparenting com a mesma contagem: nenhum ponteiro antigo, nenhuma recaptura de valor já escalado (tarefa 3).
3. Overrides/blueprints e alvo diferente com o mesmo nome de perfil: cook correto e cache invalidado (tarefas 5 e 6).
4. Owners aninhados e edição em preview escalado: fronteiras de aplicação e dados autorais preservados (tarefas 3, 4 e 6).
5. Segundo frame e UI gerada: animação, rodapé e scroll permanecem corretos depois da preparação (tarefa 7).

## Mapa e interfaces comuns

Todos os caminhos abaixo são absolutos. Os novos arquivos são propostas de implementação deste plano.

- Core: C:\dev\helworks\helengine\engine\helengine.core\components\layout\ — matemática, métricas, aplicação e buffers opcionais.
- Persistência/editor/cook: mecanismos existentes de C:\dev\helworks\helengine\engine\helengine.editor\ e C:\dev\helworks\helengine\engine\helengine.managed\.
- Contrato do backend: C:\dev\helworks\helengine\engine\helengine.baseplatform\Builders\.
- Dependências: 1 → 2 → 3 → 4; 1 → 5; 2 + 4 + 5 → 6 → 7 → 8.
- Os tipos compartilhados com editor/cook são públicos; detalhes de buffers são internos. Uma classe/struct/enum por arquivo.
- Não adicionar campos de cache a toda Entity. Não criar um novo sistema de overrides de plataforma.

### Task 1: Política persistida e metadados cooked compatíveis

**Files:**
- Create: C:\dev\helworks\helengine\engine\helengine.core\components\layout\ViewportLayoutProcessingMode.cs
- Create: C:\dev\helworks\helengine\engine\helengine.core\components\layout\ViewportCookedLayout.cs
- Modify: C:\dev\helworks\helengine\engine\helengine.core\components\ViewportComponent.cs
- Test: C:\dev\helworks\helengine\engine\helengine.editor.tests\serialization\scene\ViewportLayoutProcessingPersistenceTests.cs
- Existing regression: C:\dev\helworks\helengine\engine\helengine.editor.tests\serialization\scene\ComponentPersistenceRegistryTests.cs

**Interfaces:**
- Produce enum ViewportLayoutProcessingMode : byte { Dynamic = 0, OnLoad = 1, Cook = 2 }.
- Produce property ViewportLayoutProcessingMode ViewportComponent.LayoutProcessingMode, append ordinal 0.
- Produce property ViewportCookedLayout ViewportComponent.CookedLayout, append ordinal 1, hidden in inspector.
- ViewportCookedLayout is a nullable reference allocated only for cooked owners: AlgorithmVersion, TargetWidth/Height, PixelAspect, Dpi/HasDpi, UiScale, ScaleX/Y, GlyphScale, Origin and AnchorSize. No per-participant data.
- Reuse ScenePersistenceAppendAttribute and EditorPropertyHiddenAttribute. Before preparation, permit setting policy; after preparation reject a different value with reload diagnostic.
- Authored saves omit/reset cooked metadata; packaged serialization deliberately includes it. Inspector never offers cooked metadata as authoring input.

- [ ] Add tests OldPayload_DefaultsToDynamic, NamedPayload_PreservesMode, PlatformOverride_ChangesOnlyTarget and CookMetadata_DoesNotLeakIntoAuthoredSave. Assert byte values 0/1/2; absent suffix yields Dynamic and null metadata; Common remains unchanged after PS1 override.
Pinned assertions:
~~~csharp
Assert.Equal((byte)0, (byte)ViewportLayoutProcessingMode.Dynamic);
Assert.Equal((byte)1, (byte)ViewportLayoutProcessingMode.OnLoad);
Assert.Equal((byte)2, (byte)ViewportLayoutProcessingMode.Cook);
Assert.Null(new ViewportComponent().CookedLayout);
~~~

- [ ] Run: dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --artifacts-path C:\temp\viewport-layout\windows --filter FullyQualifiedName~ViewportLayoutProcessingPersistenceTests. Expect failures for the missing public contract.
- [ ] Implement the two types/properties through the existing persistence registry. Require the complete current payload; append ordering does not make members optional. Reject unknown enum values during preparation.
- [ ] Repeat the test command and the ComponentPersistenceRegistryTests filter. Require all selected tests passing.
- [ ] Commit only this task's named files: "feat: persist viewport layout processing policy".

### Task 2: Matemática compartilhada e aplicação sem snapshots

**Files:**
- Create: C:\dev\helworks\helengine\engine\helengine.core\components\layout\ViewportLayoutMetrics.cs
- Create: C:\dev\helworks\helengine\engine\helengine.core\components\layout\ViewportLayoutMath.cs
- Create: C:\dev\helworks\helengine\engine\helengine.core\components\layout\ViewportLayoutApplier.cs
- Test: C:\dev\helworks\helengine\engine\helengine.core.tests\components\ViewportLayoutApplierTests.cs
- Reuse: C:\dev\helworks\helengine\engine\helengine.core\utils\DisplayAspectFitUtils.cs

**Interfaces:**
- ViewportLayoutMetrics is a value struct: ScaleX/Y, GlyphScale, Origin, AnchorSize, PixelAspect.
- ViewportLayoutMetrics ViewportLayoutMath.Resolve(int scalingMode, int2 referenceSize, int2 targetSize, double pixelAspect, double uiScale, double dpi, double referenceDpi).
- void ViewportLayoutApplier.ApplyComponent(Component component, ViewportLayoutMetrics metrics).
- float3 ViewportLayoutApplier.ResolvePosition(float3 authoredPosition, ViewportLayoutMetrics metrics).
- The applier accepts detached components too, never captures originals, discovers no scene references and allocates no cache. Owner traversal/anchor ordering belongs to task 4.
- NoScaling is identity; Density requires resolved positive DPI. Preserve existing integer rounding/minimum, glyph native-pixel mode and axis scaling semantics.

- [ ] Add Stretch_UsesIndependentAxes, Fit_UsesLetterboxOrigin, NonSquarePixels_PreserveGlyphScale, Density_UsesDpi, NativePixelFont_PreservesScale and NoScaling_IsIdentity.
- [ ] Pin results: reference1280×720 → target320×240 Stretch: position(384,90)→(96,30), size512×96→128×32. Fit canvas320×180, origin(0,30). Reference320×240 → target256×240 with pixelAspect1.25: size100×40→80×40, GlyphScale=1. Density320/160 → factor2. Native font scale remains1.
Pinned Stretch assertions:
~~~csharp
ViewportLayoutMetrics metrics = ViewportLayoutMath.Resolve(
    2, new int2(1280, 720), new int2(320, 240), 1, 1, 160, 160);
Assert.Equal(new float3(96, 30, 0),
    ViewportLayoutApplier.ResolvePosition(new float3(384, 90, 0), metrics));
~~~

- [ ] Run: dotnet test C:\dev\helworks\helengine\engine\helengine.core.tests\helengine.core.tests.csproj --artifacts-path C:\temp\viewport-layout\windows --filter FullyQualifiedName~ViewportLayoutApplierTests. Expect missing-type failures.
- [ ] Implement shared math and direct application for Layout, RoundedRect, Text, Sprite, ClipRect, Interactable, Scroll and FPS. Position is a separate operation; do not multiply a layout-owned anchored position twice.
- [ ] Repeat tests; require passing. Add no independent math implementation to cook.
- [ ] Commit named files: "refactor: share viewport layout calculation and application".

### Task 3: Buffers tipados opcionais e invalidação estrutural

**Files:**
- Create: C:\dev\helworks\helengine\engine\helengine.core\components\layout\ViewportLayoutCache.cs
- Create: C:\dev\helworks\helengine\engine\helengine.core\components\layout\ViewportLayoutInvalidation.cs
- Create: C:\dev\helworks\helengine\engine\helengine.core\components\layout\ViewportPositionState.cs
- Create: C:\dev\helworks\helengine\engine\helengine.core\components\layout\ViewportAnchorState.cs
- Create: C:\dev\helworks\helengine\engine\helengine.core\components\layout\ViewportRectangleState.cs
- Create: C:\dev\helworks\helengine\engine\helengine.core\components\layout\ViewportTextState.cs
- Create: C:\dev\helworks\helengine\engine\helengine.core\components\layout\ViewportSpriteState.cs
- Create: C:\dev\helworks\helengine\engine\helengine.core\components\layout\ViewportClipState.cs
- Create: C:\dev\helworks\helengine\engine\helengine.core\components\layout\ViewportInteractableState.cs
- Create: C:\dev\helworks\helengine\engine\helengine.core\components\layout\ViewportScrollState.cs
- Create: C:\dev\helworks\helengine\engine\helengine.core\components\layout\ViewportFpsState.cs
- Modify: C:\dev\helworks\helengine\engine\helengine.core\Entity.cs
- Modify: C:\dev\helworks\helengine\engine\helengine.editor\serialization\scene\SceneViewportAuthoredLayoutScope.cs
- Test: C:\dev\helworks\helengine\engine\helengine.core.tests\components\ViewportLayoutCacheTests.cs
- Test: C:\dev\helworks\helengine\engine\helengine.editor.tests\serialization\scene\ViewportAuthoredLayoutSaveTests.cs

**Interfaces:**
- ViewportLayoutCache Capture(Entity owner); void Reconcile(Entity owner); void Apply(ViewportLayoutMetrics metrics); void RestoreAuthoredLayout(); void Release().
- Capture is static; other methods belong to the returned ViewportLayoutCache. Reconcile retains originals for unchanged participants.
- void ViewportLayoutInvalidation.BeforeDetach(Entity subtree); void BeforeDetach(Component component); void AfterAttach(Entity subtree); void AfterAttach(Component component).
- Typed state structs contain only the owning entity/component reference and relevant original fields. Dense buffers sized to participants, no per-record object allocation.
- BeforeDetach restores transferable authored state and removes references before native destruction; AfterAttach marks affected ancestors/subtree owners dirty. Mutation-time traversal is permitted; frame-time subtree counting is not.
- Keep editor last-applied/edit reconciliation as optional editor-only state. Do not change runtime's existing original-baseline behavior when gameplay writes a resolved property.

- [ ] Add SameCountComponentReplacement_DropsOldReference, ReparentBetweenScaledOwners_DoesNotCompound, RemoveBeforeDestroy_ReleasesNativeReference, IdleUpdate_DoesNotTraverse and RuntimeWrites_KeepOriginalResizeBaseline.
- [ ] Add scaled-preview edit/save/clone tests: edit one resolved text size, save authoring values, reapply preview, then reload; nested owner results equal original authored values plus the deliberate edit.
- [ ] Run core filter FullyQualifiedName~ViewportLayoutCacheTests and editor filter FullyQualifiedName~ViewportAuthoredLayoutSaveTests using the projects/artifacts path from tasks 1–2. Expect missing-cache/behavior failures.
- [ ] Implement the typed buffers and cold mutation hooks. Discard removed records immediately; discover new component identities even when entity count is unchanged. Reuse unchanged records without recapturing scaled values.
- [ ] Repeat both commands; also run FullyQualifiedName~ViewportRuntimeAuthoredLayoutTests to pin existing semantics. Require passing, including replacement/reparent/destruction cases.
- [ ] Commit named task files: "refactor: retain optional typed viewport layout state".

### Task 4: Coordenação runtime, owners aninhados e fit legado

**Files:**
- Modify: C:\dev\helworks\helengine\engine\helengine.core\components\ViewportComponent.cs
- Modify: C:\dev\helworks\helengine\engine\helengine.core\components\ReferenceCanvasFitComponent.cs
- Modify: C:\dev\helworks\helengine\engine\helengine.core\scene\runtime\RuntimeSceneLoadOperation.cs
- Delete after migrating callers: C:\dev\helworks\helengine\engine\helengine.core\components\ViewportLayoutSnapshot.cs
- Delete after migrating callers: C:\dev\helworks\helengine\engine\helengine.core\components\ReferenceCanvasFitSnapshot.cs
- Test: C:\dev\helworks\helengine\engine\helengine.core.tests\components\ViewportLayoutProcessingTests.cs
- Existing regression: C:\dev\helworks\helengine\engine\helengine.editor.tests\ViewportAndAnchorLayoutTests.cs and C:\dev\helworks\helengine\engine\helengine.editor.tests\WindowScopedLayoutTests.cs

**Interfaces:**
- Preserve internal void ViewportComponent.PrepareSceneLayout() and the existing IAuthoredLayoutOwner contract.
- Add internal void ViewportComponent.PrepareAttachedSubtree(Entity subtree) for newly prepared runtime instances. Call it from structural/preparation lifecycle, never from an idle full-tree scan.
- Consume task 2's metrics/applier and task 3's cache. Runtime state machine: Unprepared → PreparedDynamic / PreparedOnLoad / PreparedCook.
- Dynamic allocates cache only when preparing. OnLoad applies once directly. Cook validates metadata/version/target and initializes bounds without applying values. Reject missing/incompatible cooked metadata.
- Bounds getters are side-effect free with respect to applying layout. Preparation completes before ScenePrepared callbacks.
- Nested viewport begins a separate owner. Legacy ReferenceCanvasFit uses the enclosing viewport's effective policy; standalone legacy fit defaults to Dynamic. It shares math/cache and cannot apply the enclosing owner's fit twice.
- Editor preview uses optional typed authoring state regardless of eventual platform policy.

- [ ] Add a mode matrix asserting identical resolved geometry at the same target. Bounds read before/after preparation and repeated ScenePrepared do not change geometry.
- [ ] Add CookAndOnLoad_NeverConstructOriginalCache with a test observer at ViewportLayoutCache.Capture; assert count0 throughout preparation, generated UI and idle updates. Retained-cache count alone is insufficient.
- [ ] Add Dynamic_ResizeReusesOriginals, OnLoad_ResizeDoesNotReapply, NewRuntimeSubtree_AppliesOnlyOnce, MixedNestedPolicies_RespectBoundary, LegacyFit_UsesOwnerPolicy and PolicyChangeAfterPreparation_RequiresReload.
- [ ] Run core filter FullyQualifiedName~ViewportLayoutProcessingTests; expect failures. Implement the state machine and remove eager snapshot lists/counting. Remove obsolete classes only once all callers use the new mechanism.
- [ ] Repeat new tests and existing ViewportRuntimeAuthoredLayoutTests/ViewportDensityTests/editor layout filters. Require passing; verify diagnostics distinguish missing cook metadata from unsupported runtime target changes.
- [ ] Commit named files: "feat: execute viewport layout by processing policy".

### Task 5: Alvo efetivo do backend e seleção de Play antes do cook

**Files:**
- Create: C:\dev\helworks\helengine\engine\helengine.baseplatform\Builders\IPlatformLayoutTargetProvider.cs
- Create: C:\dev\helworks\helengine\engine\helengine.baseplatform\Builders\PlatformLayoutTargetRequest.cs
- Create: C:\dev\helworks\helengine\engine\helengine.baseplatform\Builders\PlatformLayoutTarget.cs
- Modify: C:\dev\helworks\helengine\engine\helengine.editor\managers\play\EditorPlayLaunchSettingsResolver.cs
- Modify: C:\dev\helworks\helengine\engine\helengine.editor\managers\play\EditorPlaySessionService.cs
- Modify: C:\dev\helworks\helengine-windows\builder\WindowsPlatformAssetBuilder.cs and C:\dev\helworks\helengine-windows\builder\WindowsRuntimeNativeManifestWriter.cs
- Modify: C:\dev\helworks\helengine-ps1\builder\PlayStation1PlatformAssetBuilder.cs
- Test: C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\PlatformLayoutTargetTests.cs
- Test: C:\dev\helworks\helengine-windows\builder.tests\WindowsRuntimeNativeManifestWriterTests.cs
- Test: C:\dev\helworks\helengine-ps1\tests\helengine.ps1.builder.tests\PlatformLayoutTargetTests.cs

**Interfaces:**
- PlatformLayoutTarget IPlatformLayoutTargetProvider.ResolveLayoutTarget(PlatformLayoutTargetRequest request).
- Request: BuildProfileId, GraphicsProfileId, effective IReadOnlyDictionary<string,string> GraphicsOptionValues and selected PlatformVideoMode (nullable when backend has no catalog).
- Target: Width, Height, PixelAspect, HasDpi, Dpi, UiScale. No invented DPI; missing knowledge is explicit.
- PlatformVideoMode EditorPlayLaunchSettingsResolver.ResolveVideoMode(EditorPlaySettingsSnapshot snapshot); reuse this selection before cook and in final launch settings. Full launch paths still resolve after cook.
- Windows provider and manifest writer consume the same required default-width/default-height option parsing. PS1 provider returns the actual existing 320×240 NTSC progressive framebuffer; no new video modes or resolution changes.
- Existing PlatformVideoModeCapability remains authoritative when present. A provider is an optional backend capability; Cook on an unsupported backend reports unresolved metrics. OnLoad/Dynamic remain usable.

- [ ] Add SameProfileDifferentOptions_ResolveDifferentTargets, PlayAndCook_UseSameVideoSelection, WindowsManifest_EqualsCookTarget, Ps1Target_Matches320x240 and UnknownDensity_IsNotInvented.
- [ ] Run the new engine editor filter and relevant backend projects against freshly compiled engine assemblies. Expect missing-provider failures. Determine/pass EngineBinaryRoot explicitly for PS1 tests; never accept their stale default editor path.
- [ ] Implement provider types/adapters and factor pre-cook video selection. Do not make the request depend on PlatformBuildRequest, which is produced after asset cooking.
- [ ] Repeat tests. Require exact Windows option parity and PS1 dimensions320×240; square-pixel aspect follows the current PS1 presentation contract. Fail Density cook if DPI remains unresolved.
- [ ] Commit each repository's named task files separately: "feat: resolve effective viewport cook target".

### Task 6: Passe de cook, compatibilidade ordinal e chave de cache

**Files:**
- Create: C:\dev\helworks\helengine\engine\helengine.editor\managers\project\ViewportLayoutCookProcessor.cs
- Modify: C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorWindowsBuildScenePackager.cs (class EditorPlatformBuildScenePackager)
- Modify: C:\dev\helworks\helengine\engine\helengine.editor\managers\project\SceneComponentPackagingTransformService.cs
- Modify: C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorPlatformAssetCookService.cs
- Modify: C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorScenePackageCache.cs
- Preserve strict exact-count reads in C:\dev\helworks\helengine\engine\helengine.managed\scene\runtime\AutomaticScriptComponentRuntimeDeserializer.cs and C:\dev\helworks\helengine\engine\helengine.editor\managers\project\ScriptComponentPlayerDeserializerGenerator.cs; no optional suffix support.
- Test: C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\ViewportLayoutCookTests.cs
- Test: C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorScenePackageCacheKeyTests.cs

**Interfaces:**
- void ViewportLayoutCookProcessor.Process(SceneAsset packagedScene, PlatformLayoutTarget target), using existing descriptor registry and reference resolver supplied to its constructor.
- const int ViewportLayoutCookProcessor.AlgorithmVersion = 1.
- Add PlatformLayoutTarget effectiveLayoutTarget to EditorScenePackageCache.ComputeKey; hash normalized target metrics, effective policy and AlgorithmVersion with existing asset/profile inputs.
- Cook ordering: cloned scene → prune/expand/prune → effective named component overrides → layout processing → ordinal payload rewrite → final reference validation/write.
- Deserialize only detached participants through registered descriptors; preserve component keys, entity references and unknown components. Never instantiate a running Core or invoke ComponentAdded/ScenePrepared.
- Apply metadata only to Cook owners. Resolve camera binding/rect from the effective packaged scene; fail with owner identity and unresolved parameter when not statically known.
- Fresh dynamic instances of separately packaged assets follow their own declared policy; never reinterpret already cooked values as authored input.

- [ ] Add Cook_EqualsOnLoadAndDynamic for Stretch/Fit/Density/non-square pixels, OverridesAndBlueprints_AreResolvedFirst, NestedOwners_ProcessOnce and UnknownCameraOrDpi_IdentifiesViewport.
- [ ] Add Cook_DoesNotInvokeGameplayOrChangeSource: byte-identical authoring asset after cook, callback counter0, no original-cache construction.
- [ ] Add cache-key tests: same profile ID with changed target width, pixel aspect, DPI/UI scale, effective mode or algorithm version produces a different key. Clean and incremental packaged scene bytes agree.
- [ ] Add managed/generated-native old/new payload tests: old or incomplete payloads are rejected, cooked metadata round-trips, truncated suffix errors remain explicit. Named authoring payloads must contain the new layout fields. Reject unsupported algorithm versions.
- [ ] Run editor filters FullyQualifiedName~ViewportLayoutCookTests and FullyQualifiedName~EditorScenePackageCacheKeyTests; expect missing processor/key behavior. Implement ordering and shared applier use.
- [ ] Repeat tests and existing EditorWindowsBuildScenePackagerTests. Generate/compile native deserializers through the actual build pipeline; no hand-patched generated code. Require passing before advancing.
- [ ] Commit named files: "feat: cook viewport layout with effective platform metrics".

### Task 7: Consumidores, animação e configuração do DemoDisc

**Files:**
- Modify preserving prior caches: C:\dev\helprojs\demodisc\assets\codebase\menu\MenuComponent.cs
- Modify preserving prior caches: C:\dev\helprojs\demodisc\assets\codebase\menu\FooterIdentityMarqueeComponent.cs
- Modify if needed for effective row metrics: C:\dev\helprojs\demodisc\assets\codebase\menu\MenuPanelComponent.cs
- Modify via official scene persistence: C:\dev\helprojs\demodisc\assets\scenes\DemoDiscMainMenu.helen
- Modify: C:\dev\helprojs\demodisc\project.heproj
- Create via official platform settings: C:\dev\helprojs\demodisc\settings\platform.ps1.json
- Modify: C:\dev\helworks\helengine\engine\helengine.core\components\AnimationPlayerComponent.cs
- Modify preserving prior cache: C:\dev\helworks\helengine\engine\helengine.core\components\2d\interactable\ScrollComponent.cs
- Test: C:\dev\helworks\helengine\scripts\demodisc-menu\runtime-cache\RuntimeCacheTests.cs
- Test: C:\dev\helworks\helengine\engine\helengine.core.tests\components\ViewportLayoutAnimationTests.cs

**Interfaces:**
- Consumers keep existing viewport/legacy provider references and AnchorBoundsChanged notification.
- Footer uses effective shared ScaleX for its custom unbaked StripWidth/speed; text width and baked UI sizes are not scaled again.
- Menu navigation/scroll/hit test derive row step from prepared UI geometry rather than assuming the authored value.
- AnimationPlayer's existing transform channel evaluation composes with the nearest layout owner's prepared metrics through task 2's ResolvePosition. Cook does not rewrite shared animation clips.
- Runtime-generated scrollbars derive sizes from resolved scroll/clip bounds; no original-cache path in Cook/OnLoad.
- Asset configuration: Common Dynamic for desktop/editor resize; PS1 override Cook. Preserve every existing override, every menu entry and all prior cache changes. Restore ps1 in supportedPlatforms and its missing platform settings using the existing project configuration path, selecting release and menu +14 rendering scenes.

- [ ] Add MenuModes_KeepAll44Buttons, CookedFooter_DoesNotDoubleScale, ScrolledPointer_UsesResolvedRowStep and GeneratedScrollbars_DoNotCaptureOriginals.
- [ ] Add animation assertions at preparation, first update and a later keyframe; two instances sharing the same clip under different owners produce their respective resolved positions while clip bytes remain unchanged. Include a non-UI instance of that clip.
- [ ] Run: dotnet test C:\dev\helworks\helengine\scripts\demodisc-menu\runtime-cache\RuntimeCacheTests.csproj --artifacts-path C:\temp\viewport-layout\windows. Also run core filter FullyQualifiedName~ViewportLayoutAnimationTests. Expect failures specific to new policy paths.
- [ ] Implement only the consumer adaptations above. Serialize the scene through official tooling, not binary patching. Preserve resolved entity references, input device aggregation and cached clip validation from prior work.
- [ ] Repeat both commands and editor ScrollComponentTests. Require passing; confirm authored scene still has all44 menu items and packaging selection remains menu +14 rendering scenes.
- [ ] Commit engine and DemoDisc named changes separately: "fix: preserve menu behavior with prepared viewport layout".

### Task 8: Builds reais, profiler e orçamento de memória

**Files:**
- Update evidence: C:\dev\helworks\helengine\docs\superpowers\plans\2026-10-18-viewport-layout-processing.md
- No new production feature. Reuse actual build/profiler infrastructure; probes/logs only C:\temp\viewport-layout.

**Interfaces:**
- Fixed Windows package output C:\dev\helworks\builds\windows\package.
- Fixed PS1 package output C:\dev\helworks\builds\ps1\package.
- Use the chosen execution method's fresh final reviewer after relevant tests; review spec compliance and changes, not unrelated local files.
- No screenshot or emulator launch without authorization required by PS1 AGENTS.md. Finish build and collect available static evidence before requesting launch permission.

- [x] Run the relevant core/editor/menu/backend test filters from earlier tasks; check each command exit code. Regenerate/recompile affected shared consumers through normal tooling. List actual successful targets and any blocked target explicitly.
- [x] Build Windows in the existing optimized Profiler profile with Tracy and PDB: powershell -File C:\dev\helworks\helengine\scripts\build-platform.ps1 -Project C:\dev\helprojs\demodisc -Platform windows -Output C:\dev\helworks\builds\windows\package -Configuration Release -BuildProfile profiler -CacheRoot C:\temp\helengine. Inspect selected profile before running; profile IDs must match the project without silently selecting another profile.
- [x] Build PS1 from its actual checkout: powershell -File C:\dev\helworks\helengine\scripts\build-platform.ps1 -Project C:\dev\helprojs\demodisc -Platform ps1 -Output C:\dev\helworks\builds\ps1\package -Configuration Release -BuildProfile release -CacheRoot C:\temp\helengine. The current backend declares only release; do not invent a profiler profile or label this a profiler build.
- [ ] Verify packaged menu +14 rendering scenes and complete menu entries. Compare clean/incremental scene payloads using the same output path, storing only temporary comparison hashes in C:\temp.
- [ ] Measure on actual generated ABI: sizeof typed records, buffer capacity/count, retained original bytes, preparation peak and idle update/traversal cost. Cook/OnLoad assert original-cache allocation count0 from construction onward; do not infer that from a post-GC snapshot.
- [ ] With launch authorization, run the fixed Windows/PS1 packages. Collect Windows Tracy component/entity costs, not only global FPS. Check whether PS1 exports usable timing/allocation counters; if absent, report PS1 measurement as blocked and obtain architectural feedback before adding profiling infrastructure. Verify scroll, footer, logo after multiple frames, input and resizing policy. Windows Dynamic must resize; PS1 Cook must show correct prepared bounds with zero original-value cache.
- [x] Record actual RAM and timing values with build/profile/context. Treat6–7 KiB as estimated Dynamic budget until measured; if exceeded, report the breakdown and cause instead of hiding it with a mode change.
- [ ] Complete final review, git diff --check and task-only commits. Remove own temporary probes/logs after summarizing evidence. Do not push without current authorization; do not delete unrelated user files.

## Self-review / handoff

- Spec coverage: policy/persistence1; shared math2; optional retention/editor mutations3; runtime/legacy ownership4; effective presentation5; cook/cache/native schema6; consumers/config7; ABI/profiler/real builds8.
- Five review-focus cases have named tests in the owning tasks. No idle tree count or hidden create-and-discard original cache is acceptable.
- Cross-task types/signatures above are the contract. Any change that alters the approved ownership or mode behavior returns to Helena for architectural feedback.
- Before editing another repository, recheck branch/status and preserve the explicitly known local work. Unexpected other-session edits in a touched path require stopping and asking.
- Recommended execution: Native, because cook, runtime, persistence and consumers share tightly ordered interfaces. Execução realizada nos checkouts reais; resultados e limites registrados na evidência abaixo.

## Evidência de execução — 2026-10-18

Estado: tarefas 1–7 implementadas e revisadas; tarefa 8 ainda depende do resultado final da build PS1 e da validação em emulador. Não houve push.

Revisões usadas: engine da122189, conversor 606d09e5 (master autorizada), Windows 5fb0feb, PS1 269713a e DemoDisc 80dfbdf. Os demais checkouts estão na main. As alterações pessoais não rastreadas foram preservadas.

### Windows: build e execução confirmadas

- Build oficial terminou com exit code 0; Profiler otimizado, TRACY_ENABLE, profiling das funções geradas e PDB. Saída fixa: C:\dev\helworks\builds\windows\package. Executável C:\dev\helworks\builds\windows\package\helengine_windows.exe: 6.515.200 bytes; PDB: 98.783.232 bytes; ambos gerados em 2026-10-18 22:17:17 UTC.
- Seleção efetiva: menu primeiro e 14 cenas de rendering. Os 44 botões continuam presentes, inclusive os de cenas não empacotadas.
- Helena confirmou menu com logo/textos corretos e, na build final, Cube Test aberto e rodando sem BaseColorBuffer. A causa era ownership: as listas locais de MaterialLayoutBuilder liberavam bindings ainda referenciados pelo MaterialLayout; três transferências DetachOwned corrigem a origem. Contratos de layout/ownership: 7/7.
- Captura Tracy real de 30,57 s; descartados 2 s iniciais. 6.854 amostras de componentes/frame, 149 entidades registradas, uma câmera, cinco componentes com Update em quatro entidades. A build PS1 estava em segundo plano; não existe baseline comparável antes/depois.

| Entidade / componente | Média por frame | P95 |
| --- | ---: | ---: |
| 14 DemoDiscOverlayImage / AnimationPlayerComponent | 3,184 µs | 5 µs |
| 11 DemoDiscMenuRoot / MenuComponent | 1,977 µs | 3 µs |
| 25 Panel-main-ItemsRoot / ScrollComponent | 1,952 µs | 3 µs |
| 18 DemoDiscFooterIdentitySurface / FooterIdentityMarqueeComponent | 0,349 µs | 1 µs |
| 11 DemoDiscMenuRoot / ViewportComponent | 0,165 µs | 1 µs |

Update completo: média 0,030597 ms, P95 0,043222 ms. Draw: média 0,157468 ms, P95 0,224457 ms. Frame real: média 4,168086 ms, P95 4,304638 ms; aproximadamente 240 FPS. Present bloqueia cerca de 3,972 ms e está contido no pacing: não somar os dois. O relógio de componentes tem resolução de 1 µs; médias abaixo disso não são precisão individual submicrossegundo. A ausência de escopos frios na política coarse não prova ausência de travessia; essa propriedade é validada pelo código e pelos testes.

### Memória: ABI real versus reconstrução

- ABI Windows x64 dos registros, em bytes: posição24, anchor24, retângulo24, texto32, sprite16, clip16, interactable16, scroll24 e FPS24; cache88; metadata Cook96.
- ABI MIPS do SDK PS1 compilada e inspecionada com nm: ponteiro4; posição16, anchor20, retângulo20, texto28, sprite12, clip12, interactable12, scroll16 e FPS16; cache44; metadata Cook88.
- Cook/OnLoad não constroem cache de originais desde a preparação, validado pelos testes. Os 88 bytes medidos no PS1 são o objeto de metadata por viewport, não o custo total do componente/alocador. Não há medição de pico de heap nativo nesta evidência.
- Reconstrução Dynamic Windows com structs e crescimento reais do vetor: 127 posições (cap141), 24 anchors (cap28), 54 retângulos (cap63), 47 textos (cap63), dois sprites (cap2), sete clips (cap9) e sete scrolls (cap9). Valores6736 + folga1240 + nove wrappers/cache448 = 8424 bytes pedidos. Exclui cabeçalhos do alocador e entidades geradas em runtime; não é uma medição do heap vivo. A estimativa inicial de 6–7 KiB não vale como medição x64.

### Validações e limites

- Core completo: 1290/1290; menu: 15/15; leitor estrito/gerador: 40/40; mutações de provider: 6/6; último recorte de editor autoral/perfis: 20/20. A validação expandida por blueprints das 15 cenas encontrou zero campos de layout ausentes. Cenas antigas continuam bloqueadas, sem fallback de schema.
- Duas asserções de projeção com igualdade exata de float observaram 5,7735033 versus 5,773503; controllers byte-inalterados desde a base. Não foram mascaradas nem atribuídas definitivamente à base.
- Formatter portátil: MSVC2026, GCC e EASTL com headers hospedados bloqueados passaram 24.576 comparações por configuração e contratos explícitos; compilação MIPS real passou. NaN de sinalização usa a política portátil nan/NAN, sem a decoração específica de UCRT. Não houve exclusão de casos finitos. A suite ampla do conversor tem bloqueios independentes de reverse_iterator e construção de std::logic_error, registrados sem alteração.
- Runner PS1: testes C#7/7 e mounts Python4/4. Cache externo, outputs fixos e objetos candidatos temporários separados. Kernel nativo e 588 objetos candidatos já compilaram; linkedição, orçamento retail e pacote final ainda pendentes neste registro.
- Revisão final independente cobriu implementação, ownership, caminhos e formatter. Cook rejeita explicitamente CheckBox/TextBox/ComboBox gerados em runtime; esses hosts continuam em Dynamic/OnLoad e não aparecem no menu.

Pendências da tarefa 8: build PS1 final e seu orçamento, lançamento autorizado pelo MCP e QA; comparação limpa/incremental dos payloads no mesmo output; pico de alocação nativo não medido. O PS1 dispõe somente de Release nesta execução, sem profiler: não há números de tempo PS1. Limpar apenas temporários próprios ao encerrar, preservando o cache compartilhado e arquivos alheios.
### PS1: build e primeiro teste nativo concluídos; logo em atualização

- A build oficial da revisão anterior ao ajuste do logo terminou com exit code 0. Saída fixa C:\dev\helworks\builds\ps1\package; entrada real C:\dev\helworks\builds\ps1\package\helengine_ps1.cue; disco C:\dev\helworks\builds\ps1\package\helengine_ps1.bin, 1.812.480 bytes, SHA256 0D3FAF55BCC710513474E6CE057585ABF89BEC929FEFE51EC69723A1A27190C7. O receipt confirmou passed=true, 15 cenas e nove módulos.
- Orçamento estático retail passou: imagem residente1.067.008 bytes, footprint estático com bancos1.343.488, heap reservado622.592, cache máximo133.654, saldo488.938 antes das alocações reais/overhead. Os três bancos medem18.432,114.688 e24.576 bytes. Reserva conservadora de runtime65.536; runtimeHeapMeasured=false. Não confundir o saldo com heap livre medido.
- DuckStation lançado pelo MCP conforme autorização anterior de teste PS1. O log TTY confirmou menu com uma câmera, cinco updateables, UI320×240, dois sprites e failure=none; depois encerrou normalmente. A confirmação visual explícita está pendente.
- Foram encontrados contadores de fases já existentes no Release, sem Tracy nem instrumentação nova. A janela seq1200 registrou update1651µs, boundary11889µs, draw10313µs e between10850µs; contém carregamento e não é um benchmark isolado. A linha STEADY_CADENCE confirmou600frames/1200vblanks, aproximadamente30FPS no NTSC. Portanto não houve confirmação de60FPS no PS1. Custos por componente PS1 e pico nativo de heap continuam sem medição.
- Comparação de cache Windows executou TryRestore oficial no mesmo output e verificou SHA256 antes/depois:123arquivos byte-idênticos, incluindo15cenas. No PS1,94payloads/15cenas coincidem com o manifesto imutável. Isso verifica restauração/identidade dos outputs atuais; não representa duas builds completas independentes.
- Nova solicitação de Helena: logo256×256 no indexado de menor memória. DemoDisc34b4a9c adiciona somente o override ps1 do sidecar C:\dev\helprojs\demodisc\assets\textures\menu\helengine-logo.png.hasset: MaxResolution256, Indexed4, Binary, QuantizedIndexed. Source2048×2048; cook real confirmou256×256,32.768bytes de índices e64bytes de paletaRGBA serializada; CLUT GPU16×2=32bytes. Nenhuma geometria do menu foi alterada e os registros das outras plataformas foram comparados/preservados. A rebuild oficial no mesmo output está em andamento para publicar essa revisão.
### Entrega do logo PS1 — revisão final

A rebuild oficial de DemoDisc34b4a9c terminou com exit code0. Disco final: C:\dev\helworks\builds\ps1\package\helengine_ps1.bin,1.839.104bytes, SHA256 A5A1DE6A503D0351CDFF52139CA640ADFC861F1562459D2C630B57DE91238C8D; entrada C:\dev\helworks\builds\ps1\package\helengine_ps1.cue. Receipt final passed=true/15cenas; cache máximo161.366bytes, aumento27.712; saldo estático após bancos/cache461.226bytes. Os tamanhos dos bancos e do residente permaneceram iguais. Isso é orçamento estático, não heap livre medido.

Reaberto pelo MCP no DuckStation, PID42944. O TTY confirmou a textura256×256/bpp4; o contador de VRAM subiu de2304 para18704words nesse upload:16.400words=32.800bytes (32.768índices+32CLUT). O menu chegou ao frame1200, uma câmera/cinco updateables/UI320×240, failure=none. O break do alocador estava em0x801d7b20, com início0x80158000:523.040bytes de span e99.552bytes até o teto0x801f0000. Esse span inclui regiões internas livres e não mede bytes de alocações vivas nem atribuição por componente. Helena confirmou que a definição do logo ficou boa; nenhuma captura de tela foi feita.

O sourcePNG2048×2048 e a geometria do sprite permanecem iguais. O ajuste é o cook PS1 do asset, com16cores incluindo transparência binária. Windows mantém sua configuração de textura. Não houve push. A comparação de cache e os perfis acima continuam válidos para o código de engine inalterado; a imagem PS1 antiga foi sobrescrita.
### Todo o menu PS1 em Indexed4 — solicitação seguinte

Helena aprovou a definição do logo e pediu todas as texturas do menu PS1 em Indexed4. A auditoria dos cinco payloads runtime confirmou: logo256×256/Indexed4, atlas de botões256×256/Indexed4, atlas do carrossel256×128/Indexed4, atlas do título256×128/Indexed4 e fundo64×64/Indexed8. O fundo foi o único asset que precisou mudar.

DemoDiscd2ca9a1 altera somente C:\dev\helprojs\demodisc\assets\textures\menu\main_menu_background.png.hasset. A resolução foi obtida do payload runtime vigente e comparada ao novo cook,64×64→64×64; alphaBinary preservado. O cook da fonte com o formato antigo reproduziu exatamente os pixels decodificados do fundo runtime, confirmando o asset correto. Indexed4 reduziu índices/paletaRGBA de5120 para2112bytes; na GPU,4608→2080bytes. Registros das outras plataformas preservados. As fontes já eram cozidas em4bpp e o logo continua256×256.

A revisão automática inicialmente rejeitou um comando que fixava64×64 sem comprovação suficiente de preservação. A alternativa aceita deriva as dimensões do payload atual, valida a igualdade antes/depois e só então grava o formato; nenhuma redução de resolução ocorreu. Nova rebuild oficial no mesmo output em andamento; a publicação anterior não é apresentada como contendo essa última alteração.
### Todo o menu PS1 em Indexed4 — build e runtime verificados

Build oficial final de DemoDiscd2ca9a1: exit code0. Entrada C:\dev\helworks\builds\ps1\package\helengine_ps1.cue; disco C:\dev\helworks\builds\ps1\package\helengine_ps1.bin,1.837.056bytes, SHA256 8155CCA43FC654E131C0AACCD00C0F4FA10E15D8EE8FBF6801DAE8A83FA3E24D. Receipt passed=true,15cenas. RAM gate passou: cache máximo158.358bytes, saldo estático após bancos/cache464.234; não é uma medição de heap livre.

A auditoria dos payloads finais exigiu e confirmou Indexed4 nos cinco assets: fundo64×64, logo256×256, botões256×256, carrossel256×128 e título256×128. Todas as dimensões anteriores foram preservadas. Nenhuma textura de outro menu/plataforma ou cena de rendering teve seu formato alterado.

DuckStation reaberto pelo MCP, PID28468. Os cinco uploads TTY são bpp4, com contador acumulado50256words=100.512bytes de VRAM, incluindo as CLUTs. O fundo usa1040words=2080bytes, economizando2528bytes de VRAM frente ao Indexed8. O menu atingiu frame1200 com uma câmera/cinco updateables/UI320×240 e failure=none. Heap break0x801d6f70 desde0x80158000:520.048bytes de span; não corresponde a bytes de alocações vivas por componente. Nenhuma captura de tela foi feita. O último contador steady da revisão anterior era600frames/1200vblanks (~30FPS); não se declarou60FPS nem ganho de desempenho decorrente apenas desta mudança de formato.

A definição do logo256×256 foi aprovada visualmente por Helena antes deste último ajuste do fundo. A validação automática final cobre build, dimensões, formato dos cinco payloads, uploads e ausência de falha no log. Pico nativo de alocações e custos por componente PS1 permanecem não medidos; as duas falhas de projeção float observadas e os bloqueios independentes da suite ampla do conversor continuam explicitamente registrados acima. Sem push.
### Menu indexed nas seis plataformas — escopo confirmado

Helena confirmou menu +14 cenas de rendering para PS1, N64, Dreamcast, GameCube, PS2 e Xbox original, mantendo os44 botões e sem abrir emuladores. A solicitação substitui o fundo PS1 Indexed4 anterior: fundos agora usam indexed8 nativo. Logo e três atlas usam indexed4 nativo nos cinco consoles que o suportam; Xbox usa seu P8 nativo. Portáteis e plataformas posteriores permanecem intactos. Resoluções e rasterização das fontes preservadas; comparação dos registros excluídos executada pelo serializer oficial antes de gravar os cinco sidecars. SNES permanece excluído: seu repositório atual é um ROM diagnóstico sem integração de menu/cook.

O formato GameCube RGB5A3 usa AlphaPrecision.A3 já existente. A serialização de import settings rejeitava esse enum; corrigida em helengine ae265d39 sem novo schema/ABI, com13 testes focados aprovados e rejeição de enums inválidos preservada. Auditoria dos30 ajustes (cinco assets ×seis plataformas) passou após a correção.

Builds oficiais no checkout real e main; temporários sob C:\temp\viewport-layout por plataforma, cache compartilhado C:\temp\helengine. Os pacotes aprovados serão copiados para C:\dev\helworks\builds\<plataforma>\package, sobrescrevendo as saídas anteriores. Builders atuais compilados do source, registry temporário; nenhum registry instalado alterado. Xbox habilitado com seu runtime shader experimental já existente. Resultados finais ainda pendentes.

### Extensão de QA solicitada

Helena autorizou abrir os seis emuladores, navegar todas as14 cenas pelo HelenUI, criar perfis faltantes e salvar screenshots das cenas sem crash para revisão na manhã seguinte. Essa autorização substitui a instrução anterior de não abrir. Destino final C:\dev\helworks\builds\rendering-review, separado por plataforma; cada resultado deve distinguir execução confirmada, crash, travamento e reconhecimento inconclusivo. Perfis novos no checkout real main C:\dev\helenui; capturas/input exclusivamente HelenUI. Nenhuma captura genérica de desktop nem OCR externo. Os10 cenários raster em Release não têm títulos exclusivos; identidade precisa de rota reconhecida mais captura/log, não só HUD comum ou processo aberto.
