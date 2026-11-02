# Helengine native lightmap tooling

Optional standalone C++17 module. The default CPU build has no core, voxel GI, Gevo, Windows, graphics API or desktop dependency. An optional tooling-only Vulkan backend accelerates irradiance tracing; runtime lightmap loading does not require it. The public entry point is `include/hellm/Lightmap.h`; each data type lives in its own header. Add this directory with CMake and link `helengine_lightmap` (alias of `helengine_lightmap_native`, also available as `hellm::lightmap`). Set `HELENGINE_LIGHTMAP_TESTS=OFF` to omit standalone tests in a consumer build; `HELLM_BUILD_CLI=OFF` omits the tool.

xatlas is vendored unmodified from official upstream commit `f700c7790aaa030e794b52ba7791a05c085faf0c`. Its MIT license and origin are in `third_party/xatlas/`. Building never downloads dependencies.

## Saved-source workflow

The runtime adapter exports one fully initialized `hellm::Scene` through `SaveScene`. This is the `export-input` step; it must retain a unique stable drawable/submesh key, exact world positions and normals, UV0, original triangle element order, linear base color, metallic fraction, perceptual roughness, optional explicit probe volume and the directional sun. Reused model instances are separate meshes. Optional diffuse texture pixels are linear RGB reflectance, top-to-bottom row-major, repeating with bilinear UV0 sampling; the adapter must reject unsupported material features before export.

Run the explicit tool on that saved snapshot. Ordinary rendering only calls `LoadLightmap`, compares `SourceFingerprint` with `ComputeSceneFingerprint(currentScene)`, validates every binding's key/count, uploads the atlas and binds `CornerUvs` in original triangle-element order. UV0 and original geometry are not modified. Apply the receiver diffuse base color and nonmetallic fraction to irradiance exactly once. Baked receivers bypass dynamic diffuse sun, voxel GI and SSGI; analytic specular sun remains a runtime term. The optional scene cube supplies material-prefiltered indirect/specular environment radiance.

Example Windows build and validation using the same fixed output directory:

```powershell
cmake -S C:\dev\helworks\helengine\engine\helengine.lightmap.native -B C:\temp\lightmap-windows -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_CXX_COMPILER=cl
cmake --build C:\temp\lightmap-windows --parallel 8
ctest --test-dir C:\temp\lightmap-windows --output-on-failure
C:\temp\lightmap-windows\helengine-lightmap.exe validate-scene C:\temp\Gevo.hlscene
C:\temp\lightmap-windows\helengine-lightmap.exe bake C:\temp\Gevo.hlscene C:\temp\Gevo.hellm --size 4096 --samples 128 --bounces 4 --padding 4 --sun-angle 0.53 --probe-size 128 --probe-samples 128 --probe-filter-samples 128
C:\temp\lightmap-windows\helengine-lightmap.exe inspect C:\temp\Gevo.hellm
```

`--workers N` controls scheduling; zero uses available logical processors, limited to 64. `--seed N` chooses the deterministic sampling sequence. `--samples` can be lowered for a preliminary check; this does not change the production default of 128. The CLI prints chart, raster, trace and dilation stages with counts/percentages, validates the completed asset after saving, and returns nonzero on failure.

## Optional Vulkan ray-query transport

Configure with `-DHELLM_ENABLE_VULKAN=ON`, `-DHELLM_VULKAN_INCLUDE_DIR=<directory containing vulkan/vulkan.h>` and `-DHELLM_GLSLC=<glslc executable>`. CMake compiles and embeds the shader; no external shader file is required at runtime. When headers come from an Android NDK, CMake stages only `vulkan/` and `vk_video/` under the fixed build output so Android C library headers cannot shadow the host compiler's headers. No dependency download is performed.

