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

- [ ] Run the relevant core/editor/menu/backend test filters from earlier tasks; check each command exit code. Regenerate/recompile affected shared consumers through normal tooling. List actual successful targets and any blocked target explicitly.
- [ ] Build Windows in the existing optimized Profiler profile with Tracy and PDB: powershell -File C:\dev\helworks\helengine\scripts\build-platform.ps1 -Project C:\dev\helprojs\demodisc -Platform windows -Output C:\dev\helworks\builds\windows\package -Configuration Release -BuildProfile profiler -CacheRoot C:\temp\helengine. Inspect selected profile before running; profile IDs must match the project without silently selecting another profile.
- [ ] Build PS1 from its actual checkout: powershell -File C:\dev\helworks\helengine\scripts\build-platform.ps1 -Project C:\dev\helprojs\demodisc -Platform ps1 -Output C:\dev\helworks\builds\ps1\package -Configuration Release -BuildProfile release -CacheRoot C:\temp\helengine. The current backend declares only release; do not invent a profiler profile or label this a profiler build.
- [ ] Verify packaged menu +14 rendering scenes and complete menu entries. Compare clean/incremental scene payloads using the same output path, storing only temporary comparison hashes in C:\temp.
- [ ] Measure on actual generated ABI: sizeof typed records, buffer capacity/count, retained original bytes, preparation peak and idle update/traversal cost. Cook/OnLoad assert original-cache allocation count0 from construction onward; do not infer that from a post-GC snapshot.
- [ ] With launch authorization, run the fixed Windows/PS1 packages. Collect Windows Tracy component/entity costs, not only global FPS. Check whether PS1 exports usable timing/allocation counters; if absent, report PS1 measurement as blocked and obtain architectural feedback before adding profiling infrastructure. Verify scroll, footer, logo after multiple frames, input and resizing policy. Windows Dynamic must resize; PS1 Cook must show correct prepared bounds with zero original-value cache.
- [ ] Record actual RAM and timing values with build/profile/context. Treat6–7 KiB as estimated Dynamic budget until measured; if exceeded, report the breakdown and cause instead of hiding it with a mode change.
- [ ] Complete final review, git diff --check and task-only commits. Remove own temporary probes/logs after summarizing evidence. Do not push without current authorization; do not delete unrelated user files.

## Self-review / handoff

- Spec coverage: policy/persistence1; shared math2; optional retention/editor mutations3; runtime/legacy ownership4; effective presentation5; cook/cache/native schema6; consumers/config7; ABI/profiler/real builds8.
- Five review-focus cases have named tests in the owning tasks. No idle tree count or hidden create-and-discard original cache is acceptable.
- Cross-task types/signatures above are the contract. Any change that alters the approved ownership or mode behavior returns to Helena for architectural feedback.
- Before editing another repository, recheck branch/status and preserve the explicitly known local work. Unexpected other-session edits in a touched path require stopping and asking.
- Recommended execution: Native, because cook, runtime, persistence and consumers share tightly ordered interfaces. No implementation has started with this plan.
