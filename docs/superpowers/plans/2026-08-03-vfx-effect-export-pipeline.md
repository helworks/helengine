# VFX Effect & Export Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a batch VFX pipeline that reads paired EXR source+mask image sequences, runs the `RainbowExpand` effect (hue-cycle + scale-from-center expansion, mask-driven compositing over a solid background) as a real HLSL shader through a headless DirectX11 device, and writes an EXR sequence out via a CLI tool.

**Architecture:** Four new projects — `helengine.vfx` (contracts, zero dependencies), `helengine.vfx.io` (Magick.NET-backed EXR read/write), `helengine.vfx.directx11` (headless GPU execution via a windowless `Device` and the existing `helengine.shader.compilation` pipeline), `helengine.vfx.cli` (thin console wiring) — plus one new raw asset type, `FloatImageAsset`, in `helengine.core`.

**Tech Stack:** .NET 9 (`net9.0`), SharpDX 4.2.0 (`SharpDX`, `SharpDX.Direct3D11`, `SharpDX.DXGI`, `SharpDX.D3DCompiler`), `Magick.NET-Q16-HDRI-AnyCPU` 14.13.0, xunit (`Microsoft.NET.Test.Sdk` 17.14.1, `xunit` 2.9.3, `xunit.runner.visualstudio` 3.1.4, `coverlet.collector` 6.0.4), the existing `helengine.shader`/`helengine.shader.compilation`/`helengine.directx11` projects.

## Global Constraints