Select `--backend vulkan` on the CLI or `BakeOptions::Backend = BakeBackend::Vulkan`. The library/CLI default remains `cpu`; Gevo's bake script explicitly defaults to `vulkan`. The loader dynamically opens the platform Vulkan runtime. Required capabilities are Vulkan 1.2, acceleration structures, ray queries, buffer device addresses and shaderInt64, with a compute queue. Selection reports the actual device. Missing build support, runtime, features or memory capacity produces an explicit error, never a silent CPU fallback. Windows RTX 4090 is validated; other devices/platforms require their own validation.

Charts, exact double-precision clipping, fan/coverage areas and gutter dilation remain on CPU. Static world triangles, materials and linear texture pixels are uploaded once. Two-sided hardware triangle ray queries compute the same directional visibility and GGX metallic paths. The shader uses explicit IEEE32 GPU arithmetic and the same SplitMix64 stream per original atlas pixel; CPU/GPU bit identity is not required. Backend identity is included in the settings fingerprint. Transport version 2 intentionally invalidates old Lambertian settings identities; scene/lightmap version-2 writers include material and probe data while readers still accept version 1.

`--gpu-batch N` sets an upper bound on occupied texels per dispatch (default 4096, range 1..65536), further reduced by actual coverage work. A batch holds at most 65536 coverage records (14 MiB); path work is bounded by samples times bounce count times coverage records. A single texel exceeding this budget is rejected before dispatch with guidance to increase atlas resolution, reduce sampling or select CPU explicitly. Batch size does not change random streams or settings identity. Device submissions, allocations and readback are checked; negative/nonfinite output is rejected.

With Vulkan enabled, CTest runs both `hellm.correctness` and `hellm.vulkan`. The GPU suite repeats analytic, aperture/backface, tiny-primitive, diffuse/texture and zero-light assertions, compares CPU/GPU four-bounce output, checks exact batch repeatability and rejects an over-budget densely tessellated texel. The CLI reports end-to-end elapsed seconds including source loading, charts, transport, dilation, saving and checked reload.

## UV coverage and transport

xatlas segments charts from world geometry, retaining original-corner correspondence. Chart density is proportional to world surface area. All charts must fit one square atlas with explicit bilinear footprints and padded gutters; packing retries reduce one uniform world-space density rather than collapsing individual faces. Every output index is checked against xatlas's original vertex reference. Collapsed UV triangles, out-of-bounds triangles, overlapping UV triangles and chart sharing of a texel fail explicitly.

Chart input uses a common power-of-two coordinate scale, with a mesh-local translation, so valid small world triangles are above xatlas's fixed absolute area cutoff. The inverse scale is applied to texels-per-unit, preserving world-area density. Original world positions, normals, indices, fingerprint data and BVH geometry remain untouched. Clipped atlas points and intersections use double precision so long shared UV edges retain exactly zero overlap.

Rasterization clips each UV triangle to each intersected texel square. Uniform samples are drawn from the exact clipped polygon, covering acute and thin triangles even when no texel center lies inside them. Faces sharing a texel within one chart are weighted by covered area. There is no nearest-triangle or arbitrary chart fallback. Eight-neighbor dilation fills only pixels assigned to that same chart by xatlas; unfilled chart/gutter pixels are an error.

The CPU backend uses a deterministic balanced BVH to trace nearest two-sided hits and early-exit sun blockers. Direct illumination uses the imported world shading normal and geometric hemisphere. Secondary normals face the incoming path and remain in the reflecting geometric hemisphere. The receiver uses a cosine-weighted hemisphere sample and stores material-independent irradiance. At secondary hits, paths evaluate normalized isotropic GGX/Smith/Schlick specular plus diffuse attenuated by reciprocal view/light Fresnel transmission. Metallic fraction blends dielectric F0=0.04 with textured base color; a full conductor has no diffuse lobe. Perceptual roughness is clamped to 0.04 then squared into microfacet alpha. Sampling mixes the GGX normal distribution and cosine diffuse proposals with the full mixture PDF, applying f*cos/pdf. Directional-sun next-event estimation evaluates the full BRDF and visibility at every hit. This conservative single-scattering model can lose energy through masking and interface transmission; it never adds ambient energy. The receiver albedo is excluded from the irradiance asset. Four bounces means up to four reflecting surfaces beyond the receiver. Missed rays return black; there is no artificial ambient, sky term, emissive transport, tone mapping, clamping or denoising.

