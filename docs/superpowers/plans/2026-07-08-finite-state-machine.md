# Finite State Machine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a reusable enum-backed finite state machine utility to `helengine.core` with guarded transitions, enter and exit hooks, engine tests, and a focused `cs2.cpp` proof fixture.

**Architecture:** The FSM lives in `helengine.core` as a code-only runtime utility under `runtime/statemachine/`. The first pass keeps the API generic over `TState` with a `struct` constraint, validates enum usage at runtime, and stores registered states plus optional transition guards in dictionaries keyed by `TState` and a small transition-key value type. Verification is split between `helengine.editor.tests` for runtime behavior and `csharpcodegen/cs2.cpp.tests` for enum-backed generic conversion coverage.

**Tech Stack:** C#, .NET 9, xUnit, `helengine.core`, `helengine.editor.tests`, `cs2.cpp.tests`

---

## File Structure

### Runtime files

- Create: `C:\dev\helworks\helengine\engine\helengine.core\runtime\statemachine\FiniteStateDefinition.cs`
  Owns optional per-state `OnEnter` and `OnExit` callbacks.
- Create: `C:\dev\helworks\helengine\engine\helengine.core\runtime\statemachine\FiniteStateTransition.cs`
  Owns one optional guarded edge between two states.
- Create: `C:\dev\helworks\helengine\engine\helengine.core\runtime\statemachine\FiniteStateTransitionKey.cs`
  Provides one dictionary-safe value key for `(from, to)` transition lookup.
- Create: `C:\dev\helworks\helengine\engine\helengine.core\runtime\statemachine\FiniteStateMachine.cs`
  Owns registration, initialization, validation, and transition execution.

### Engine test files

- Create: `C:\dev\helworks\helengine\engine\helengine.editor.tests\runtime\FiniteStateMachineTests.cs`
  Covers runtime behavior, exception semantics, hook ordering, and enum validation.

### Converter proof files

- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPFiniteStateMachineAuditTests.cs`
  Covers enum-backed generic conversion using the existing `RunConversion`-style output assertions used by other focused audit tests.

---

### Task 1: Add the failing engine behavior tests

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.editor.tests\runtime\FiniteStateMachineTests.cs`
- Test: `C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj`

- [ ] **Step 1: Write the failing test file**

