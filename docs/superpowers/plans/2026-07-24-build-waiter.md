# Build Waiter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a host console tool that launches a platform build and reports success only after the current build process exits successfully and produces fresh required artifacts.

**Architecture:** `Program` parses the waiter command line and creates a `BuildWaiter`. The waiter launches the child command, forwards its output, periodically emits status, then delegates output validation to `BuildArtifactVerifier`. Parsed options and result data remain separate immutable classes so CLI parsing, process execution, and artifact verification can be tested independently.

**Tech Stack:** C# / .NET 9 console application, `System.Diagnostics.Process`, xUnit.

---

## File Structure

- Create: `tools/build-waiter/helengine.buildwaiter.csproj` — standalone `net9.0-windows` executable project with no engine dependency.
- Create: `tools/build-waiter/Program.cs` — console entry point and final stderr/exit-code handling.
- Create: `tools/build-waiter/BuildWaiterOptions.cs` — immutable parsed command configuration.
- Create: `tools/build-waiter/BuildWaiterOptionsParser.cs` — validates `--output`, repeated `--require`, and `--` command separator.
- Create: `tools/build-waiter/BuildArtifactVerifier.cs` — validates containment, freshness, existence, and non-empty required artifacts.
- Create: `tools/build-waiter/BuildArtifactVerificationResult.cs` — immutable success/failure data returned by the verifier.
- Create: `tools/build-waiter/BuildWaiter.cs` — owns the child process, live output forwarding, periodic waiting status, and final verification.
- Create: `tools/build-waiter/BuildWaiterResult.cs` — immutable terminal result reported by the waiter.
- Create: `tools/build-waiter.tests/helengine.buildwaiter.tests.csproj` — xUnit test project referencing the tool project.
- Create: `tools/build-waiter.tests/BuildWaiterOptionsParserTests.cs` — CLI contract and malformed-input tests.
- Create: `tools/build-waiter.tests/BuildArtifactVerifierTests.cs` — filesystem validation tests using unique temporary directories.
- Create: `tools/build-waiter.tests/BuildWaiterTests.cs` — child-process integration test that creates current artifacts.

### Task 1: Create the executable project and command-line parser

**Files:**

- Create: `tools/build-waiter/helengine.buildwaiter.csproj`
- Create: `tools/build-waiter/Program.cs`
- Create: `tools/build-waiter/BuildWaiterOptions.cs`
- Create: `tools/build-waiter/BuildWaiterOptionsParser.cs`
- Create: `tools/build-waiter.tests/helengine.buildwaiter.tests.csproj`
- Create: `tools/build-waiter.tests/BuildWaiterOptionsParserTests.cs`

- [ ] **Step 1: Write failing parser tests**

```csharp
[Fact]
public void Parse_WhenOutputArtifactsAndCommandAreProvided_ReturnsValidatedOptions() {
    BuildWaiterOptions options = BuildWaiterOptionsParser.Parse([
        "--output", "C:\\build-output",
        "--require", "game.iso",
        "--require", "disc/SYSTEM.CNF",
        "--",
        "dotnet", "build", "project.csproj"
    ]);

    Assert.Equal(Path.GetFullPath("C:\\build-output"), options.OutputRootPath);
    Assert.Equal(["game.iso", "disc/SYSTEM.CNF"], options.RequiredArtifactRelativePaths);
    Assert.Equal("dotnet", options.CommandFileName);
    Assert.Equal(["build", "project.csproj"], options.CommandArguments);
}

[Fact]
public void Parse_WhenARequiredPathEscapesOutputRoot_ThrowsArgumentException() {
    Assert.Throws<ArgumentException>(() => BuildWaiterOptionsParser.Parse([
        "--output", "C:\\build-output", "--require", "../game.iso", "--", "dotnet", "build"
    ]));
}
```

- [ ] **Step 2: Run parser tests and verify they fail**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\tools\build-waiter.tests\helengine.buildwaiter.tests.csproj --filter FullyQualifiedName~BuildWaiterOptionsParserTests
```

Expected: compilation fails because the parser and options types do not exist.

- [ ] **Step 3: Add project files and minimal parser implementation**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
</Project>
```

```csharp
public sealed class BuildWaiterOptions {
    public BuildWaiterOptions(string outputRootPath, string[] requiredArtifactRelativePaths, string commandFileName, string[] commandArguments) {
        OutputRootPath = Path.GetFullPath(outputRootPath);
        RequiredArtifactRelativePaths = requiredArtifactRelativePaths;
        CommandFileName = commandFileName;
        CommandArguments = commandArguments;
    }

    public string OutputRootPath { get; }
    public string[] RequiredArtifactRelativePaths { get; }
    public string CommandFileName { get; }
    public string[] CommandArguments { get; }
}
```