Positive solar diameter samples a uniform solid-angle spherical cap centered on the normalized sun direction at the receiver and every diffuse hit. Two random values select cosine and azimuth per evaluation. One shadow ray per path sample estimates actual geometric visibility; there is no shadow blur or denoising. The estimator divides by the cap mean cosine `(1 + cos(diameter/2))/2`, preserving unoccluded normal-incidence energy as diameter changes. Stable `2*sin(diameter/4)^2` cap span and `sqrt(t*(2-t))` sampling avoid small-angle float cancellation on GPU. Wider angular diameters or larger blocker/receiver separation produce wider penumbrae; limited samples can remain noisy. Positive diameters append a transport version and diameter to the settings fingerprint.

Each atlas texel owns its random sequence, accumulation order and output storage. Results are identical across worker counts for the same compiler, platform, scene and settings. Cross-platform floating-point bit identity is not promised. Exceptions are propagated after all workers join; failures are never replaced with default data.

## Optional scene reflection probe

Set `Scene::Probe.Enabled=true` and provide a finite capture `Position` strictly inside ordered `BoundsMin`/`BoundsMax`. The native default is disabled; no inferred room, sky, ambient or fallback probe is created. `BakeOptions::ProbeFaceSize` / `--probe-size` selects a power-of-two edge in [1,1024], default 128. `ProbeSamples` / `--probe-samples` and `ProbeFilterSamples` / `--probe-filter-samples` accept [1,65536], default 128. Production wrappers may impose narrower ranges.

The selected CPU or Vulkan backend traces primary scene-radiance rays with up to `MaxBounces+1` material hits. Misses are black and there is no direct solar disk in the cube: runtime analytic direct specular sun is separate. Visible surfaces contain their physically reflected direct and secondary sunlight. Vulkan capture uses actual hardware ray queries and reports errors instead of falling back to CPU. Full-chain GGX importance filtering is bounded offline CPU preprocessing; mip zero retains the unfiltered HDR capture.

`Lightmap::Probe.FaceSize=0` with empty `Mips` denotes no probe. Otherwise `Mips[level]` contains six contiguous row-major RGBA32F faces in D3D order **+X,-X,+Y,-Y,+Z,-Z** with row zero at the top, edge `max(1,FaceSize>>level)`, and alpha exactly one. The face directions for face coordinates `(u,v)` in [-1,1] are `(1,-v,-u)`, `(-1,-v,u)`, `(u,1,v)`, `(u,-1,-v)`, `(u,-v,1)`, `(-u,-v,-1)`. Mip perceptual roughness is `level/(mipCount-1)`; all levels through edge one are required. Bilinear offline sampling crosses cube edges in direction space. The probe carries its capture position and box volume for runtime box projection.

Version-2 scene payloads add the probe enabled word and three vectors immediately after sun, and metallic/roughness floats immediately after each material base color. Version-2 lightmap payloads append face size, capture position, minimum/maximum box corners, mip count and each length-prefixed float array after the bindings. Version-1 readers assign metallic=0, roughness=0.86 and no probe; their original fingerprints remain stale under version-2 source/settings computation. Validate full mip dimensions before allocating float arrays and reject nonfinite/negative radiance, incorrect alpha and malformed volumes.

## Binary format and validity

Scene magic is `HELLMSC1`; lightmap magic is `HELLMLM1`. Both begin with eight magic bytes, a little-endian 32-bit version (currently 2; readers also accept version 1), a 64-bit payload byte length and a 64-bit FNV-1a payload checksum. Integers and IEEE binary32 floats use canonical little-endian bytes. Strings and collections use checked 32-bit element counts. Loads reject wrong kind/version, size mismatch, truncated fields, checksum mismatch, trailing data and invalid decoded content. The checksum detects accidental corruption; it is not an authentication mechanism.