```csharp
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the reusable finite state machine runtime behavior.
    /// </summary>
    public sealed class FiniteStateMachineTests {
        /// <summary>
        /// Declares one representative enum-backed state set for runtime FSM tests.
        /// </summary>
        enum TestState {
            Waiting,
            Playing,
            Failed
        }

        /// <summary>
        /// Ensures initialization enters the starting state and exposes it through the current-state surface.
        /// </summary>
        [Fact]
        public void Initialize_WhenStartingStateIsRegistered_SetsCurrentStateAndRunsEnterHook() {
            List<string> events = new List<string>();
            FiniteStateMachine<TestState> machine = new FiniteStateMachine<TestState>();
            machine.RegisterState(TestState.Waiting, new FiniteStateDefinition<TestState> {
                OnEnter = state => events.Add($"enter:{state}")
            });

            machine.Initialize(TestState.Waiting);

            Assert.True(machine.HasCurrentState);
            Assert.Equal(TestState.Waiting, machine.CurrentState);
            Assert.Equal(new[] { "enter:Waiting" }, events);
        }

        /// <summary>
        /// Ensures one successful transition runs exit before enter and updates previous-state tracking.
        /// </summary>
        [Fact]
        public void TryChangeState_WhenGuardAllows_RunsExitThenEnterAndUpdatesPreviousState() {
            List<string> events = new List<string>();
            FiniteStateMachine<TestState> machine = new FiniteStateMachine<TestState>();
            machine.RegisterState(TestState.Waiting, new FiniteStateDefinition<TestState> {
                OnExit = state => events.Add($"exit:{state}")
            });
            machine.RegisterState(TestState.Playing, new FiniteStateDefinition<TestState> {
                OnEnter = state => events.Add($"enter:{state}")
            });
            machine.RegisterTransition(TestState.Waiting, TestState.Playing, () => true);
            machine.Initialize(TestState.Waiting);
            events.Clear();

            bool changed = machine.TryChangeState(TestState.Playing);

            Assert.True(changed);
            Assert.Equal(TestState.Waiting, machine.PreviousState);
            Assert.Equal(TestState.Playing, machine.CurrentState);
            Assert.Equal(new[] { "exit:Waiting", "enter:Playing" }, events);
        }

        /// <summary>
        /// Ensures a rejected guard leaves the active state untouched and skips lifecycle callbacks.
        /// </summary>
        [Fact]
        public void TryChangeState_WhenGuardRejects_LeavesCurrentStateAndSkipsHooks() {
            List<string> events = new List<string>();
            FiniteStateMachine<TestState> machine = new FiniteStateMachine<TestState>();
            machine.RegisterState(TestState.Waiting, new FiniteStateDefinition<TestState> {
                OnExit = state => events.Add($"exit:{state}")
            });
            machine.RegisterState(TestState.Failed, new FiniteStateDefinition<TestState> {
                OnEnter = state => events.Add($"enter:{state}")
            });
            machine.RegisterTransition(TestState.Waiting, TestState.Failed, () => false);
            machine.Initialize(TestState.Waiting);
            events.Clear();

            bool changed = machine.TryChangeState(TestState.Failed);

            Assert.False(changed);
            Assert.Equal(TestState.Waiting, machine.CurrentState);
            Assert.Empty(events);
        }

        /// <summary>
        /// Ensures registering one transition against an unregistered state fails fast.
        /// </summary>
        [Fact]
        public void RegisterTransition_WhenEndpointStateIsUnregistered_ThrowsInvalidOperationException() {
            FiniteStateMachine<TestState> machine = new FiniteStateMachine<TestState>();
            machine.RegisterState(TestState.Waiting, new FiniteStateDefinition<TestState>());

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => machine.RegisterTransition(TestState.Waiting, TestState.Playing, () => true));

            Assert.Equal("Finite state machine transitions require both endpoint states to be registered first.", exception.Message);
        }

        /// <summary>
        /// Ensures non-enum generic state types are rejected during setup in the managed runtime path.
        /// </summary>
        [Fact]
        public void RegisterState_WhenStateTypeIsNotEnum_ThrowsInvalidOperationException() {
            FiniteStateMachine<int> machine = new FiniteStateMachine<int>();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => machine.RegisterState(1, new FiniteStateDefinition<int>()));

            Assert.Equal("Finite state machine state types must be enum value types.", exception.Message);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FiniteStateMachineTests -v minimal`

Expected: FAIL with compile errors that `FiniteStateMachine<>`, `FiniteStateDefinition<>`, and related runtime types do not exist yet.

- [ ] **Step 3: Commit the failing test**

```bash
rtk git -C C:\dev\helworks\helengine add -- engine/helengine.editor.tests/runtime/FiniteStateMachineTests.cs
rtk git -C C:\dev\helworks\helengine commit -m "test: add finite state machine behavior coverage"
```

---

### Task 2: Implement the reusable runtime FSM types

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.core\runtime\statemachine\FiniteStateDefinition.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.core\runtime\statemachine\FiniteStateTransition.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.core\runtime\statemachine\FiniteStateTransitionKey.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.core\runtime\statemachine\FiniteStateMachine.cs`
- Test: `C:\dev\helworks\helengine\engine\helengine.editor.tests\runtime\FiniteStateMachineTests.cs`

- [ ] **Step 1: Create the state definition type**

```csharp
namespace helengine {
    /// <summary>
    /// Stores the optional lifecycle callbacks associated with one registered FSM state.
    /// </summary>
    public sealed class FiniteStateDefinition<TState> where TState : struct {
        /// <summary>
        /// Gets or sets the callback invoked immediately after the machine enters the owning state.
        /// </summary>
        public Action<TState> OnEnter { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked immediately before the machine exits the owning state.
        /// </summary>
        public Action<TState> OnExit { get; set; }
    }
}
```

- [ ] **Step 2: Create the transition type**

```csharp
namespace helengine {
    /// <summary>
    /// Stores one optional guarded transition between two registered FSM states.
    /// </summary>
    public sealed class FiniteStateTransition<TState> where TState : struct {
        /// <summary>
        /// Gets or sets the state being exited by this transition.
        /// </summary>
        public TState FromState { get; set; }

        /// <summary>
        /// Gets or sets the state being entered by this transition.
        /// </summary>
        public TState ToState { get; set; }