Implement `BuildWaiterOptionsParser.Parse` as a left-to-right argument reader. It requires one non-empty `--output`, one or more non-empty `--require` values, exactly one `--` separator, and a non-empty command after the separator. It rejects rooted required paths and any required path whose full resolved path is outside the resolved output root. Add a temporary `Program.Main` returning `1` so the executable project compiles; Task 4 replaces it with the production entry point.

- [ ] **Step 4: Run parser tests and verify they pass**

Run the Step 2 command.

Expected: all `BuildWaiterOptionsParserTests` pass.

- [ ] **Step 5: Commit the parser slice**

```powershell
rtk git -C C:\dev\helworks\helengine add -- tools/build-waiter tools/build-waiter.tests
rtk git -C C:\dev\helworks\helengine commit -m "feat: add build waiter command parser"
```

### Task 2: Verify current-build artifacts independently

**Files:**

- Create: `tools/build-waiter/BuildArtifactVerificationResult.cs`
- Create: `tools/build-waiter/BuildArtifactVerifier.cs`
- Create: `tools/build-waiter.tests/BuildArtifactVerifierTests.cs`

- [ ] **Step 1: Write failing artifact verification tests**

```csharp
[Fact]
public void Verify_WhenArtifactsAreFreshAndNonEmpty_ReturnsSuccess() {
    string outputRootPath = Path.Combine(Path.GetTempPath(), "build-waiter-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(outputRootPath);
    DateTime buildStartedUtc = DateTime.UtcNow.AddSeconds(-1);
    string gameIsoPath = Path.Combine(outputRootPath, "game.iso");
    File.WriteAllText(gameIsoPath, "iso");
    File.SetLastWriteTimeUtc(gameIsoPath, DateTime.UtcNow);

    BuildArtifactVerificationResult result = new BuildArtifactVerifier().Verify(
        outputRootPath,
        ["game.iso"],
        buildStartedUtc);

    Assert.True(result.Succeeded);
}

[Fact]
public void Verify_WhenArtifactPredatesBuildStart_ReturnsFailure() {
    string outputRootPath = Path.Combine(Path.GetTempPath(), "build-waiter-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(outputRootPath);
    string gameIsoPath = Path.Combine(outputRootPath, "game.iso");
    File.WriteAllText(gameIsoPath, "old iso");
    File.SetLastWriteTimeUtc(gameIsoPath, DateTime.UtcNow.AddMinutes(-1));

    BuildArtifactVerificationResult result = new BuildArtifactVerifier().Verify(
        outputRootPath,
        ["game.iso"],
        DateTime.UtcNow);

    Assert.False(result.Succeeded);
    Assert.Contains("stale", result.Message, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run verifier tests and verify they fail**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\tools\build-waiter.tests\helengine.buildwaiter.tests.csproj --filter FullyQualifiedName~BuildArtifactVerifierTests
```

Expected: compilation fails because the verifier types do not exist.

- [ ] **Step 3: Implement artifact verification**

```csharp
public sealed class BuildArtifactVerificationResult {
    public BuildArtifactVerificationResult(bool succeeded, string message) {
        Succeeded = succeeded;
        Message = message;
    }

    public bool Succeeded { get; }
    public string Message { get; }
}
```

`BuildArtifactVerifier.Verify` resolves every required relative path beneath `outputRootPath`, checks that it remains contained by the root, then checks `File.Exists`, `new FileInfo(path).Length > 0`, and `File.GetLastWriteTimeUtc(path) >= buildStartedUtc`. It returns the first detailed failure, otherwise `new BuildArtifactVerificationResult(true, "All required artifacts are fresh.")`.

- [ ] **Step 4: Expand tests and verify them**

Add tests for a missing artifact, a zero-byte artifact, and an artifact path that escapes through `..`. Run the Step 2 command.

Expected: all verifier tests pass.

- [ ] **Step 5: Commit the verifier slice**

```powershell
rtk git -C C:\dev\helworks\helengine add -- tools/build-waiter tools/build-waiter.tests
rtk git -C C:\dev\helworks\helengine commit -m "feat: verify fresh build artifacts"
```

### Task 3: Launch and observe child builds

**Files:**

- Create: `tools/build-waiter/BuildWaiterResult.cs`
- Create: `tools/build-waiter/BuildWaiter.cs`
- Create: `tools/build-waiter.tests/BuildWaiterTests.cs`

- [ ] **Step 1: Write the failing child-process integration test**