RGBA loading uses a bounds-checked bulk copy on little-endian IEEE binary32 hosts and explicit canonical decoding on big-endian hosts. Checksums and finite/nonnegative HDR validation remain mandatory. MSVC Debug builds optimize the module's native data passes while preserving the Debug CRT and debug symbols; the embedding renderer's compiler flags are unchanged.

The scene fingerprint hashes every canonical source field, including mesh order, stable keys, original UV0, material base color/textures, metallic/roughness, probe volume and sun. The settings fingerprint includes algorithm version, resolution, padding, sample count, bounce count, seed, ray epsilon and probe capture/filter controls; scheduling and callbacks are excluded. Consumers must compare the source fingerprint to reject stale geometry or sun. Files are capped at 2 GiB; supported square atlas sides are 16 through 8192. The default 4096 atlas is approximately 256 MiB of uncompressed RGBA32F plus bindings.

## First-stage limits

- Runtime sampling is **bilinear at mip level zero only** (`MaxLOD=0`), with default four-pixel gutters. The irradiance atlas has no mip chain; generating ordinary whole-atlas mipmaps would mix unrelated charts. The separate reflection cube has a complete GGX-filtered mip chain.
- All triangles are opaque two-sided blockers with diffuse/GGX metallic reflection. Alpha cutouts, transmission, normal maps, emissive sources and non-repeating texture modes are unsupported.
- `--sun-angle DEGREES` / `BakeOptions::SunAngularDiameterDegrees` selects a finite full apparent solar diameter in [0,20] (for example 0.53). The native default is zero, preserving directional sun sampling and its random-stream behavior. Transport version 2 still requires rebaking legacy material transport. Gevo explicitly selects 0.53. Changing the bake setting requires rebaking the atlas; scene and lightmap writers use version 2.
- Ray epsilon defaults to `0.0001` world units and must suit the exported scene scale; it is configurable through `BakeOptions` and included in settings identity.
- 128 samples with four material bounces is a configurable production starting point, not a convergence guarantee. Enclosed scenes can remain noisy, and CPU bake time scales with occupied atlas area, path length and geometry.
- Atlas generation and asset publication are explicit tooling operations. A failed save can leave an incomplete output file, which the checked loader rejects; no hidden temporary files are created beside project assets.

## Focused validation

The native correctness suite checks analytic direct cosine irradiance and unclipped HDR, separate instance charts, immutable UV0, albedo separation, real aperture transmission, back-face blocking, zero-direct positive diffuse bounce, hit-albedo and linear-texture scaling, no artificial ambient, exact worker-count reproducibility, acute triangle coverage, imported normals opposite winding, default padding-four diagonal gutters, four-bounce settings, invalid input rejection, scene/lightmap roundtrip, changed-sun fingerprints, corruption and truncation. Numeric regressions additionally cover world triangles below xatlas's absolute area threshold and the exact long shared UV edge from the initialized scene, with a positive-overlap control. Finite-sun validation covers realistic 0.53-degree partial visibility, penumbra growth with distance/diameter, an exact hard-sun control, stable open energy, solid-angle distribution, bounce contribution, invalid angles, unchanged file roundtrip, CPU/GPU tolerance and exact batch/worker reproducibility. Build and test on the real checkout; no worktree or source export is required.



Reflection tests additionally cover D3D face orientation/seams, HDR preservation and constant-map GGX normalization, directional roughness broadening, full-chain dimensions, independent BSDF-PDF quadrature, absent conductor diffuse, grazing white-furnace energy, actual mixed/conductor CPU/Vulkan capture and four-bounce parity, observable secondary weighting, closed-wall darkness, v1/v2 asset roundtrip and invalid bounds/mips.