        /// <summary>
        /// Gets or sets the optional transition guard that decides whether the transition may proceed.
        /// </summary>
        public Func<bool> CanTransition { get; set; }
    }
}
```

- [ ] **Step 3: Create the transition-key value type**

```csharp
namespace helengine {
    /// <summary>
    /// Provides one dictionary-safe value key for a transition edge between two states.
    /// </summary>
    public readonly struct FiniteStateTransitionKey<TState> : IEquatable<FiniteStateTransitionKey<TState>> where TState : struct {
        /// <summary>
        /// Gets the source state for the keyed transition.
        /// </summary>
        public TState FromState { get; }

        /// <summary>
        /// Gets the target state for the keyed transition.
        /// </summary>
        public TState ToState { get; }

        /// <summary>
        /// Initializes one transition key for the supplied source and target states.
        /// </summary>
        /// <param name="fromState">Source state.</param>
        /// <param name="toState">Target state.</param>
        public FiniteStateTransitionKey(TState fromState, TState toState) {
            FromState = fromState;
            ToState = toState;
        }

        /// <summary>
        /// Determines whether this key matches another transition key.
        /// </summary>
        /// <param name="other">Other key to compare.</param>
        /// <returns><c>true</c> when both endpoints match; otherwise <c>false</c>.</returns>
        public bool Equals(FiniteStateTransitionKey<TState> other) {
            return EqualityComparer<TState>.Default.Equals(FromState, other.FromState)
                && EqualityComparer<TState>.Default.Equals(ToState, other.ToState);
        }

        /// <summary>
        /// Determines whether this key matches another object instance.
        /// </summary>
        /// <param name="obj">Object to compare.</param>
        /// <returns><c>true</c> when the object is one equal key; otherwise <c>false</c>.</returns>
        public override bool Equals(object obj) {
            return obj is FiniteStateTransitionKey<TState> other && Equals(other);
        }

        /// <summary>
        /// Builds the stable hash code used by transition dictionaries.
        /// </summary>
        /// <returns>Combined endpoint hash code.</returns>
        public override int GetHashCode() {
            return HashCode.Combine(FromState, ToState);
        }
    }
}
```

- [ ] **Step 4: Create the FSM implementation**

```csharp
namespace helengine {
    /// <summary>
    /// Provides one reusable finite state machine for enum-backed runtime systems.
    /// </summary>
    public sealed class FiniteStateMachine<TState> where TState : struct {
        /// <summary>
        /// Stores the registered state definitions keyed by state value.
        /// </summary>
        readonly Dictionary<TState, FiniteStateDefinition<TState>> StateDefinitionsByState;

        /// <summary>
        /// Stores the registered guarded transitions keyed by source and target state.
        /// </summary>
        readonly Dictionary<FiniteStateTransitionKey<TState>, FiniteStateTransition<TState>> TransitionsByKey;

        /// <summary>
        /// Stores the currently active state after initialization succeeds.
        /// </summary>
        TState CurrentStateValue;

        /// <summary>
        /// Stores the previously active state after one successful transition occurs.
        /// </summary>
        TState PreviousStateValue;

        /// <summary>
        /// Initializes one empty finite state machine.
        /// </summary>
        public FiniteStateMachine() {
            StateDefinitionsByState = new Dictionary<TState, FiniteStateDefinition<TState>>();
            TransitionsByKey = new Dictionary<FiniteStateTransitionKey<TState>, FiniteStateTransition<TState>>();
        }

        /// <summary>
        /// Gets the currently active state.
        /// </summary>
        public TState CurrentState {
            get {
                EnsureInitialized();
                return CurrentStateValue;
            }
        }