```csharp
[Fact]
public async Task WaitAsync_WhenChildWritesRequiredArtifact_ReturnsSuccess() {
    string outputRootPath = Path.Combine(Path.GetTempPath(), "build-waiter-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(outputRootPath);
    BuildWaiterOptions options = new(
        outputRootPath,
        ["game.iso"],
        "cmd.exe",
        ["/c", $"echo iso>{Path.Combine(outputRootPath, "game.iso")}"]);

    BuildWaiterResult result = await new BuildWaiter(new BuildArtifactVerifier()).WaitAsync(options, CancellationToken.None);

    Assert.True(result.Succeeded);
    Assert.Equal(0, result.ExitCode);
}
```

- [ ] **Step 2: Run the integration test and verify it fails**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\tools\build-waiter.tests\helengine.buildwaiter.tests.csproj --filter FullyQualifiedName~BuildWaiterTests
```

Expected: compilation fails because the waiter types do not exist.

- [ ] **Step 3: Implement child-process waiting and result reporting**

```csharp
public sealed class BuildWaiterResult {
    public BuildWaiterResult(bool succeeded, int exitCode, string message) {
        Succeeded = succeeded;
        ExitCode = exitCode;
        Message = message;
    }

    public bool Succeeded { get; }
    public int ExitCode { get; }
    public string Message { get; }
}
```

`BuildWaiter.WaitAsync` records `DateTime.UtcNow`, starts `Process` with `UseShellExecute = false`, redirects stdout and stderr, forwards each output line immediately, and emits `[build-waiter] waiting` every ten seconds while awaiting `WaitForExitAsync`. A non-zero child exit returns failure without artifact verification. A zero child exit invokes `BuildArtifactVerifier.Verify` with the recorded start timestamp and converts its result into `BuildWaiterResult`.

- [ ] **Step 4: Expand and run integration tests**

Add a child command that exits with `7` and a child command that exits `0` without writing `game.iso`. Run the Step 2 command.

Expected: the written-artifact case passes; both failure cases return a non-zero waiter result with a clear message.

- [ ] **Step 5: Commit the waiter slice**

```powershell
rtk git -C C:\dev\helworks\helengine add -- tools/build-waiter tools/build-waiter.tests
rtk git -C C:\dev\helworks\helengine commit -m "feat: wait for verified platform builds"
```

### Task 4: Add the console entry point and PS2 usage documentation

**Files:**

- Create: `tools/build-waiter/Program.cs`
- Modify: `tools/build-waiter/BuildWaiterOptionsParser.cs`
- Modify: `README.md`

- [ ] **Step 1: Write failing entry-point contract tests**

```csharp
[Fact]
public async Task RunAsync_WhenArgumentsAreInvalid_ReturnsOne() {
    int exitCode = await Program.RunAsync(["--output", "C:\\output"]);

    Assert.Equal(1, exitCode);
}
```

- [ ] **Step 2: Run the contract test and verify it fails**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\tools\build-waiter.tests\helengine.buildwaiter.tests.csproj --filter FullyQualifiedName~ProgramTests
```

Expected: compilation fails because `Program.RunAsync` does not exist.

- [ ] **Step 3: Implement `Program.RunAsync` and document the PS2 command**

```csharp
public static class Program {
    static async Task<int> Main(string[] args) {
        return await RunAsync(args);
    }

    public static async Task<int> RunAsync(string[] args) {
        try {
            BuildWaiterOptions options = BuildWaiterOptionsParser.Parse(args);
            BuildWaiterResult result = await new BuildWaiter(new BuildArtifactVerifier()).WaitAsync(options, CancellationToken.None);
            if (!result.Succeeded) {
                Console.Error.WriteLine("[build-waiter] " + result.Message);
                return result.ExitCode == 0 ? 1 : result.ExitCode;
            }

            Console.WriteLine("[build-waiter] complete: " + result.Message);
            return 0;
        } catch (Exception exception) {
            Console.Error.WriteLine("[build-waiter] " + exception.Message);
            return 1;
        }
    }
}
```

Add this PS2 example to `README.md`:

```powershell
dotnet run --project tools/build-waiter/helengine.buildwaiter.csproj -- --output C:\dev\helprojs\output\ps2 --require game.iso --require disc/SYSTEM.CNF --require disc/HELENGIN.ELF -- dotnet C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.dll --build ps2 --project C:\dev\helprojs\demodisc\project.heproj --output C:\dev\helprojs\output\ps2
```

- [ ] **Step 4: Run the complete test project and build the console tool**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\tools\build-waiter.tests\helengine.buildwaiter.tests.csproj
rtk dotnet build C:\dev\helworks\helengine\tools\build-waiter\helengine.buildwaiter.csproj --no-restore
```

Expected: all build-waiter tests pass and the console project builds with zero errors.

- [ ] **Step 5: Commit the usable console tool**

```powershell
rtk git -C C:\dev\helworks\helengine add -- tools/build-waiter tools/build-waiter.tests README.md
rtk git -C C:\dev\helworks\helengine commit -m "feat: add verified build waiter console"
```