- Every new `.csproj` under `engine/` targets `net9.0`, sets `<ImplicitUsings>enable</ImplicitUsings>` and `<Nullable>disable</Nullable>` (this repo's convention is always `disable`, despite implicit usings being on). Do not add per-project `EnableNETAnalyzers`/`BuildInParallel`/etc. overrides — the repo-root `Directory.Build.props` already applies these automatically to anything under `engine/`.
- No central package management exists under `engine/` — every `PackageReference` must carry an explicit `Version="..."` attribute.
- SharpDX packages are pinned to `4.2.0` everywhere in this repo; use that exact version for any new SharpDX reference.
- `Magick.NET-Q16-HDRI-AnyCPU` must be `14.13.0` (matches the version already vetted for the sibling `Magick.NET-Q8-AnyCPU` package elsewhere in the repo).
- Test projects follow the existing `*.tests` sibling-project convention: `IsPackable=false`, `<Using Include="Xunit" />`, and the four package references shown in Task 1 below.
- `FloatImageAsset.Pixels` is RGBA float, interleaved per pixel, row-major with the top row first — this exact layout is assumed by every reader/writer/GPU-upload/readback step in this plan; do not change it partway through.
- The solution file is `helengine.ui/helengine.sln`, not a repo-root `.sln`. New engine projects are referenced as `..\engine\<name>\<name>.csproj` and use project-type GUID `{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`.

---

## File Structure

New files this plan creates:

- `engine/helengine.core/assets/raw/FloatImageAsset.cs` — new raw asset type.
- `engine/helengine.core.tests/assets/raw/FloatImageAssetTests.cs` — new test project + test.
- `engine/helengine.vfx/helengine.vfx.csproj`, `ImageSequence.cs`, `VfxClip.cs`, `VfxEasingKind.cs`, `VfxEasing.cs`, `VfxParameterType.cs`, `VfxEffectParameterDescriptor.cs`, `VfxFrameConstants.cs`, `IVfxEffect.cs`, `VfxEffectRegistry.cs`, `effects/RainbowExpandEffect.cs` — contracts project.
- `engine/helengine.vfx.tests/` — matching test project.
- `engine/helengine.vfx.io/helengine.vfx.io.csproj`, `ExrFrameReader.cs`, `ExrFrameWriter.cs`, `ExrSequenceReader.cs` — EXR I/O project.
- `engine/helengine.vfx.io.tests/` — matching test project.
- `engine/helengine.vfx.directx11/helengine.vfx.directx11.csproj`, `DirectX11VfxDevice.cs`, `DirectX11VfxEffectRunner.cs`, `shaders/effects/RainbowExpand.hlsl` — headless GPU execution project.
- `engine/helengine.vfx.cli/helengine.vfx.cli.csproj`, `VfxCliArguments.cs`, `Program.cs` — CLI project.
- `engine/helengine.vfx.cli.tests/` — matching test project (argument parsing + the end-to-end structural export test).

Modified files:

- `helengine.ui/helengine.sln` — new project entries and configuration platforms, added incrementally per task.

---

### Task 1: `FloatImageAsset` raw asset type

**Files:**
- Create: `engine/helengine.core/assets/raw/FloatImageAsset.cs`
- Create: `engine/helengine.core.tests/helengine.core.tests.csproj`
- Create: `engine/helengine.core.tests/assets/raw/FloatImageAssetTests.cs`
- Modify: `helengine.ui/helengine.sln`

**Interfaces:**
- Produces: `public class FloatImageAsset : Asset, IDisposable` with public fields `float[] Pixels` (`[NativeOwnedMember]`), `ushort Width`, `ushort Height`, and `void Dispose()`. Every later task that reads/writes/uploads a frame constructs or consumes this exact type.

- [ ] **Step 1: Write `FloatImageAsset`**

```csharp
namespace helengine {
    /// <summary>
    /// Represents raw floating-point (HDR/linear) image data stored in memory, RGBA interleaved,
    /// row-major with the top row first.
    /// </summary>
    public class FloatImageAsset : Asset, IDisposable {
        bool IsDisposedValue;

        /// <summary>
        /// Raw color data for the image in RGBA float order.
        /// </summary>
        [NativeOwnedMember]
        public float[] Pixels;

        /// <summary>
        /// Width of the image in pixels.
        /// </summary>
        public ushort Width;

        /// <summary>
        /// Height of the image in pixels.
        /// </summary>
        public ushort Height;

        /// <summary>
        /// Releases the pixel buffer owned by this raw image asset.
        /// </summary>
        public void Dispose() {
            NativeOwnership.Release(ref Pixels);
            IsDisposedValue = true;
        }
    }
}
```

- [ ] **Step 2: Create the `helengine.core.tests` project**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <Using Include="Xunit" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\helengine.core\helengine.core.csproj" />
  </ItemGroup>
</Project>
```

Save as `engine/helengine.core.tests/helengine.core.tests.csproj`.

- [ ] **Step 3: Write the failing test**

```csharp
using helengine;
using Xunit;

namespace helengine.core.tests.assets.raw {
    public class FloatImageAssetTests {
        [Fact]
        public void Dispose_ReleasesPixelBuffer() {
            var asset = new FloatImageAsset {
                Id = "test-image",
                Width = 2,
                Height = 2,
                Pixels = new float[2 * 2 * 4]
            };

            asset.Dispose();

            Assert.Null(asset.Pixels);
        }
    }
}
```

- [ ] **Step 4: Add both new projects to the solution**

Edit `helengine.ui/helengine.sln`. Insert immediately after the existing `helengine.shader.compilation` project block (the last project entry in the file, right before the `Global` line):

```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "helengine.core.tests", "..\engine\helengine.core.tests\helengine.core.tests.csproj", "{DC9A545A-B5F4-4821-9C31-6BBB9C1A1F8C}"
EndProject
```

Then insert immediately after the last `ProjectConfigurationPlatforms` line (`{51D6C14C-CFAE-4494-9E54-6739C7C169E8}.Release|x86.Build.0 = Release|Any CPU`), still inside that same `GlobalSection`:

```
		{DC9A545A-B5F4-4821-9C31-6BBB9C1A1F8C}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{DC9A545A-B5F4-4821-9C31-6BBB9C1A1F8C}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{DC9A545A-B5F4-4821-9C31-6BBB9C1A1F8C}.Debug|x64.ActiveCfg = Debug|Any CPU
		{DC9A545A-B5F4-4821-9C31-6BBB9C1A1F8C}.Debug|x64.Build.0 = Debug|Any CPU
		{DC9A545A-B5F4-4821-9C31-6BBB9C1A1F8C}.Debug|x86.ActiveCfg = Debug|Any CPU
		{DC9A545A-B5F4-4821-9C31-6BBB9C1A1F8C}.Debug|x86.Build.0 = Debug|Any CPU
		{DC9A545A-B5F4-4821-9C31-6BBB9C1A1F8C}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{DC9A545A-B5F4-4821-9C31-6BBB9C1A1F8C}.Release|Any CPU.Build.0 = Release|Any CPU
		{DC9A545A-B5F4-4821-9C31-6BBB9C1A1F8C}.Release|x64.ActiveCfg = Release|Any CPU
		{DC9A545A-B5F4-4821-9C31-6BBB9C1A1F8C}.Release|x64.Build.0 = Release|Any CPU
		{DC9A545A-B5F4-4821-9C31-6BBB9C1A1F8C}.Release|x86.ActiveCfg = Release|Any CPU
		{DC9A545A-B5F4-4821-9C31-6BBB9C1A1F8C}.Release|x86.Build.0 = Release|Any CPU
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test engine/helengine.core.tests/helengine.core.tests.csproj`
Expected: 1 passed.

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.core/assets/raw/FloatImageAsset.cs engine/helengine.core.tests helengine.ui/helengine.sln
git commit -m "feat: add FloatImageAsset raw asset type for HDR image data"
```

---

### Task 2: `helengine.vfx` contracts project — `ImageSequence` and `VfxClip`

**Files:**
- Create: `engine/helengine.vfx/helengine.vfx.csproj`
- Create: `engine/helengine.vfx/ImageSequence.cs`
- Create: `engine/helengine.vfx/VfxClip.cs`
- Create: `engine/helengine.vfx.tests/helengine.vfx.tests.csproj`
- Create: `engine/helengine.vfx.tests/ImageSequenceTests.cs`
- Create: `engine/helengine.vfx.tests/VfxClipTests.cs`
- Modify: `helengine.ui/helengine.sln`

**Interfaces:**
- Produces: `public class ImageSequence` with `IReadOnlyList<string> FramePaths`, `int Width`, `int Height`, `double? FrameRate`, `int FrameCount`, constructor `ImageSequence(IReadOnlyList<string> framePaths, int width, int height, double? frameRate = null)`.
- Produces: `public class VfxClip` with `ImageSequence Source`, `ImageSequence Mask`, `int FrameCount`, `int Width`, `int Height`, constructor `VfxClip(ImageSequence source, ImageSequence mask)` that throws `InvalidOperationException` on frame-count or resolution mismatch.

- [ ] **Step 1: Create the `helengine.vfx` project (no dependencies)**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
</Project>
```

Save as `engine/helengine.vfx/helengine.vfx.csproj`.

- [ ] **Step 2: Create the `helengine.vfx.tests` project**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <Using Include="Xunit" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\helengine.vfx\helengine.vfx.csproj" />
  </ItemGroup>
</Project>
```

Save as `engine/helengine.vfx.tests/helengine.vfx.tests.csproj`.

- [ ] **Step 3: Write the failing tests**

```csharp
using helengine.vfx;
using Xunit;

namespace helengine.vfx.tests {
    public class ImageSequenceTests {
        [Fact]
        public void Constructor_EmptyFramePaths_Throws() {
            Assert.Throws<ArgumentException>(() => new ImageSequence(new string[0], 4, 4));
        }

        [Fact]
        public void Constructor_ValidInput_SetsFrameCount() {
            var sequence = new ImageSequence(new[] { "a.exr", "b.exr" }, 4, 4);

            Assert.Equal(2, sequence.FrameCount);
            Assert.Equal(4, sequence.Width);
            Assert.Equal(4, sequence.Height);
        }
    }
}
```

```csharp
using helengine.vfx;
using Xunit;

namespace helengine.vfx.tests {
    public class VfxClipTests {
        [Fact]
        public void Constructor_MismatchedFrameCount_Throws() {
            var source = new ImageSequence(new[] { "a.exr", "b.exr" }, 4, 4);
            var mask = new ImageSequence(new[] { "a.exr" }, 4, 4);

            Assert.Throws<InvalidOperationException>(() => new VfxClip(source, mask));
        }

        [Fact]
        public void Constructor_MismatchedResolution_Throws() {
            var source = new ImageSequence(new[] { "a.exr" }, 4, 4);
            var mask = new ImageSequence(new[] { "a.exr" }, 8, 8);

            Assert.Throws<InvalidOperationException>(() => new VfxClip(source, mask));
        }

        [Fact]
        public void Constructor_MatchingSequences_ExposesFrameCountAndResolution() {
            var source = new ImageSequence(new[] { "a.exr", "b.exr" }, 4, 4);
            var mask = new ImageSequence(new[] { "a.exr", "b.exr" }, 4, 4);

            var clip = new VfxClip(source, mask);

            Assert.Equal(2, clip.FrameCount);
            Assert.Equal(4, clip.Width);
            Assert.Equal(4, clip.Height);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test engine/helengine.vfx.tests/helengine.vfx.tests.csproj`
Expected: FAIL (compile error — `ImageSequence`/`VfxClip` do not exist yet).

- [ ] **Step 5: Implement `ImageSequence`**

```csharp
namespace helengine.vfx {
    /// <summary>
    /// An ordered sequence of image frame file paths that make up one clip.
    /// </summary>
    public class ImageSequence {
        public IReadOnlyList<string> FramePaths { get; }
        public int Width { get; }
        public int Height { get; }
        public double? FrameRate { get; }

        public int FrameCount => FramePaths.Count;

        public ImageSequence(IReadOnlyList<string> framePaths, int width, int height, double? frameRate = null) {
            if (framePaths == null || framePaths.Count == 0) {
                throw new ArgumentException("Image sequence must contain at least one frame.", nameof(framePaths));
            }
            if (width <= 0) {
                throw new ArgumentOutOfRangeException(nameof(width), "Image sequence width must be positive.");
            }
            if (height <= 0) {
                throw new ArgumentOutOfRangeException(nameof(height), "Image sequence height must be positive.");
            }

            FramePaths = framePaths;
            Width = width;
            Height = height;
            FrameRate = frameRate;
        }
    }
}
```

- [ ] **Step 6: Implement `VfxClip`**

```csharp
namespace helengine.vfx {
    /// <summary>
    /// Pairs a source color image sequence with a matching alpha mask image sequence.
    /// </summary>
    public class VfxClip {
        public ImageSequence Source { get; }
        public ImageSequence Mask { get; }

        public int FrameCount => Source.FrameCount;
        public int Width => Source.Width;
        public int Height => Source.Height;

        public VfxClip(ImageSequence source, ImageSequence mask) {
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }
            if (mask == null) {
                throw new ArgumentNullException(nameof(mask));
            }
            if (source.FrameCount != mask.FrameCount) {
                throw new InvalidOperationException(
                    $"Source sequence has {source.FrameCount} frames but mask sequence has {mask.FrameCount} frames. They must match.");
            }
            if (source.Width != mask.Width || source.Height != mask.Height) {
                throw new InvalidOperationException(
                    $"Source sequence resolution {source.Width}x{source.Height} does not match mask sequence resolution {mask.Width}x{mask.Height}.");
            }

            Source = source;
            Mask = mask;
        }
    }
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test engine/helengine.vfx.tests/helengine.vfx.tests.csproj`
Expected: 5 passed.

- [ ] **Step 8: Add both new projects to the solution**

Edit `helengine.ui/helengine.sln`. Insert immediately after the `helengine.core.tests` project block added in Task 1:

```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "helengine.vfx", "..\engine\helengine.vfx\helengine.vfx.csproj", "{43C299F7-58EF-44EB-A7A9-2A16F642A508}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "helengine.vfx.tests", "..\engine\helengine.vfx.tests\helengine.vfx.tests.csproj", "{6AC69D97-B34E-4C78-8EA5-D57130BC1265}"
EndProject
```

Then insert immediately after the `helengine.core.tests` configuration lines added in Task 1:

```
		{43C299F7-58EF-44EB-A7A9-2A16F642A508}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{43C299F7-58EF-44EB-A7A9-2A16F642A508}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{43C299F7-58EF-44EB-A7A9-2A16F642A508}.Debug|x64.ActiveCfg = Debug|Any CPU
		{43C299F7-58EF-44EB-A7A9-2A16F642A508}.Debug|x64.Build.0 = Debug|Any CPU
		{43C299F7-58EF-44EB-A7A9-2A16F642A508}.Debug|x86.ActiveCfg = Debug|Any CPU
		{43C299F7-58EF-44EB-A7A9-2A16F642A508}.Debug|x86.Build.0 = Debug|Any CPU
		{43C299F7-58EF-44EB-A7A9-2A16F642A508}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{43C299F7-58EF-44EB-A7A9-2A16F642A508}.Release|Any CPU.Build.0 = Release|Any CPU
		{43C299F7-58EF-44EB-A7A9-2A16F642A508}.Release|x64.ActiveCfg = Release|Any CPU
		{43C299F7-58EF-44EB-A7A9-2A16F642A508}.Release|x64.Build.0 = Release|Any CPU
		{43C299F7-58EF-44EB-A7A9-2A16F642A508}.Release|x86.ActiveCfg = Release|Any CPU
		{43C299F7-58EF-44EB-A7A9-2A16F642A508}.Release|x86.Build.0 = Release|Any CPU
		{6AC69D97-B34E-4C78-8EA5-D57130BC1265}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{6AC69D97-B34E-4C78-8EA5-D57130BC1265}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{6AC69D97-B34E-4C78-8EA5-D57130BC1265}.Debug|x64.ActiveCfg = Debug|Any CPU
		{6AC69D97-B34E-4C78-8EA5-D57130BC1265}.Debug|x64.Build.0 = Debug|Any CPU
		{6AC69D97-B34E-4C78-8EA5-D57130BC1265}.Debug|x86.ActiveCfg = Debug|Any CPU
		{6AC69D97-B34E-4C78-8EA5-D57130BC1265}.Debug|x86.Build.0 = Debug|Any CPU
		{6AC69D97-B34E-4C78-8EA5-D57130BC1265}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{6AC69D97-B34E-4C78-8EA5-D57130BC1265}.Release|Any CPU.Build.0 = Release|Any CPU
		{6AC69D97-B34E-4C78-8EA5-D57130BC1265}.Release|x64.ActiveCfg = Release|Any CPU
		{6AC69D97-B34E-4C78-8EA5-D57130BC1265}.Release|x64.Build.0 = Release|Any CPU
		{6AC69D97-B34E-4C78-8EA5-D57130BC1265}.Release|x86.ActiveCfg = Release|Any CPU
		{6AC69D97-B34E-4C78-8EA5-D57130BC1265}.Release|x86.Build.0 = Release|Any CPU
```

- [ ] **Step 9: Commit**

```bash
git add engine/helengine.vfx engine/helengine.vfx.tests helengine.ui/helengine.sln
git commit -m "feat: add ImageSequence and VfxClip to new helengine.vfx contracts project"
```

---

### Task 3: Easing math (`VfxEasingKind`, `VfxEasing`)

**Files:**
- Create: `engine/helengine.vfx/VfxEasingKind.cs`
- Create: `engine/helengine.vfx/VfxEasing.cs`
- Create: `engine/helengine.vfx.tests/VfxEasingTests.cs`

**Interfaces:**
- Produces: `public enum VfxEasingKind { Linear = 0, EaseIn = 1, EaseOut = 2, EaseInOut = 3 }`.
- Produces: `public static class VfxEasing { public static float Apply(VfxEasingKind kind, float t); }`. This same formula must be mirrored in `RainbowExpand.hlsl` in Task 9 — the HLSL comment there must reference this method by name so the duplication stays visible.

- [ ] **Step 1: Write the failing tests**

```csharp
using helengine.vfx;
using Xunit;

namespace helengine.vfx.tests {
    public class VfxEasingTests {
        [Theory]
        [InlineData(VfxEasingKind.Linear, 0f, 0f)]
        [InlineData(VfxEasingKind.Linear, 0.5f, 0.5f)]
        [InlineData(VfxEasingKind.Linear, 1f, 1f)]
        [InlineData(VfxEasingKind.EaseIn, 0.5f, 0.25f)]
        [InlineData(VfxEasingKind.EaseOut, 0.5f, 0.75f)]
        public void Apply_KnownValues_MatchesExpected(VfxEasingKind kind, float t, float expected) {
            float result = VfxEasing.Apply(kind, t);

            Assert.Equal(expected, result, 3);
        }

        [Fact]
        public void Apply_ValuesOutsideZeroToOne_AreClamped() {
            Assert.Equal(0f, VfxEasing.Apply(VfxEasingKind.Linear, -1f));
            Assert.Equal(1f, VfxEasing.Apply(VfxEasingKind.Linear, 2f));
        }

        [Theory]
        [InlineData(VfxEasingKind.EaseInOut, 0f)]
        [InlineData(VfxEasingKind.EaseInOut, 1f)]
        public void Apply_EaseInOut_HitsEndpoints(VfxEasingKind kind, float t) {
            Assert.Equal(t, VfxEasing.Apply(kind, t), 3);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test engine/helengine.vfx.tests/helengine.vfx.tests.csproj`
Expected: FAIL (compile error — `VfxEasingKind`/`VfxEasing` do not exist yet).

- [ ] **Step 3: Implement `VfxEasingKind`**

```csharp
namespace helengine.vfx {
    public enum VfxEasingKind {
        Linear = 0,
        EaseIn = 1,
        EaseOut = 2,
        EaseInOut = 3
    }
}
```

- [ ] **Step 4: Implement `VfxEasing`**

```csharp
namespace helengine.vfx {
    /// <summary>
    /// Pure easing curve math. Must stay in sync with the identical formulas in RainbowExpand.hlsl.
    /// </summary>
    public static class VfxEasing {
        public static float Apply(VfxEasingKind kind, float t) {
            float clamped = Math.Clamp(t, 0f, 1f);
            switch (kind) {
                case VfxEasingKind.Linear:
                    return clamped;
                case VfxEasingKind.EaseIn:
                    return clamped * clamped;
                case VfxEasingKind.EaseOut:
                    return 1f - ((1f - clamped) * (1f - clamped));
                case VfxEasingKind.EaseInOut:
                    return clamped < 0.5f
                        ? 2f * clamped * clamped
                        : 1f - (float)(Math.Pow((-2f * clamped) + 2f, 2) / 2f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown easing kind.");
            }
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test engine/helengine.vfx.tests/helengine.vfx.tests.csproj`
Expected: 8 passed total (5 from Task 2 + 3 theories with 5 total cases from this task — 10 passed).

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.vfx/VfxEasingKind.cs engine/helengine.vfx/VfxEasing.cs engine/helengine.vfx.tests/VfxEasingTests.cs
git commit -m "feat: add VfxEasing curve math"
```

---

### Task 4: Effect abstraction (`VfxFrameConstants`, `VfxEffectParameterDescriptor`, `IVfxEffect`, `VfxEffectRegistry`)

**Files:**
- Create: `engine/helengine.vfx/VfxFrameConstants.cs`
- Create: `engine/helengine.vfx/VfxParameterType.cs`
- Create: `engine/helengine.vfx/VfxEffectParameterDescriptor.cs`
- Create: `engine/helengine.vfx/IVfxEffect.cs`
- Create: `engine/helengine.vfx/VfxEffectRegistry.cs`
- Create: `engine/helengine.vfx.tests/VfxFrameConstantsTests.cs`
- Create: `engine/helengine.vfx.tests/VfxEffectRegistryTests.cs`

**Interfaces:**
- Produces: `public static class VfxFrameConstants { public const int ParamSlotCount = 16; public const int HeaderFloatCount = 4; public const int TotalFloatCount = 20; public static float[] Build(float normalizedTime, int width, int height, float[] paramSlots); }`. Task 9's HLSL cbuffer and Task 9's runner both depend on this exact layout (4 header floats, then 16 param floats).
- Produces: `public interface IVfxEffect { string Id { get; } string DisplayName { get; } IReadOnlyList<VfxEffectParameterDescriptor> Parameters { get; } string ShaderResourcePath { get; } string VertexEntryPoint { get; } string PixelEntryPoint { get; } float[] ResolveParameterSlots(IReadOnlyDictionary<string, string> parameterValues); }`.
- Produces: `public static class VfxEffectRegistry { public static void Register(IVfxEffect effect); public static IVfxEffect Resolve(string id); public static IReadOnlyCollection<string> KnownIds { get; } }`. `Resolve` throws `InvalidOperationException` listing known ids when `id` is not registered.

- [ ] **Step 1: Write the failing tests**

```csharp
using helengine.vfx;
using Xunit;

namespace helengine.vfx.tests {
    public class VfxFrameConstantsTests {
        [Fact]
        public void Build_WrongParamSlotLength_Throws() {
            Assert.Throws<ArgumentException>(() => VfxFrameConstants.Build(0f, 4, 4, new float[4]));
        }

        [Fact]
        public void Build_ValidInput_LaysOutHeaderThenParams() {
            float[] paramSlots = new float[VfxFrameConstants.ParamSlotCount];
            paramSlots[0] = 7f;

            float[] result = VfxFrameConstants.Build(0.5f, 100, 200, paramSlots);

            Assert.Equal(VfxFrameConstants.TotalFloatCount, result.Length);
            Assert.Equal(0.5f, result[0]);
            Assert.Equal(100f, result[1]);
            Assert.Equal(200f, result[2]);
            Assert.Equal(7f, result[VfxFrameConstants.HeaderFloatCount]);
        }
    }
}
```

```csharp
using helengine.vfx;
using Xunit;

namespace helengine.vfx.tests {
    class FakeEffect : IVfxEffect {
        public string Id => "fake-effect";
        public string DisplayName => "Fake Effect";
        public IReadOnlyList<VfxEffectParameterDescriptor> Parameters { get; } = new List<VfxEffectParameterDescriptor>();
        public string ShaderResourcePath => "shaders/fake.hlsl";
        public string VertexEntryPoint => "FullscreenVS";
        public string PixelEntryPoint => "FakePS";
        public float[] ResolveParameterSlots(IReadOnlyDictionary<string, string> parameterValues) => new float[VfxFrameConstants.ParamSlotCount];
    }

    public class VfxEffectRegistryTests {
        [Fact]
        public void Resolve_RegisteredId_ReturnsEffect() {
            VfxEffectRegistry.Register(new FakeEffect());

            IVfxEffect resolved = VfxEffectRegistry.Resolve("fake-effect");

            Assert.Equal("fake-effect", resolved.Id);
        }

        [Fact]
        public void Resolve_UnknownId_ThrowsWithMessage() {
            var exception = Assert.Throws<InvalidOperationException>(() => VfxEffectRegistry.Resolve("does-not-exist"));

            Assert.Contains("does-not-exist", exception.Message);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test engine/helengine.vfx.tests/helengine.vfx.tests.csproj`
Expected: FAIL (compile error — the new types do not exist yet).

- [ ] **Step 3: Implement `VfxFrameConstants`**

```csharp
namespace helengine.vfx {
    /// <summary>
    /// Describes the fixed constant-buffer layout every VFX effect shader shares: normalized time,
    /// resolution, and a fixed bank of parameter slots. Must stay in sync with each effect's HLSL
    /// cbuffer declaration (register b0).
    /// </summary>
    public static class VfxFrameConstants {
        public const int ParamSlotCount = 16;
        public const int HeaderFloatCount = 4;
        public const int TotalFloatCount = HeaderFloatCount + ParamSlotCount;

        public static float[] Build(float normalizedTime, int width, int height, float[] paramSlots) {
            if (paramSlots == null || paramSlots.Length != ParamSlotCount) {
                throw new ArgumentException($"Parameter slots must contain exactly {ParamSlotCount} values.", nameof(paramSlots));
            }

            float[] buffer = new float[TotalFloatCount];
            buffer[0] = normalizedTime;
            buffer[1] = width;
            buffer[2] = height;
            buffer[3] = 0f;
            Array.Copy(paramSlots, 0, buffer, HeaderFloatCount, ParamSlotCount);
            return buffer;
        }
    }
}
```

- [ ] **Step 4: Implement `VfxParameterType` and `VfxEffectParameterDescriptor`**

```csharp
namespace helengine.vfx {
    public enum VfxParameterType {
        Float,
        Int,
        Color
    }
}
```

```csharp
namespace helengine.vfx {
    /// <summary>
    /// Describes one parameter an effect exposes, for CLI help text and validation.
    /// </summary>
    public class VfxEffectParameterDescriptor {
        public string Name { get; }
        public VfxParameterType Type { get; }
        public string DefaultValueText { get; }
        public string Description { get; }

        public VfxEffectParameterDescriptor(string name, VfxParameterType type, string defaultValueText, string description) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Parameter name must be provided.", nameof(name));
            }
            if (string.IsNullOrWhiteSpace(defaultValueText)) {
                throw new ArgumentException("Default value text must be provided.", nameof(defaultValueText));
            }

            Name = name;
            Type = type;
            DefaultValueText = defaultValueText;
            Description = description;
        }
    }
}
```

- [ ] **Step 5: Implement `IVfxEffect`**

```csharp
namespace helengine.vfx {
    /// <summary>
    /// A VFX effect backed by an HLSL shader compiled through the engine's shader compiler.
    /// </summary>
    public interface IVfxEffect {
        string Id { get; }
        string DisplayName { get; }
        IReadOnlyList<VfxEffectParameterDescriptor> Parameters { get; }
        string ShaderResourcePath { get; }
        string VertexEntryPoint { get; }
        string PixelEntryPoint { get; }

        /// <summary>
        /// Resolves named parameter values (as raw CLI strings) into the fixed
        /// VfxFrameConstants.ParamSlotCount-length float array this effect's shader expects.
        /// </summary>
        float[] ResolveParameterSlots(IReadOnlyDictionary<string, string> parameterValues);
    }
}
```

- [ ] **Step 6: Implement `VfxEffectRegistry`**

```csharp
namespace helengine.vfx {
    /// <summary>
    /// Maps effect ids to registered effect instances. New effects register themselves here at startup.
    /// </summary>
    public static class VfxEffectRegistry {
        static readonly Dictionary<string, IVfxEffect> effects = new Dictionary<string, IVfxEffect>();

        public static void Register(IVfxEffect effect) {
            if (effect == null) {
                throw new ArgumentNullException(nameof(effect));
            }
            effects[effect.Id] = effect;
        }

        public static IVfxEffect Resolve(string id) {
            if (effects.TryGetValue(id, out IVfxEffect effect)) {
                return effect;
            }
            string knownIds = string.Join(", ", effects.Keys);
            throw new InvalidOperationException($"No VFX effect is registered with id '{id}'. Known effect ids: {knownIds}");
        }

        public static IReadOnlyCollection<string> KnownIds => effects.Keys;
    }
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test engine/helengine.vfx.tests/helengine.vfx.tests.csproj`
Expected: all passed (10 from before + 4 new = 14).

- [ ] **Step 8: Commit**

```bash
git add engine/helengine.vfx/VfxFrameConstants.cs engine/helengine.vfx/VfxParameterType.cs engine/helengine.vfx/VfxEffectParameterDescriptor.cs engine/helengine.vfx/IVfxEffect.cs engine/helengine.vfx/VfxEffectRegistry.cs engine/helengine.vfx.tests/VfxFrameConstantsTests.cs engine/helengine.vfx.tests/VfxEffectRegistryTests.cs
git commit -m "feat: add IVfxEffect abstraction and VfxEffectRegistry"
```

---

### Task 5: `RainbowExpandEffect`

**Files:**
- Create: `engine/helengine.vfx/effects/RainbowExpandEffect.cs`
- Create: `engine/helengine.vfx.tests/effects/RainbowExpandEffectTests.cs`

**Interfaces:**
- Consumes: `IVfxEffect`, `VfxEffectParameterDescriptor`, `VfxParameterType`, `VfxEasingKind`, `VfxFrameConstants.ParamSlotCount` (all from Task 4).
- Produces: `public class RainbowExpandEffect : IVfxEffect` with `Id => "rainbow-expand"`, `ShaderResourcePath => "shaders/effects/RainbowExpand.hlsl"`, `VertexEntryPoint => "FullscreenVS"`, `PixelEntryPoint => "RainbowExpandPS"`. Slot layout produced by `ResolveParameterSlots`: `[0]=HueCyclesPerClip, [1]=StartScale, [2]=EndScale, [3]=(float)VfxEasingKind, [4..6]=BackgroundColor R,G,B`, remaining slots zero. Task 9's `RainbowExpand.hlsl` cbuffer must read these same indices.

- [ ] **Step 1: Write the failing tests**

```csharp
using helengine.vfx;
using helengine.vfx.effects;
using Xunit;

namespace helengine.vfx.tests.effects {
    public class RainbowExpandEffectTests {
        [Fact]
        public void ResolveParameterSlots_Defaults_MatchDocumentedDefaults() {
            var effect = new RainbowExpandEffect();

            float[] slots = effect.ResolveParameterSlots(new Dictionary<string, string>());

            Assert.Equal(VfxFrameConstants.ParamSlotCount, slots.Length);
            Assert.Equal(1f, slots[0]);
            Assert.Equal(1f, slots[1]);
            Assert.Equal(2f, slots[2]);
            Assert.Equal((float)VfxEasingKind.Linear, slots[3]);
            Assert.Equal(0f, slots[4]);
            Assert.Equal(0f, slots[5]);
            Assert.Equal(0f, slots[6]);
        }

        [Fact]
        public void ResolveParameterSlots_ExplicitValues_AreParsed() {
            var effect = new RainbowExpandEffect();
            var values = new Dictionary<string, string> {
                ["HueCyclesPerClip"] = "3",
                ["StartScale"] = "0.5",
                ["EndScale"] = "4",
                ["Easing"] = "EaseInOut",
                ["BackgroundColor"] = "0.1,0.2,0.3"
            };

            float[] slots = effect.ResolveParameterSlots(values);

            Assert.Equal(3f, slots[0]);
            Assert.Equal(0.5f, slots[1]);
            Assert.Equal(4f, slots[2]);
            Assert.Equal((float)VfxEasingKind.EaseInOut, slots[3]);
            Assert.Equal(0.1f, slots[4], 3);
            Assert.Equal(0.2f, slots[5], 3);
            Assert.Equal(0.3f, slots[6], 3);
        }

        [Fact]
        public void ResolveParameterSlots_InvalidEasing_Throws() {
            var effect = new RainbowExpandEffect();
            var values = new Dictionary<string, string> { ["Easing"] = "NotARealEasing" };

            Assert.Throws<ArgumentException>(() => effect.ResolveParameterSlots(values));
        }

        [Fact]
        public void ResolveParameterSlots_InvalidBackgroundColor_Throws() {
            var effect = new RainbowExpandEffect();
            var values = new Dictionary<string, string> { ["BackgroundColor"] = "not,a,color" };

            Assert.Throws<ArgumentException>(() => effect.ResolveParameterSlots(values));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test engine/helengine.vfx.tests/helengine.vfx.tests.csproj`
Expected: FAIL (compile error — `RainbowExpandEffect` does not exist yet).

- [ ] **Step 3: Implement `RainbowExpandEffect`**

```csharp
using System.Globalization;

namespace helengine.vfx.effects {
    /// <summary>
    /// Hue-cycles a mask-keyed subject while scaling it from frame center, composited over a solid background.
    /// </summary>
    public class RainbowExpandEffect : IVfxEffect {
        public string Id => "rainbow-expand";
        public string DisplayName => "Rainbow Expand";
        public string ShaderResourcePath => "shaders/effects/RainbowExpand.hlsl";
        public string VertexEntryPoint => "FullscreenVS";
        public string PixelEntryPoint => "RainbowExpandPS";

        public IReadOnlyList<VfxEffectParameterDescriptor> Parameters { get; } = new List<VfxEffectParameterDescriptor> {
            new VfxEffectParameterDescriptor("HueCyclesPerClip", VfxParameterType.Float, "1", "Number of full 360-degree hue rotations across the whole clip."),
            new VfxEffectParameterDescriptor("StartScale", VfxParameterType.Float, "1", "Uniform scale factor at the start of the clip."),
            new VfxEffectParameterDescriptor("EndScale", VfxParameterType.Float, "2", "Uniform scale factor at the end of the clip."),
            new VfxEffectParameterDescriptor("Easing", VfxParameterType.Int, "Linear", "One of Linear, EaseIn, EaseOut, EaseInOut."),
            new VfxEffectParameterDescriptor("BackgroundColor", VfxParameterType.Color, "0,0,0", "Solid background color as R,G,B in [0,1].")
        };

        public float[] ResolveParameterSlots(IReadOnlyDictionary<string, string> parameterValues) {
            if (parameterValues == null) {
                throw new ArgumentNullException(nameof(parameterValues));
            }

            float[] slots = new float[VfxFrameConstants.ParamSlotCount];
            slots[0] = ResolveFloat(parameterValues, "HueCyclesPerClip", "1");
            slots[1] = ResolveFloat(parameterValues, "StartScale", "1");
            slots[2] = ResolveFloat(parameterValues, "EndScale", "2");
            slots[3] = (float)ResolveEasing(parameterValues);

            (float r, float g, float b) = ResolveColor(parameterValues, "BackgroundColor", "0,0,0");
            slots[4] = r;
            slots[5] = g;
            slots[6] = b;

            return slots;
        }

        static float ResolveFloat(IReadOnlyDictionary<string, string> values, string name, string defaultValueText) {
            string text = values.TryGetValue(name, out string raw) ? raw : defaultValueText;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)) {
                throw new ArgumentException($"Parameter '{name}' must be a number, got '{text}'.");
            }
            return parsed;
        }

        static VfxEasingKind ResolveEasing(IReadOnlyDictionary<string, string> values) {
            string text = values.TryGetValue("Easing", out string raw) ? raw : "Linear";
            if (!Enum.TryParse(text, ignoreCase: true, out VfxEasingKind kind)) {
                throw new ArgumentException($"Parameter 'Easing' must be one of Linear, EaseIn, EaseOut, EaseInOut, got '{text}'.");
            }
            return kind;
        }

        static (float, float, float) ResolveColor(IReadOnlyDictionary<string, string> values, string name, string defaultValueText) {
            string text = values.TryGetValue(name, out string raw) ? raw : defaultValueText;
            string[] parts = text.Split(',');
            if (parts.Length != 3
                || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float r)
                || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float g)
                || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float b)) {
                throw new ArgumentException($"Parameter '{name}' must be three comma-separated numbers R,G,B, got '{text}'.");
            }
            return (r, g, b);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test engine/helengine.vfx.tests/helengine.vfx.tests.csproj`
Expected: all passed (14 from before + 4 new = 18).

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.vfx/effects/RainbowExpandEffect.cs engine/helengine.vfx.tests/effects/RainbowExpandEffectTests.cs
git commit -m "feat: add RainbowExpandEffect parameter resolution"
```

---

### Task 6: `helengine.vfx.io` project — `ExrFrameReader` / `ExrFrameWriter`

**Files:**
- Create: `engine/helengine.vfx.io/helengine.vfx.io.csproj`
- Create: `engine/helengine.vfx.io/ExrFrameReader.cs`
- Create: `engine/helengine.vfx.io/ExrFrameWriter.cs`
- Create: `engine/helengine.vfx.io.tests/helengine.vfx.io.tests.csproj`
- Create: `engine/helengine.vfx.io.tests/ExrFrameRoundTripTests.cs`
- Modify: `helengine.ui/helengine.sln`

**Interfaces:**
- Consumes: `FloatImageAsset` from `helengine.core` (Task 1).
- Produces: `public static class ExrFrameReader { public static FloatImageAsset ReadFrame(string filePath); public static (int Width, int Height) ReadDimensions(string filePath); }`.
- Produces: `public static class ExrFrameWriter { public static void WriteFrame(FloatImageAsset frame, string filePath); }`.

This is the design's flagged open risk (Magick.NET Q16-HDRI float fidelity). It has already been spiked directly against the real package outside this repo: writing normalized RGBA floats (including values above 1.0, confirming HDR values are not clamped) via `new MagickImage(byte[], PixelReadSettings)` with `StorageType.Float`, then reading back via `image.GetPixels().ToArray()` and dividing by `Quantum.Max`, round-trips exactly. The code below is that verified approach.

- [ ] **Step 1: Create the `helengine.vfx.io` project**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Magick.NET-Q16-HDRI-AnyCPU" Version="14.13.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\helengine.core\helengine.core.csproj" />
    <ProjectReference Include="..\helengine.vfx\helengine.vfx.csproj" />
  </ItemGroup>
</Project>
```

Save as `engine/helengine.vfx.io/helengine.vfx.io.csproj`.

- [ ] **Step 2: Create the `helengine.vfx.io.tests` project**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <Using Include="Xunit" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\helengine.core\helengine.core.csproj" />
    <ProjectReference Include="..\helengine.vfx\helengine.vfx.csproj" />
    <ProjectReference Include="..\helengine.vfx.io\helengine.vfx.io.csproj" />
  </ItemGroup>
</Project>
```

Save as `engine/helengine.vfx.io.tests/helengine.vfx.io.tests.csproj`.

- [ ] **Step 3: Write the failing test**

```csharp
using helengine;
using helengine.vfx.io;
using Xunit;

namespace helengine.vfx.io.tests {
    public class ExrFrameRoundTripTests {
        [Fact]
        public void WriteFrame_ThenReadFrame_RoundTripsWithinTolerance() {
            string path = Path.Combine(Path.GetTempPath(), "helengine-vfx-io-tests-" + Guid.NewGuid().ToString("N") + ".exr");
            try {
                float[] pixels = new float[2 * 2 * 4];
                for (int i = 0; i < 2 * 2; i++) {
                    pixels[(i * 4) + 0] = 0.25f;
                    pixels[(i * 4) + 1] = 0.5f;
                    pixels[(i * 4) + 2] = 0.75f;
                    pixels[(i * 4) + 3] = 2.0f; // above 1.0 to confirm HDR values are not clamped
                }
                var original = new FloatImageAsset { Width = 2, Height = 2, Pixels = pixels };

                ExrFrameWriter.WriteFrame(original, path);
                FloatImageAsset roundTripped = ExrFrameReader.ReadFrame(path);

                Assert.Equal(2, roundTripped.Width);
                Assert.Equal(2, roundTripped.Height);
                for (int i = 0; i < pixels.Length; i++) {
                    Assert.Equal(pixels[i], roundTripped.Pixels[i], 2);
                }

                original.Dispose();
                roundTripped.Dispose();
            } finally {
                if (File.Exists(path)) {
                    File.Delete(path);
                }
            }
        }
    }
}
```

- [ ] **Step 4: Run test to verify it fails**

Run: `dotnet test engine/helengine.vfx.io.tests/helengine.vfx.io.tests.csproj`
Expected: FAIL (compile error — `ExrFrameReader`/`ExrFrameWriter` do not exist yet).

- [ ] **Step 5: Implement `ExrFrameWriter`**

```csharp
using ImageMagick;

namespace helengine.vfx.io {
    /// <summary>
    /// Writes a single FloatImageAsset frame to an EXR file using Magick.NET's HDRI (float) pipeline.
    /// </summary>
    public static class ExrFrameWriter {
        public static void WriteFrame(FloatImageAsset frame, string filePath) {
            if (frame == null) {
                throw new ArgumentNullException(nameof(frame));
            }
            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            byte[] rgbaBytes = new byte[frame.Pixels.Length * sizeof(float)];
            Buffer.BlockCopy(frame.Pixels, 0, rgbaBytes, 0, rgbaBytes.Length);

            var settings = new PixelReadSettings(frame.Width, frame.Height, StorageType.Float, PixelMapping.RGBA);
            using var image = new MagickImage(rgbaBytes, settings);
            image.Format = MagickFormat.Exr;

            string directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
            if (!string.IsNullOrWhiteSpace(directory)) {
                Directory.CreateDirectory(directory);
            }

            image.Write(filePath);
        }
    }
}
```

- [ ] **Step 6: Implement `ExrFrameReader`**

```csharp
using ImageMagick;

namespace helengine.vfx.io {
    /// <summary>
    /// Reads a single EXR frame into a FloatImageAsset using Magick.NET's HDRI (float) pipeline.
    /// Quantum values from GetPixels().ToArray() are scaled to [0, Quantum.Max] and must be divided
    /// by Quantum.Max to recover the normalized (and possibly HDR, above-1.0) float value.
    /// </summary>
    public static class ExrFrameReader {
        public static FloatImageAsset ReadFrame(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            using var image = new MagickImage(filePath);
            int width = (int)image.Width;
            int height = (int)image.Height;

            using var pixelCollection = image.GetPixels();
            float[] quantumScaled = pixelCollection.ToArray();
            int channelCount = quantumScaled.Length / (width * height);

            float[] rgba = new float[width * height * 4];
            for (int i = 0; i < width * height; i++) {
                int sourceOffset = i * channelCount;
                int destOffset = i * 4;
                rgba[destOffset + 0] = quantumScaled[sourceOffset + 0] / Quantum.Max;
                rgba[destOffset + 1] = (channelCount > 1 ? quantumScaled[sourceOffset + 1] : quantumScaled[sourceOffset + 0]) / Quantum.Max;
                rgba[destOffset + 2] = (channelCount > 2 ? quantumScaled[sourceOffset + 2] : quantumScaled[sourceOffset + 0]) / Quantum.Max;
                rgba[destOffset + 3] = channelCount > 3 ? quantumScaled[sourceOffset + 3] / Quantum.Max : 1f;
            }

            return new FloatImageAsset { Id = filePath, Width = (ushort)width, Height = (ushort)height, Pixels = rgba };
        }

        public static (int Width, int Height) ReadDimensions(string filePath) {
            using var image = new MagickImage(filePath);
            return ((int)image.Width, (int)image.Height);
        }
    }
}
```

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test engine/helengine.vfx.io.tests/helengine.vfx.io.tests.csproj`
Expected: 1 passed.

- [ ] **Step 8: Add both new projects to the solution**

Edit `helengine.ui/helengine.sln`. Insert immediately after the `helengine.vfx.tests` project block added in Task 2:

```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "helengine.vfx.io", "..\engine\helengine.vfx.io\helengine.vfx.io.csproj", "{1058EA22-05A9-4CB3-B3D6-8C851B12C7D4}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "helengine.vfx.io.tests", "..\engine\helengine.vfx.io.tests\helengine.vfx.io.tests.csproj", "{F945B9E5-77DB-4497-8897-A83D03405089}"
EndProject
```

Then insert immediately after the `helengine.vfx.tests` configuration lines added in Task 2:

```
		{1058EA22-05A9-4CB3-B3D6-8C851B12C7D4}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{1058EA22-05A9-4CB3-B3D6-8C851B12C7D4}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{1058EA22-05A9-4CB3-B3D6-8C851B12C7D4}.Debug|x64.ActiveCfg = Debug|Any CPU
		{1058EA22-05A9-4CB3-B3D6-8C851B12C7D4}.Debug|x64.Build.0 = Debug|Any CPU
		{1058EA22-05A9-4CB3-B3D6-8C851B12C7D4}.Debug|x86.ActiveCfg = Debug|Any CPU
		{1058EA22-05A9-4CB3-B3D6-8C851B12C7D4}.Debug|x86.Build.0 = Debug|Any CPU
		{1058EA22-05A9-4CB3-B3D6-8C851B12C7D4}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{1058EA22-05A9-4CB3-B3D6-8C851B12C7D4}.Release|Any CPU.Build.0 = Release|Any CPU
		{1058EA22-05A9-4CB3-B3D6-8C851B12C7D4}.Release|x64.ActiveCfg = Release|Any CPU
		{1058EA22-05A9-4CB3-B3D6-8C851B12C7D4}.Release|x64.Build.0 = Release|Any CPU
		{1058EA22-05A9-4CB3-B3D6-8C851B12C7D4}.Release|x86.ActiveCfg = Release|Any CPU
		{1058EA22-05A9-4CB3-B3D6-8C851B12C7D4}.Release|x86.Build.0 = Release|Any CPU
		{F945B9E5-77DB-4497-8897-A83D03405089}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{F945B9E5-77DB-4497-8897-A83D03405089}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{F945B9E5-77DB-4497-8897-A83D03405089}.Debug|x64.ActiveCfg = Debug|Any CPU
		{F945B9E5-77DB-4497-8897-A83D03405089}.Debug|x64.Build.0 = Debug|Any CPU
		{F945B9E5-77DB-4497-8897-A83D03405089}.Debug|x86.ActiveCfg = Debug|Any CPU
		{F945B9E5-77DB-4497-8897-A83D03405089}.Debug|x86.Build.0 = Debug|Any CPU
		{F945B9E5-77DB-4497-8897-A83D03405089}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{F945B9E5-77DB-4497-8897-A83D03405089}.Release|Any CPU.Build.0 = Release|Any CPU
		{F945B9E5-77DB-4497-8897-A83D03405089}.Release|x64.ActiveCfg = Release|Any CPU
		{F945B9E5-77DB-4497-8897-A83D03405089}.Release|x64.Build.0 = Release|Any CPU
		{F945B9E5-77DB-4497-8897-A83D03405089}.Release|x86.ActiveCfg = Release|Any CPU
		{F945B9E5-77DB-4497-8897-A83D03405089}.Release|x86.Build.0 = Release|Any CPU
```

- [ ] **Step 9: Commit**

```bash
git add engine/helengine.vfx.io engine/helengine.vfx.io.tests helengine.ui/helengine.sln
git commit -m "feat: add ExrFrameReader/ExrFrameWriter backed by Magick.NET Q16-HDRI"
```

---

### Task 7: `ExrSequenceReader`

**Files:**
- Create: `engine/helengine.vfx.io/ExrSequenceReader.cs`
- Create: `engine/helengine.vfx.io.tests/ExrSequenceReaderTests.cs`

**Interfaces:**
- Consumes: `ImageSequence` (Task 2), `ExrFrameReader.ReadDimensions` (Task 6).
- Produces: `public static class ExrSequenceReader { public static ImageSequence ReadSequence(string folderPath); }`. Sorts files in the folder by the last run of digits in each filename (e.g. `frame.0007.exr` → 7), not alphabetically, so 10-frame+ sequences sort correctly.

- [ ] **Step 1: Write the failing tests**

```csharp
using helengine.vfx;
using helengine.vfx.io;
using Xunit;

namespace helengine.vfx.io.tests {
    public class ExrSequenceReaderTests {
        [Fact]
        public void ReadSequence_MissingFolder_Throws() {
            string missingFolder = Path.Combine(Path.GetTempPath(), "helengine-vfx-io-tests-missing-" + Guid.NewGuid().ToString("N"));

            Assert.Throws<DirectoryNotFoundException>(() => ExrSequenceReader.ReadSequence(missingFolder));
        }

        [Fact]
        public void ReadSequence_SortsFramesNumerically_NotAlphabetically() {
            string folder = Path.Combine(Path.GetTempPath(), "helengine-vfx-io-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try {
                WriteFrame(folder, "frame.0010.exr");
                WriteFrame(folder, "frame.0002.exr");
                WriteFrame(folder, "frame.0001.exr");

                ImageSequence sequence = ExrSequenceReader.ReadSequence(folder);

                Assert.Equal(3, sequence.FrameCount);
                Assert.EndsWith("frame.0001.exr", sequence.FramePaths[0]);
                Assert.EndsWith("frame.0002.exr", sequence.FramePaths[1]);
                Assert.EndsWith("frame.0010.exr", sequence.FramePaths[2]);
                Assert.Equal(2, sequence.Width);
                Assert.Equal(2, sequence.Height);
            } finally {
                Directory.Delete(folder, recursive: true);
            }
        }

        static void WriteFrame(string folder, string fileName) {
            var asset = new helengine.FloatImageAsset { Width = 2, Height = 2, Pixels = new float[2 * 2 * 4] };
            ExrFrameWriter.WriteFrame(asset, Path.Combine(folder, fileName));
            asset.Dispose();
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test engine/helengine.vfx.io.tests/helengine.vfx.io.tests.csproj`
Expected: FAIL (compile error — `ExrSequenceReader` does not exist yet).

- [ ] **Step 3: Implement `ExrSequenceReader`**

```csharp
using System.Globalization;
using System.Text.RegularExpressions;

namespace helengine.vfx.io {
    /// <summary>
    /// Discovers EXR frame files in a folder and builds an ImageSequence, sorted by the numeric
    /// frame index embedded in each filename (e.g. frame.0007.exr).
    /// </summary>
    public static class ExrSequenceReader {
        static readonly Regex FrameNumberPattern = new Regex(@"(\d+)(?!.*\d)", RegexOptions.Compiled);

        public static ImageSequence ReadSequence(string folderPath) {
            if (string.IsNullOrWhiteSpace(folderPath)) {
                throw new ArgumentException("Folder path must be provided.", nameof(folderPath));
            }
            if (!Directory.Exists(folderPath)) {
                throw new DirectoryNotFoundException($"Image sequence folder '{folderPath}' does not exist.");
            }

            string[] files = Directory.GetFiles(folderPath, "*.exr");
            if (files.Length == 0) {
                throw new InvalidOperationException($"Image sequence folder '{folderPath}' contains no .exr files.");
            }

            string[] sorted = files
                .OrderBy(path => ExtractFrameNumber(path))
                .ToArray();

            (int width, int height) = ExrFrameReader.ReadDimensions(sorted[0]);

            return new ImageSequence(sorted, width, height);
        }

        static int ExtractFrameNumber(string path) {
            string fileName = Path.GetFileNameWithoutExtension(path);
            Match match = FrameNumberPattern.Match(fileName);
            if (!match.Success) {
                throw new InvalidOperationException($"File '{path}' does not contain a numeric frame index in its name.");
            }
            return int.Parse(match.Value, CultureInfo.InvariantCulture);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test engine/helengine.vfx.io.tests/helengine.vfx.io.tests.csproj`
Expected: all passed (1 from before + 2 new = 3).

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.vfx.io/ExrSequenceReader.cs engine/helengine.vfx.io.tests/ExrSequenceReaderTests.cs
git commit -m "feat: add ExrSequenceReader for numeric frame discovery"
```

---

### Task 8: `helengine.vfx.directx11` project — headless `DirectX11VfxDevice`

**Files:**
- Create: `engine/helengine.vfx.directx11/helengine.vfx.directx11.csproj`
- Create: `engine/helengine.vfx.directx11/DirectX11VfxDevice.cs`
- Modify: `helengine.ui/helengine.sln`

**Interfaces:**
- Produces: `public sealed class DirectX11VfxDevice : IDisposable { public SharpDX.Direct3D11.Device Device { get; } }`. Task 9's `DirectX11VfxEffectRunner` consumes this.

This device-creation pattern (adapter from `DXGI.Factory1.GetAdapter1(0)`, then `new Device(adapter, DeviceCreationFlags.None, featureLevels[])`, no swap chain) has been spiked directly on this machine and confirmed to create a real Direct3D11 device successfully.

- [ ] **Step 1: Create the `helengine.vfx.directx11` project**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="SharpDX" Version="4.2.0" />
    <PackageReference Include="SharpDX.Direct3D11" Version="4.2.0" />
    <PackageReference Include="SharpDX.DXGI" Version="4.2.0" />
    <PackageReference Include="SharpDX.D3DCompiler" Version="4.2.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\helengine.core\helengine.core.csproj" />
    <ProjectReference Include="..\helengine.vfx\helengine.vfx.csproj" />
    <ProjectReference Include="..\helengine.vfx.io\helengine.vfx.io.csproj" />
    <ProjectReference Include="..\helengine.shader\helengine.shader.csproj" />
    <ProjectReference Include="..\helengine.shader.compilation\helengine.shader.compilation.csproj" />
    <ProjectReference Include="..\helengine.directx11\helengine.directx11.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Include="shaders\**\*.hlsl">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

Save as `engine/helengine.vfx.directx11/helengine.vfx.directx11.csproj`.

- [ ] **Step 2: Implement `DirectX11VfxDevice`**

```csharp
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using D3DDevice = SharpDX.Direct3D11.Device;
using DxgiFactory1 = SharpDX.DXGI.Factory1;

namespace helengine.vfx.directx11 {
    /// <summary>
    /// A headless Direct3D11 device with no swap chain, used to run VFX effect shaders offline.
    /// </summary>
    public sealed class DirectX11VfxDevice : IDisposable {
        public D3DDevice Device { get; }

        public DirectX11VfxDevice() {
            Adapter1 adapter;
            using (var factory = new DxgiFactory1()) {
                adapter = factory.GetAdapter1(0);
            }

            using (adapter) {
                Device = new D3DDevice(adapter, DeviceCreationFlags.None, new[] {
                    FeatureLevel.Level_11_1,
                    FeatureLevel.Level_11_0,
                    FeatureLevel.Level_10_0
                });
            }
        }

        public void Dispose() {
            Device.Dispose();
        }
    }
}
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build engine/helengine.vfx.directx11/helengine.vfx.directx11.csproj`
Expected: Build succeeded.

There is no automated test for this step in isolation — device creation requires real Direct3D11 hardware/driver support, and is exercised for real by the end-to-end test in Task 11.

- [ ] **Step 4: Add the new project to the solution**

Edit `helengine.ui/helengine.sln`. Insert immediately after the `helengine.vfx.io.tests` project block added in Task 6:

```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "helengine.vfx.directx11", "..\engine\helengine.vfx.directx11\helengine.vfx.directx11.csproj", "{E6F6125E-60B3-4C3E-83F7-A0E1AA825B1B}"
EndProject
```

Then insert immediately after the `helengine.vfx.io.tests` configuration lines added in Task 6:

```
		{E6F6125E-60B3-4C3E-83F7-A0E1AA825B1B}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{E6F6125E-60B3-4C3E-83F7-A0E1AA825B1B}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{E6F6125E-60B3-4C3E-83F7-A0E1AA825B1B}.Debug|x64.ActiveCfg = Debug|Any CPU
		{E6F6125E-60B3-4C3E-83F7-A0E1AA825B1B}.Debug|x64.Build.0 = Debug|Any CPU
		{E6F6125E-60B3-4C3E-83F7-A0E1AA825B1B}.Debug|x86.ActiveCfg = Debug|Any CPU
		{E6F6125E-60B3-4C3E-83F7-A0E1AA825B1B}.Debug|x86.Build.0 = Debug|Any CPU
		{E6F6125E-60B3-4C3E-83F7-A0E1AA825B1B}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{E6F6125E-60B3-4C3E-83F7-A0E1AA825B1B}.Release|Any CPU.Build.0 = Release|Any CPU
		{E6F6125E-60B3-4C3E-83F7-A0E1AA825B1B}.Release|x64.ActiveCfg = Release|Any CPU
		{E6F6125E-60B3-4C3E-83F7-A0E1AA825B1B}.Release|x64.Build.0 = Release|Any CPU
		{E6F6125E-60B3-4C3E-83F7-A0E1AA825B1B}.Release|x86.ActiveCfg = Release|Any CPU
		{E6F6125E-60B3-4C3E-83F7-A0E1AA825B1B}.Release|x86.Build.0 = Release|Any CPU
```

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.vfx.directx11 helengine.ui/helengine.sln
git commit -m "feat: add headless DirectX11VfxDevice"
```

---

### Task 9: `RainbowExpand.hlsl` and `DirectX11VfxEffectRunner`

**Files:**
- Create: `engine/helengine.vfx.directx11/shaders/effects/RainbowExpand.hlsl`
- Create: `engine/helengine.vfx.directx11/DirectX11VfxEffectRunner.cs`

**Interfaces:**
- Consumes: `DirectX11VfxDevice` (Task 8), `IVfxEffect`, `VfxClip`, `VfxFrameConstants` (Task 2/4), `ExrFrameReader`/`ExrFrameWriter` (Task 6), `FloatImageAsset` (Task 1).
- Produces: `public sealed class DirectX11VfxEffectRunner : IDisposable { public DirectX11VfxEffectRunner(DirectX11VfxDevice vfxDevice, IVfxEffect effect); public void Run(VfxClip clip, IVfxEffect effect, IReadOnlyDictionary<string, string> parameterValues, string outputFolder, string frameFileNamePattern = "frame.{0:D4}.exr"); }`.

The shader-compile call (`ShaderCompileService.CompileFromFile` → `DirectX11ShaderBackend` → real bytecode) and every SharpDX call below (immutable-texture-from-pinned-float-array upload, offscreen float render target, staging-texture readback with `RowPitch`-aware `Marshal.Copy`, dynamic constant buffer update) have all been spiked directly against the real engine assemblies and SharpDX 4.2.0 on this machine and confirmed working.

- [ ] **Step 1: Write `RainbowExpand.hlsl`**

```hlsl
cbuffer VfxFrameConstants : register(b0)
{
    float NormalizedTime;
    float2 Resolution;
    float Reserved;
    float4 Params0; // x: HueCyclesPerClip, y: StartScale, z: EndScale, w: Easing kind (0=Linear,1=EaseIn,2=EaseOut,3=EaseInOut)
    float4 Params1; // xyz: BackgroundColor, w: unused
    float4 Params2; // unused
    float4 Params3; // unused
};

Texture2D SourceTexture : register(t0);
Texture2D MaskTexture : register(t1);
SamplerState LinearClampSampler : register(s0);

struct PSInput
{
    float4 Position : SV_POSITION;
    float2 UV : TEXCOORD0;
};

// Big-triangle fullscreen technique: 3 vertices, no vertex buffer, clipped to the viewport.
PSInput FullscreenVS(uint vertexId : SV_VertexID)
{
    PSInput output;
    float2 ndc = float2((vertexId << 1) & 2, vertexId & 2) * 2.0 - 1.0;
    output.Position = float4(ndc, 0, 1);
    output.UV = float2((ndc.x + 1.0) * 0.5, 0.5 - (ndc.y * 0.5));
    return output;
}

// Must stay in sync with helengine.vfx.VfxEasing.Apply.
float ApplyEasing(float t, float easingKind)
{
    float clamped = saturate(t);
    if (easingKind < 0.5) // Linear
    {
        return clamped;
    }
    if (easingKind < 1.5) // EaseIn
    {
        return clamped * clamped;
    }
    if (easingKind < 2.5) // EaseOut
    {
        return 1.0 - ((1.0 - clamped) * (1.0 - clamped));
    }
    // EaseInOut
    if (clamped < 0.5)
    {
        return 2.0 * clamped * clamped;
    }
    float inverted = (-2.0 * clamped) + 2.0;
    return 1.0 - ((inverted * inverted) / 2.0);
}

float3 HueRotate(float3 color, float hueDegrees)
{
    float angle = radians(hueDegrees);
    float cosA = cos(angle);
    float sinA = sin(angle);

    float3x3 rotation = float3x3(
        0.299 + (0.701 * cosA) + (0.168 * sinA), 0.587 - (0.587 * cosA) + (0.330 * sinA), 0.114 - (0.114 * cosA) - (0.497 * sinA),
        0.299 - (0.299 * cosA) - (0.328 * sinA), 0.587 + (0.413 * cosA) + (0.035 * sinA), 0.114 - (0.114 * cosA) + (0.292 * sinA),
        0.299 - (0.300 * cosA) + (1.250 * sinA), 0.587 - (0.588 * cosA) - (1.050 * sinA), 0.114 + (0.886 * cosA) - (0.203 * sinA));

    return mul(rotation, color);
}

float4 RainbowExpandPS(PSInput input) : SV_TARGET
{
    float hueCyclesPerClip = Params0.x;
    float startScale = Params0.y;
    float endScale = Params0.z;
    float easingKind = Params0.w;
    float3 backgroundColor = Params1.xyz;

    float t = ApplyEasing(NormalizedTime, easingKind);
    float scale = lerp(startScale, endScale, t);

    float2 centeredUV = (input.UV - 0.5) / max(scale, 0.0001);
    float2 sampleUV = centeredUV + 0.5;

    bool inBounds = all(sampleUV >= 0.0) && all(sampleUV <= 1.0);
    if (!inBounds)
    {
        return float4(backgroundColor, 1.0);
    }

    float alpha = MaskTexture.Sample(LinearClampSampler, sampleUV).a;
    float3 sourceColor = SourceTexture.Sample(LinearClampSampler, sampleUV).rgb;
    float3 huedColor = HueRotate(sourceColor, 360.0 * hueCyclesPerClip * t);

    float3 finalColor = lerp(backgroundColor, huedColor, alpha);
    return float4(finalColor, 1.0);
}
```

- [ ] **Step 2: Implement `DirectX11VfxEffectRunner`**

```csharp
using System.Runtime.InteropServices;
using helengine.vfx.io;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using D3DBuffer = SharpDX.Direct3D11.Buffer;
using D3DDevice = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace helengine.vfx.directx11 {
    /// <summary>
    /// Runs one VFX effect over every frame of a clip using a headless DirectX11 device, writing
    /// each processed frame out as an EXR file.
    /// </summary>
    public sealed class DirectX11VfxEffectRunner : IDisposable {
        readonly D3DDevice device;
        readonly DeviceContext context;
        readonly VertexShader vertexShader;
        readonly PixelShader pixelShader;
        readonly SamplerState sampler;
        readonly D3DBuffer constantBuffer;

        Texture2D renderTarget;
        RenderTargetView renderTargetView;
        Texture2D stagingTexture;
        int targetWidth;
        int targetHeight;

        public DirectX11VfxEffectRunner(DirectX11VfxDevice vfxDevice, IVfxEffect effect) {
            if (vfxDevice == null) {
                throw new ArgumentNullException(nameof(vfxDevice));
            }
            if (effect == null) {
                throw new ArgumentNullException(nameof(effect));
            }

            device = vfxDevice.Device;
            context = device.ImmediateContext;

            ShaderCompileService compileService = CreateCompileService();
            ShaderCompileResult vsResult = CompileEntryPoint(compileService, effect, effect.VertexEntryPoint, ShaderStage.Vertex);
            ShaderCompileResult psResult = CompileEntryPoint(compileService, effect, effect.PixelEntryPoint, ShaderStage.Pixel);

            vertexShader = new VertexShader(device, vsResult.Binary.Bytecode);
            pixelShader = new PixelShader(device, psResult.Binary.Bytecode);

            sampler = new SamplerState(device, new SamplerStateDescription {
                Filter = Filter.MinMagMipLinear,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                MaximumLod = float.MaxValue
            });

            constantBuffer = new D3DBuffer(device, new BufferDescription {
                SizeInBytes = VfxFrameConstants.TotalFloatCount * sizeof(float),
                Usage = ResourceUsage.Dynamic,
                BindFlags = BindFlags.ConstantBuffer,
                CpuAccessFlags = CpuAccessFlags.Write
            });
        }

        static ShaderCompileService CreateCompileService() {
            var includeResolver = new ShaderFilesystemIncludeResolver(AppContext.BaseDirectory);
            var cache = new ShaderMemoryCompileCache();
            var hasher = new ShaderSourceHasher();
            var service = new ShaderCompileService(includeResolver, cache, hasher);
            service.RegisterBackend(new DirectX11ShaderBackend());
            return service;
        }

        static ShaderCompileResult CompileEntryPoint(ShaderCompileService compileService, IVfxEffect effect, string entryPoint, ShaderStage stage) {
            string path = Path.Combine(AppContext.BaseDirectory, effect.ShaderResourcePath);
            var options = new ShaderCompileOptions(ShaderBindingPolicies.Default, generateDebugInfo: false, optimize: true, treatWarningsAsErrors: false);
            ShaderCompileResult result = compileService.CompileFromFile(
                path,
                effect.Id + "." + entryPoint,
                entryPoint,
                stage,
                ShaderCompileTarget.DirectX11,
                new ShaderModel(4, 0),
                "default",
                Array.Empty<ShaderDefine>(),
                options);

            if (!result.Success) {
                throw new InvalidOperationException($"Failed to compile '{entryPoint}' in '{path}'.");
            }

            return result;
        }

        public void Run(VfxClip clip, IVfxEffect effect, IReadOnlyDictionary<string, string> parameterValues, string outputFolder, string frameFileNamePattern = "frame.{0:D4}.exr") {
            if (clip == null) {
                throw new ArgumentNullException(nameof(clip));
            }
            if (effect == null) {
                throw new ArgumentNullException(nameof(effect));
            }
            if (parameterValues == null) {
                throw new ArgumentNullException(nameof(parameterValues));
            }
            if (string.IsNullOrWhiteSpace(outputFolder)) {
                throw new ArgumentException("Output folder must be provided.", nameof(outputFolder));
            }

            Directory.CreateDirectory(outputFolder);
            EnsureRenderTarget(clip.Width, clip.Height);

            float[] paramSlots = effect.ResolveParameterSlots(parameterValues);

            for (int frameIndex = 0; frameIndex < clip.FrameCount; frameIndex++) {
                FloatImageAsset sourceFrame = ExrFrameReader.ReadFrame(clip.Source.FramePaths[frameIndex]);
                FloatImageAsset maskFrame = ExrFrameReader.ReadFrame(clip.Mask.FramePaths[frameIndex]);

                using Texture2D sourceTexture = CreateInputTexture(sourceFrame);
                using ShaderResourceView sourceView = new ShaderResourceView(device, sourceTexture);
                using Texture2D maskTexture = CreateInputTexture(maskFrame);
                using ShaderResourceView maskView = new ShaderResourceView(device, maskTexture);

                sourceFrame.Dispose();
                maskFrame.Dispose();

                float normalizedTime = clip.FrameCount > 1 ? (float)frameIndex / (clip.FrameCount - 1) : 0f;
                UpdateConstantBuffer(normalizedTime, clip.Width, clip.Height, paramSlots);

                DrawFrame(sourceView, maskView);

                FloatImageAsset outputFrame = ReadBackFrame(clip.Width, clip.Height);
                string outputPath = Path.Combine(outputFolder, string.Format(frameFileNamePattern, frameIndex));
                ExrFrameWriter.WriteFrame(outputFrame, outputPath);
                outputFrame.Dispose();
            }
        }

        void EnsureRenderTarget(int width, int height) {
            if (renderTarget != null && targetWidth == width && targetHeight == height) {
                return;
            }

            renderTargetView?.Dispose();
            renderTarget?.Dispose();
            stagingTexture?.Dispose();

            var colorDescription = new Texture2DDescription {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.R32G32B32A32_Float,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            };
            renderTarget = new Texture2D(device, colorDescription);
            renderTargetView = new RenderTargetView(device, renderTarget);

            var stagingDescription = colorDescription;
            stagingDescription.BindFlags = BindFlags.None;
            stagingDescription.Usage = ResourceUsage.Staging;
            stagingDescription.CpuAccessFlags = CpuAccessFlags.Read;
            stagingTexture = new Texture2D(device, stagingDescription);

            targetWidth = width;
            targetHeight = height;
        }

        Texture2D CreateInputTexture(FloatImageAsset frame) {
            GCHandle handle = GCHandle.Alloc(frame.Pixels, GCHandleType.Pinned);
            try {
                var description = new Texture2DDescription {
                    Width = frame.Width,
                    Height = frame.Height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = Format.R32G32B32A32_Float,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Immutable,
                    BindFlags = BindFlags.ShaderResource,
                    CpuAccessFlags = CpuAccessFlags.None,
                    OptionFlags = ResourceOptionFlags.None
                };
                var dataRectangle = new DataRectangle(handle.AddrOfPinnedObject(), frame.Width * 4 * sizeof(float));
                return new Texture2D(device, description, dataRectangle);
            } finally {
                handle.Free();
            }
        }

        void UpdateConstantBuffer(float normalizedTime, int width, int height, float[] paramSlots) {
            float[] frameConstants = VfxFrameConstants.Build(normalizedTime, width, height, paramSlots);
            DataBox box = context.MapSubresource(constantBuffer, 0, MapMode.WriteDiscard, MapFlags.None);
            Marshal.Copy(frameConstants, 0, box.DataPointer, frameConstants.Length);
            context.UnmapSubresource(constantBuffer, 0);
        }

        void DrawFrame(ShaderResourceView sourceView, ShaderResourceView maskView) {
            context.OutputMerger.SetRenderTargets(renderTargetView);
            context.Rasterizer.SetViewport(0, 0, targetWidth, targetHeight, 0f, 1f);
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.InputAssembler.InputLayout = null;
            context.VertexShader.Set(vertexShader);
            context.PixelShader.Set(pixelShader);
            context.PixelShader.SetConstantBuffer(0, constantBuffer);
            context.PixelShader.SetShaderResource(0, sourceView);
            context.PixelShader.SetShaderResource(1, maskView);
            context.PixelShader.SetSampler(0, sampler);

            context.Draw(3, 0);

            context.PixelShader.SetShaderResource(0, null);
            context.PixelShader.SetShaderResource(1, null);
        }

        FloatImageAsset ReadBackFrame(int width, int height) {
            context.CopyResource(renderTarget, stagingTexture);
            DataBox dataBox = context.MapSubresource(stagingTexture, 0, MapMode.Read, MapFlags.None);
            try {
                float[] pixels = new float[width * height * 4];
                int rowFloats = width * 4;
                for (int y = 0; y < height; y++) {
                    IntPtr rowPointer = dataBox.DataPointer + (y * dataBox.RowPitch);
                    Marshal.Copy(rowPointer, pixels, y * rowFloats, rowFloats);
                }
                return new FloatImageAsset { Width = (ushort)width, Height = (ushort)height, Pixels = pixels };
            } finally {
                context.UnmapSubresource(stagingTexture, 0);
            }
        }

        public void Dispose() {
            stagingTexture?.Dispose();
            renderTargetView?.Dispose();
            renderTarget?.Dispose();
            constantBuffer.Dispose();
            sampler.Dispose();
            pixelShader.Dispose();
            vertexShader.Dispose();
        }
    }
}
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build engine/helengine.vfx.directx11/helengine.vfx.directx11.csproj`
Expected: Build succeeded.

There is no isolated unit test for this task — it is exercised fully by the end-to-end test in Task 11, which is the meaningful test boundary for a real-GPU pass per the design doc's testing strategy.

- [ ] **Step 4: Commit**

```bash
git add engine/helengine.vfx.directx11/shaders engine/helengine.vfx.directx11/DirectX11VfxEffectRunner.cs
git commit -m "feat: add RainbowExpand shader and DirectX11VfxEffectRunner"
```

---

### Task 10: `helengine.vfx.cli` project — argument parsing and `Program.cs`

**Files:**
- Create: `engine/helengine.vfx.cli/helengine.vfx.cli.csproj`
- Create: `engine/helengine.vfx.cli/VfxCliArguments.cs`
- Create: `engine/helengine.vfx.cli/Program.cs`
- Create: `engine/helengine.vfx.cli.tests/helengine.vfx.cli.tests.csproj`
- Create: `engine/helengine.vfx.cli.tests/VfxCliArgumentsTests.cs`
- Modify: `helengine.ui/helengine.sln`

**Interfaces:**
- Produces: `public class VfxCliArguments { public string SourceFolder { get; } public string MaskFolder { get; } public string EffectId { get; } public string OutputFolder { get; } public IReadOnlyDictionary<string, string> ParameterValues { get; } public static bool TryParse(string[] args, out VfxCliArguments parsed, out string error); }`.

- [ ] **Step 1: Create the `helengine.vfx.cli` project**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\helengine.vfx\helengine.vfx.csproj" />
    <ProjectReference Include="..\helengine.vfx.io\helengine.vfx.io.csproj" />
    <ProjectReference Include="..\helengine.vfx.directx11\helengine.vfx.directx11.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Include="..\helengine.vfx.directx11\shaders\**\*.hlsl">
      <Link>shaders\%(RecursiveDir)%(Filename)%(Extension)</Link>
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

Save as `engine/helengine.vfx.cli/helengine.vfx.cli.csproj`.

- [ ] **Step 2: Create the `helengine.vfx.cli.tests` project**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <Using Include="Xunit" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\helengine.core\helengine.core.csproj" />
    <ProjectReference Include="..\helengine.vfx\helengine.vfx.csproj" />
    <ProjectReference Include="..\helengine.vfx.io\helengine.vfx.io.csproj" />
    <ProjectReference Include="..\helengine.vfx.directx11\helengine.vfx.directx11.csproj" />
    <ProjectReference Include="..\helengine.vfx.cli\helengine.vfx.cli.csproj" />
  </ItemGroup>
</Project>
```

Save as `engine/helengine.vfx.cli.tests/helengine.vfx.cli.tests.csproj`.

Note: referencing an `Exe`-output project (`helengine.vfx.cli`) from a test project is unusual but valid in .NET SDK projects — the test project can still call the referenced exe's public types.

- [ ] **Step 3: Write the failing tests**

```csharp
using helengine.vfx.cli;
using Xunit;

namespace helengine.vfx.cli.tests {
    public class VfxCliArgumentsTests {
        [Fact]
        public void TryParse_AllRequiredArguments_Succeeds() {
            string[] args = { "--source", "src", "--mask", "mask", "--effect", "rainbow-expand", "--out", "out" };

            bool result = VfxCliArguments.TryParse(args, out VfxCliArguments parsed, out string error);

            Assert.True(result);
            Assert.Null(error);
            Assert.Equal("src", parsed.SourceFolder);
            Assert.Equal("mask", parsed.MaskFolder);
            Assert.Equal("rainbow-expand", parsed.EffectId);
            Assert.Equal("out", parsed.OutputFolder);
        }

        [Fact]
        public void TryParse_MissingRequiredArgument_Fails() {
            string[] args = { "--source", "src", "--mask", "mask", "--effect", "rainbow-expand" };

            bool result = VfxCliArguments.TryParse(args, out VfxCliArguments parsed, out string error);

            Assert.False(result);
            Assert.Null(parsed);
            Assert.NotNull(error);
        }

        [Fact]
        public void TryParse_ParamArguments_AreCollected() {
            string[] args = { "--source", "src", "--mask", "mask", "--effect", "rainbow-expand", "--out", "out", "--param", "HueCyclesPerClip=2", "--param", "StartScale=0.5" };

            bool result = VfxCliArguments.TryParse(args, out VfxCliArguments parsed, out string error);

            Assert.True(result);
            Assert.Equal("2", parsed.ParameterValues["HueCyclesPerClip"]);
            Assert.Equal("0.5", parsed.ParameterValues["StartScale"]);
        }

        [Fact]
        public void TryParse_MalformedParam_Fails() {
            string[] args = { "--source", "src", "--mask", "mask", "--effect", "rainbow-expand", "--out", "out", "--param", "NoEqualsSign" };

            bool result = VfxCliArguments.TryParse(args, out VfxCliArguments parsed, out string error);

            Assert.False(result);
            Assert.Null(parsed);
        }

        [Fact]
        public void TryParse_UnknownArgument_Fails() {
            string[] args = { "--nonsense", "value" };

            bool result = VfxCliArguments.TryParse(args, out VfxCliArguments parsed, out string error);

            Assert.False(result);
            Assert.Null(parsed);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test engine/helengine.vfx.cli.tests/helengine.vfx.cli.tests.csproj`
Expected: FAIL (compile error — `VfxCliArguments` does not exist yet).

- [ ] **Step 5: Implement `VfxCliArguments`**

```csharp
namespace helengine.vfx.cli {
    /// <summary>
    /// Parsed command-line arguments for the VFX export CLI.
    /// </summary>
    public class VfxCliArguments {
        public string SourceFolder { get; private set; }
        public string MaskFolder { get; private set; }
        public string EffectId { get; private set; }
        public string OutputFolder { get; private set; }
        public IReadOnlyDictionary<string, string> ParameterValues { get; private set; }

        public static bool TryParse(string[] args, out VfxCliArguments parsed, out string error) {
            string sourceFolder = null;
            string maskFolder = null;
            string effectId = null;
            string outputFolder = null;
            var parameterValues = new Dictionary<string, string>();

            for (int i = 0; i < args.Length; i++) {
                switch (args[i]) {
                    case "--source":
                        if (!TryReadValue(args, ref i, out sourceFolder, out error)) { parsed = null; return false; }
                        break;
                    case "--mask":
                        if (!TryReadValue(args, ref i, out maskFolder, out error)) { parsed = null; return false; }
                        break;
                    case "--effect":
                        if (!TryReadValue(args, ref i, out effectId, out error)) { parsed = null; return false; }
                        break;
                    case "--out":
                        if (!TryReadValue(args, ref i, out outputFolder, out error)) { parsed = null; return false; }
                        break;
                    case "--param":
                        if (!TryReadValue(args, ref i, out string paramText, out error)) { parsed = null; return false; }
                        string[] parts = paramText.Split('=', 2);
                        if (parts.Length != 2) {
                            parsed = null;
                            error = $"Invalid --param value '{paramText}'. Expected name=value.";
                            return false;
                        }
                        parameterValues[parts[0]] = parts[1];
                        break;
                    default:
                        parsed = null;
                        error = $"Unknown argument '{args[i]}'.";
                        return false;
                }
            }

            if (sourceFolder == null || maskFolder == null || effectId == null || outputFolder == null) {
                parsed = null;
                error = "Usage: helengine.vfx.cli --source <folder> --mask <folder> --effect <id> --out <folder> [--param name=value ...]";
                return false;
            }

            parsed = new VfxCliArguments {
                SourceFolder = sourceFolder,
                MaskFolder = maskFolder,
                EffectId = effectId,
                OutputFolder = outputFolder,
                ParameterValues = parameterValues
            };
            error = null;
            return true;
        }

        static bool TryReadValue(string[] args, ref int i, out string value, out string error) {
            if (i + 1 >= args.Length) {
                value = null;
                error = $"Argument '{args[i]}' requires a value.";
                return false;
            }
            i++;
            value = args[i];
            error = null;
            return true;
        }
    }
}
```

- [ ] **Step 6: Implement `Program.cs`**

```csharp
using helengine.vfx;
using helengine.vfx.cli;
using helengine.vfx.directx11;
using helengine.vfx.effects;
using helengine.vfx.io;

VfxEffectRegistry.Register(new RainbowExpandEffect());

if (!VfxCliArguments.TryParse(args, out VfxCliArguments parsedArgs, out string parseError)) {
    Console.Error.WriteLine(parseError);
    return 1;
}

IVfxEffect effect;
try {
    effect = VfxEffectRegistry.Resolve(parsedArgs.EffectId);
} catch (InvalidOperationException ex) {
    Console.Error.WriteLine(ex.Message);
    return 1;
}

VfxClip clip;
try {
    ImageSequence source = ExrSequenceReader.ReadSequence(parsedArgs.SourceFolder);
    ImageSequence mask = ExrSequenceReader.ReadSequence(parsedArgs.MaskFolder);
    clip = new VfxClip(source, mask);
} catch (Exception ex) when (ex is InvalidOperationException || ex is DirectoryNotFoundException) {
    Console.Error.WriteLine(ex.Message);
    return 1;
}

using (var vfxDevice = new DirectX11VfxDevice())
using (var runner = new DirectX11VfxEffectRunner(vfxDevice, effect)) {
    runner.Run(clip, effect, parsedArgs.ParameterValues, parsedArgs.OutputFolder);
}

Console.WriteLine($"Wrote {clip.FrameCount} frame(s) to '{parsedArgs.OutputFolder}'.");
return 0;
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test engine/helengine.vfx.cli.tests/helengine.vfx.cli.tests.csproj`
Expected: 5 passed.

- [ ] **Step 8: Add both new projects to the solution**

Edit `helengine.ui/helengine.sln`. Insert immediately after the `helengine.vfx.directx11` project block added in Task 8:

```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "helengine.vfx.cli", "..\engine\helengine.vfx.cli\helengine.vfx.cli.csproj", "{32AB077B-3C0F-47E3-B500-FC31ABF46146}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "helengine.vfx.cli.tests", "..\engine\helengine.vfx.cli.tests\helengine.vfx.cli.tests.csproj", "{7F15BF52-8E43-48F1-9692-F2F677404F17}"
EndProject
```

Then insert immediately after the `helengine.vfx.directx11` configuration lines added in Task 8:

```
		{32AB077B-3C0F-47E3-B500-FC31ABF46146}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{32AB077B-3C0F-47E3-B500-FC31ABF46146}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{32AB077B-3C0F-47E3-B500-FC31ABF46146}.Debug|x64.ActiveCfg = Debug|Any CPU
		{32AB077B-3C0F-47E3-B500-FC31ABF46146}.Debug|x64.Build.0 = Debug|Any CPU
		{32AB077B-3C0F-47E3-B500-FC31ABF46146}.Debug|x86.ActiveCfg = Debug|Any CPU
		{32AB077B-3C0F-47E3-B500-FC31ABF46146}.Debug|x86.Build.0 = Debug|Any CPU
		{32AB077B-3C0F-47E3-B500-FC31ABF46146}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{32AB077B-3C0F-47E3-B500-FC31ABF46146}.Release|Any CPU.Build.0 = Release|Any CPU
		{32AB077B-3C0F-47E3-B500-FC31ABF46146}.Release|x64.ActiveCfg = Release|Any CPU
		{32AB077B-3C0F-47E3-B500-FC31ABF46146}.Release|x64.Build.0 = Release|Any CPU
		{32AB077B-3C0F-47E3-B500-FC31ABF46146}.Release|x86.ActiveCfg = Release|Any CPU
		{32AB077B-3C0F-47E3-B500-FC31ABF46146}.Release|x86.Build.0 = Release|Any CPU
		{7F15BF52-8E43-48F1-9692-F2F677404F17}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{7F15BF52-8E43-48F1-9692-F2F677404F17}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{7F15BF52-8E43-48F1-9692-F2F677404F17}.Debug|x64.ActiveCfg = Debug|Any CPU
		{7F15BF52-8E43-48F1-9692-F2F677404F17}.Debug|x64.Build.0 = Debug|Any CPU
		{7F15BF52-8E43-48F1-9692-F2F677404F17}.Debug|x86.ActiveCfg = Debug|Any CPU
		{7F15BF52-8E43-48F1-9692-F2F677404F17}.Debug|x86.Build.0 = Debug|Any CPU
		{7F15BF52-8E43-48F1-9692-F2F677404F17}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{7F15BF52-8E43-48F1-9692-F2F677404F17}.Release|Any CPU.Build.0 = Release|Any CPU
		{7F15BF52-8E43-48F1-9692-F2F677404F17}.Release|x64.ActiveCfg = Release|Any CPU
		{7F15BF52-8E43-48F1-9692-F2F677404F17}.Release|x64.Build.0 = Release|Any CPU
		{7F15BF52-8E43-48F1-9692-F2F677404F17}.Release|x86.ActiveCfg = Release|Any CPU
		{7F15BF52-8E43-48F1-9692-F2F677404F17}.Release|x86.Build.0 = Release|Any CPU
```

- [ ] **Step 9: Commit**

```bash
git add engine/helengine.vfx.cli engine/helengine.vfx.cli.tests helengine.ui/helengine.sln
git commit -m "feat: add helengine.vfx.cli export tool"
```

---

### Task 11: End-to-end export test

**Files:**
- Create: `engine/helengine.vfx.cli.tests/EndToEndExportTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1–10.

This is the design doc's "structural end-to-end" test: generate a tiny synthetic source+mask EXR sequence at test time (no checked-in binary fixtures), run the full pipeline (`ExrSequenceReader` → `VfxClip` → `DirectX11VfxEffectRunner` with `RainbowExpandEffect`), and assert the right number of output files exist with the right resolution and non-degenerate pixel data. This test requires a real Direct3D11-capable environment — note that explicitly rather than silently assuming it always runs.

- [ ] **Step 1: Write the test**

```csharp
using helengine;
using helengine.vfx;
using helengine.vfx.directx11;
using helengine.vfx.effects;
using helengine.vfx.io;
using Xunit;

namespace helengine.vfx.cli.tests {
    public class EndToEndExportTests {
        [Fact]
        public void Run_RainbowExpand_WritesExpectedFrameCountAndResolution() {
            string root = Path.Combine(Path.GetTempPath(), "helengine-vfx-e2e-" + Guid.NewGuid().ToString("N"));
            string sourceFolder = Path.Combine(root, "source");
            string maskFolder = Path.Combine(root, "mask");
            string outputFolder = Path.Combine(root, "output");
            Directory.CreateDirectory(sourceFolder);
            Directory.CreateDirectory(maskFolder);

            const int width = 8;
            const int height = 8;
            const int frameCount = 3;

            try {
                for (int i = 0; i < frameCount; i++) {
                    WriteSolidFrame(Path.Combine(sourceFolder, $"frame.{i:D4}.exr"), width, height, 0.2f, 0.4f, 0.6f, 1f);
                    WriteSolidFrame(Path.Combine(maskFolder, $"frame.{i:D4}.exr"), width, height, 1f, 1f, 1f, 1f);
                }

                ImageSequence source = ExrSequenceReader.ReadSequence(sourceFolder);
                ImageSequence mask = ExrSequenceReader.ReadSequence(maskFolder);
                VfxClip clip = new VfxClip(source, mask);
                IVfxEffect effect = new RainbowExpandEffect();

                using (DirectX11VfxDevice device = new DirectX11VfxDevice())
                using (DirectX11VfxEffectRunner runner = new DirectX11VfxEffectRunner(device, effect)) {
                    runner.Run(clip, effect, new Dictionary<string, string>(), outputFolder);
                }

                string[] outputFiles = Directory.GetFiles(outputFolder, "*.exr");
                Assert.Equal(frameCount, outputFiles.Length);

                foreach (string outputFile in outputFiles) {
                    FloatImageAsset frame = ExrFrameReader.ReadFrame(outputFile);
                    Assert.Equal(width, frame.Width);
                    Assert.Equal(height, frame.Height);
                    Assert.Contains(frame.Pixels, value => value != 0f);
                    frame.Dispose();
                }
            } finally {
                Directory.Delete(root, recursive: true);
            }
        }

        static void WriteSolidFrame(string path, int width, int height, float r, float g, float b, float a) {
            float[] pixels = new float[width * height * 4];
            for (int i = 0; i < width * height; i++) {
                pixels[(i * 4) + 0] = r;
                pixels[(i * 4) + 1] = g;
                pixels[(i * 4) + 2] = b;
                pixels[(i * 4) + 3] = a;
            }
            FloatImageAsset frame = new FloatImageAsset { Width = (ushort)width, Height = (ushort)height, Pixels = pixels };
            ExrFrameWriter.WriteFrame(frame, path);
            frame.Dispose();
        }
    }
}
```

- [ ] **Step 2: Run the test**

Run: `dotnet test engine/helengine.vfx.cli.tests/helengine.vfx.cli.tests.csproj --filter EndToEndExportTests`
Expected: 1 passed, on a machine with a working Direct3D11 device (confirmed available on this development machine).

- [ ] **Step 3: Run the full solution build and test suite**

Run: `dotnet build helengine.ui/helengine.sln`
Expected: Build succeeded, all 8 new projects included.

Run: `dotnet test engine/helengine.core.tests/helengine.core.tests.csproj engine/helengine.vfx.tests/helengine.vfx.tests.csproj engine/helengine.vfx.io.tests/helengine.vfx.io.tests.csproj engine/helengine.vfx.cli.tests/helengine.vfx.cli.tests.csproj`
Expected: all passed.

- [ ] **Step 4: Manually verify one exported clip end to end**

Using two small real EXR sequences (or the synthetic ones the test generates, copied out before cleanup), run:

```bash
dotnet run --project engine/helengine.vfx.cli -- --source <source-folder> --mask <mask-folder> --effect rainbow-expand --out <output-folder> --param HueCyclesPerClip=2 --param StartScale=1 --param EndScale=3 --param Easing=EaseInOut --param BackgroundColor=0,0,0
```

Open a couple of the output `.exr` frames in an image viewer that supports EXR (or re-import them through the editor's existing Magick.NET-backed importer) and visually confirm: the subject grows across the sequence, hue shifts over time, and the background outside the mask is solid black.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.vfx.cli.tests/EndToEndExportTests.cs
git commit -m "test: add end-to-end RainbowExpand export test"
```

---

## Follow-up (explicitly out of scope for this plan, per the design doc)

- Video muxing/encoding of the output EXR sequence into a container format.
- Editor UI (asset browser entries, live preview, parameter pickers).
- Wiring `RainbowExpand.hlsl` into `DirectX11PostProcessChain` as a live, real-time pass.
- Additional effects beyond `RainbowExpand`.