        /// <summary>
        /// Gets the previously active state from the last successful transition.
        /// </summary>
        public TState PreviousState {
            get {
                EnsureInitialized();
                return PreviousStateValue;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the machine has entered one starting state yet.
        /// </summary>
        public bool HasCurrentState { get; private set; }

        /// <summary>
        /// Registers one state definition before initialization.
        /// </summary>
        /// <param name="state">State to register.</param>
        /// <param name="definition">Definition associated with the state.</param>
        public void RegisterState(TState state, FiniteStateDefinition<TState> definition) {
            ValidateStateType();
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            } else if (StateDefinitionsByState.ContainsKey(state)) {
                throw new InvalidOperationException("Finite state machine states may only be registered once.");
            }

            StateDefinitionsByState.Add(state, definition);
        }

        /// <summary>
        /// Registers one optional guarded transition between two states.
        /// </summary>
        /// <param name="fromState">Source state.</param>
        /// <param name="toState">Target state.</param>
        /// <param name="canTransition">Optional guard.</param>
        public void RegisterTransition(TState fromState, TState toState, Func<bool> canTransition = null) {
            EnsureStateRegistered(fromState);
            EnsureStateRegistered(toState);
            FiniteStateTransitionKey<TState> key = new FiniteStateTransitionKey<TState>(fromState, toState);
            TransitionsByKey[key] = new FiniteStateTransition<TState> {
                FromState = fromState,
                ToState = toState,
                CanTransition = canTransition
            };
        }

        /// <summary>
        /// Initializes the machine at one registered starting state.
        /// </summary>
        /// <param name="initialState">Registered starting state.</param>
        public void Initialize(TState initialState) {
            EnsureStateRegistered(initialState);
            CurrentStateValue = initialState;
            PreviousStateValue = initialState;
            HasCurrentState = true;
            ResolveRequiredDefinition(initialState).OnEnter?.Invoke(initialState);
        }

        /// <summary>
        /// Determines whether the machine may move into the supplied target state.
        /// </summary>
        /// <param name="nextState">Requested target state.</param>
        /// <returns><c>true</c> when the state change may proceed; otherwise <c>false</c>.</returns>
        public bool CanChangeState(TState nextState) {
            EnsureInitialized();
            EnsureStateRegistered(nextState);
            if (EqualityComparer<TState>.Default.Equals(CurrentStateValue, nextState)) {
                return false;
            }

            if (!TransitionsByKey.TryGetValue(new FiniteStateTransitionKey<TState>(CurrentStateValue, nextState), out FiniteStateTransition<TState> transition)) {
                return true;
            }

            return transition.CanTransition == null || transition.CanTransition();
        }

        /// <summary>
        /// Attempts one transition into the supplied target state.
        /// </summary>
        /// <param name="nextState">Requested target state.</param>
        /// <returns><c>true</c> when the transition ran; otherwise <c>false</c>.</returns>
        public bool TryChangeState(TState nextState) {
            if (!CanChangeState(nextState)) {
                return false;
            }

            TState previousState = CurrentStateValue;
            ResolveRequiredDefinition(previousState).OnExit?.Invoke(previousState);
            PreviousStateValue = previousState;
            CurrentStateValue = nextState;
            ResolveRequiredDefinition(nextState).OnEnter?.Invoke(nextState);
            return true;
        }

        /// <summary>
        /// Validates that the generic state type is one enum-backed value type.
        /// </summary>
        void ValidateStateType() {
            if (!typeof(TState).IsEnum) {
                throw new InvalidOperationException("Finite state machine state types must be enum value types.");
            }
        }

        /// <summary>
        /// Ensures one state registration exists for the supplied value.
        /// </summary>
        /// <param name="state">State to validate.</param>
        void EnsureStateRegistered(TState state) {
            ValidateStateType();
            if (!StateDefinitionsByState.ContainsKey(state)) {
                throw new InvalidOperationException("Finite state machine transitions require both endpoint states to be registered first.");
            }
        }

        /// <summary>
        /// Ensures the machine has one active state before state queries or changes proceed.
        /// </summary>
        void EnsureInitialized() {
            if (!HasCurrentState) {
                throw new InvalidOperationException("Finite state machine must be initialized before it can be queried or advanced.");
            }
        }

        /// <summary>
        /// Resolves the required state definition for one registered state.
        /// </summary>
        /// <param name="state">State whose definition should be returned.</param>
        /// <returns>Registered definition.</returns>
        FiniteStateDefinition<TState> ResolveRequiredDefinition(TState state) {
            return StateDefinitionsByState[state];
        }
    }
}
```

- [ ] **Step 5: Run the engine tests to verify they pass**

Run: `rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FiniteStateMachineTests -v minimal`

Expected: PASS with all `FiniteStateMachineTests` green.

- [ ] **Step 6: Commit the runtime implementation**

```bash
rtk git -C C:\dev\helworks\helengine add -- engine/helengine.core/runtime/statemachine engine/helengine.editor.tests/runtime/FiniteStateMachineTests.cs
rtk git -C C:\dev\helworks\helengine commit -m "feat: add reusable finite state machine runtime"
```

---

### Task 3: Add the focused `cs2.cpp` enum-backed generic audit

**Files:**
- Create: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\CPPFiniteStateMachineAuditTests.cs`
- Test: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj`

- [ ] **Step 1: Write the focused converter audit**

```csharp
using System.Text.Json;
using cs2.cpp;

namespace cs2.cpp.tests {
    /// <summary>
    /// Verifies enum-backed generic finite state machine usage converts cleanly through the C++ backend.
    /// </summary>
    public sealed class CPPFiniteStateMachineAuditTests {
        /// <summary>
        /// Ensures one representative FSM source shape with enum state usage emits stable generic and enum output without pseudo-includes for the state type.
        /// </summary>
        [Fact]
        public void WriteOutput_WithEnumBackedFiniteStateMachine_EmitsGenericTypeWithoutEnumPseudoInclude() {
            string source = """
                using System;
                using System.Collections.Generic;

                public sealed class FiniteStateDefinition<TState> where TState : struct {
                    public Action<TState> OnEnter { get; set; }
                    public Action<TState> OnExit { get; set; }
                }

                public readonly struct FiniteStateTransitionKey<TState> where TState : struct {
                    public TState FromState { get; }
                    public TState ToState { get; }

                    public FiniteStateTransitionKey(TState fromState, TState toState) {
                        FromState = fromState;
                        ToState = toState;
                    }
                }

                public sealed class FiniteStateMachine<TState> where TState : struct {
                    readonly Dictionary<TState, FiniteStateDefinition<TState>> states = new Dictionary<TState, FiniteStateDefinition<TState>>();
                    readonly Dictionary<FiniteStateTransitionKey<TState>, Func<bool>> guards = new Dictionary<FiniteStateTransitionKey<TState>, Func<bool>>();

                    public void RegisterState(TState state, FiniteStateDefinition<TState> definition) {
                        states.Add(state, definition);
                    }

                    public void RegisterTransition(TState fromState, TState toState, Func<bool> canTransition) {
                        guards[new FiniteStateTransitionKey<TState>(fromState, toState)] = canTransition;
                    }
                }

                public enum TestState {
                    Waiting,
                    Playing
                }

                public sealed class TestConsumer {
                    public FiniteStateMachine<TestState> Build() {
                        FiniteStateMachine<TestState> machine = new FiniteStateMachine<TestState>();
                        machine.RegisterState(TestState.Waiting, new FiniteStateDefinition<TestState>());
                        machine.RegisterState(TestState.Playing, new FiniteStateDefinition<TestState>());
                        machine.RegisterTransition(TestState.Waiting, TestState.Playing, () => true);
                        return machine;
                    }
                }
                """;

            ConversionOutput output = RunConversion(source);
            string generatedText = output.GeneratedText;

            Assert.Contains("template <typename TState>", generatedText, StringComparison.Ordinal);
            Assert.Contains("class FiniteStateMachine_1", generatedText, StringComparison.Ordinal);
            Assert.Contains("enum class TestState", generatedText, StringComparison.Ordinal);
            Assert.Contains("Dictionary<TestState", generatedText, StringComparison.Ordinal);
            Assert.DoesNotContain("#include \"TestState.hpp\"", generatedText, StringComparison.Ordinal);
        }

        /// <summary>
        /// Runs the converter against one temporary project fixture and returns the generated output bundle.
        /// </summary>
        /// <param name="source">Single C# source file content to convert.</param>
        /// <returns>Generated output bundle for assertions.</returns>
        static ConversionOutput RunConversion(string source) {
            string rootPath = Path.Combine(Path.GetTempPath(), "cs2cpp-fsm-tests", Guid.NewGuid().ToString("N"));
            string projectPath = Path.Combine(rootPath, "Fixture.csproj");
            string sourcePath = Path.Combine(rootPath, "Fixture.cs");
            string outputPath = Path.Combine(rootPath, "out");

            Directory.CreateDirectory(rootPath);
            File.WriteAllText(projectPath, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net9.0</TargetFramework>
                    <LangVersion>preview</LangVersion>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>disable</Nullable>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(sourcePath, source);

            CPPConversionOptions options = CPPConversionOptions.CreateDefault();
            options.LoadNativeRuntimeMetadata = false;
            options.WriteConversionReport = true;

            CPPCodeConverter converter = new CPPCodeConverter(new CPPConversionRules(), options);
            converter.AddCsproj(projectPath);
            converter.WriteOutput(outputPath);

            return new ConversionOutput(
                outputPath,
                string.Join(
                    "\n",
                    Directory.GetFiles(outputPath, "*.*", SearchOption.AllDirectories)
                        .Where(path => path.EndsWith(".hpp", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(path => path, StringComparer.Ordinal)
                        .Select(File.ReadAllText)),
                JsonDocument.Parse(File.ReadAllText(Path.Combine(outputPath, "cpp-conversion-report.json"))));
        }

        /// <summary>
        /// Stores one generated output bundle used by the converter audit.
        /// </summary>
        /// <param name="OutputPath">Generated output directory.</param>
        /// <param name="GeneratedText">Concatenated generated C++ text.</param>
        /// <param name="Report">Parsed conversion report.</param>
        record ConversionOutput(string OutputPath, string GeneratedText, JsonDocument Report);
    }
}
```

- [ ] **Step 2: Run the focused converter audit**

Run: `rtk dotnet test C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj --filter CPPFiniteStateMachineAuditTests -v minimal`

Expected: PASS. If it fails, treat that failure as a real converter-compatibility issue blocking the engine feature and fix the backend before using the FSM in generated-code paths.

- [ ] **Step 3: Commit the converter proof**

```bash
rtk git -C C:\dev\helworks\csharpcodegen add -- cs2.cpp.tests/CPPFiniteStateMachineAuditTests.cs
rtk git -C C:\dev\helworks\csharpcodegen commit -m "test: cover enum-backed finite state machine conversion"
```

---

### Task 4: Run the focused final verification

**Files:**
- Test: `C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj`
- Test: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj`

- [ ] **Step 1: Run the engine FSM tests one final time**

Run: `rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FiniteStateMachineTests -v minimal`

Expected: PASS with all FSM behavior tests green and no new warnings relevant to the added files.

- [ ] **Step 2: Run the converter FSM audit one final time**

Run: `rtk dotnet test C:\dev\helworks\csharpcodegen\cs2.cpp.tests\cs2.cpp.tests.csproj --filter CPPFiniteStateMachineAuditTests -v minimal`

Expected: PASS with the enum-backed generic converter audit green.

- [ ] **Step 3: Capture the final repo state**

```bash
rtk git -C C:\dev\helworks\helengine status --short
rtk git -C C:\dev\helworks\csharpcodegen status --short
```

Expected:

- `helengine` is clean except for any intentional uncommitted follow-up work outside this plan
- `csharpcodegen` is clean except for any intentional uncommitted follow-up work outside this plan

- [ ] **Step 4: Commit any final polish only if verification forced one last code change**

```bash
rtk git -C C:\dev\helworks\helengine add -- engine/helengine.core/runtime/statemachine engine/helengine.editor.tests/runtime/FiniteStateMachineTests.cs
rtk git -C C:\dev\helworks\helengine commit -m "chore: polish finite state machine verification fixes"
```

---

## Spec Coverage Check

- reusable `helengine.core` FSM utility: covered by Task 2
- enum-backed caller usage: covered by Task 1 tests and Task 3 converter audit
- `OnEnter` and `OnExit`: covered by Task 1 and Task 2
- guarded transitions: covered by Task 1 and Task 2
- converter-safe generic shape: covered by Task 3
- no component or editor graph layer: preserved by file placement and runtime-only API in Task 2

## Placeholder Check

- no `TODO`, `TBD`, or deferred follow-up placeholders remain
- every task lists exact files and commands
- every code-writing step includes concrete code blocks

## Type Consistency Check

- `FiniteStateMachine<TState>` is the only machine type name used throughout
- `FiniteStateDefinition<TState>`, `FiniteStateTransition<TState>`, and `FiniteStateTransitionKey<TState>` are consistent across runtime, tests, and converter audit
- engine tests and converter tests both use enum-backed state types and the same registration vocabulary: `RegisterState`, `RegisterTransition`, `Initialize`, `TryChangeState`
