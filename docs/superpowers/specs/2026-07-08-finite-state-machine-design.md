# Finite State Machine Design

## Overview

This design adds a reusable finite state machine utility to `helengine.core`.

The goal is to give gameplay and engine-side runtime code one small, explicit state-transition primitive that can be reused across authored systems such as Tilt Trial, later controller-driven runtime features, and other update-driven behaviors.

The first slice is intentionally narrow:

- code-level reusable FSM utility only
- enum-backed caller state definitions
- explicit enter and exit lifecycle hooks
- explicit guarded transitions
- converter-safe shape for generated C++ output

This slice does not include scene serialization, editor graph authoring, animation-controller graphs, blend trees, or entity-owned visual tooling.

## Goals

- add one reusable runtime FSM utility to `helengine.core`
- let gameplay code define its own strongly typed state enums
- support `OnEnter`, `OnExit`, and guarded transition checks
- keep the API small and explicit
- keep the implementation compatible with the current C# to C++ conversion pipeline
- prove enum-backed generic usage through a dedicated `cs2.cpp` regression test

## Non-Goals

- no scene-serializable `StateMachineComponent`
- no editor visualization or graph tooling
- no reflection-driven runtime discovery
- no string-based state identifiers
- no implicit transition graph generation
- no hierarchical or parallel state machines
- no animation-controller layer in this slice

## Recommended Approach

Add a generic runtime type:

- `FiniteStateMachine<TState>`

Place it under:

- `engine/helengine.core/runtime/statemachine/`

The machine should be generic so callers can use their own enum type, but v1 should constrain `TState` to `struct` rather than `struct, Enum`.

This is a deliberate converter-safety choice:

- the engine and converter already support generic types and enum types
- the exact `where T : struct, Enum` constraint is not yet proven by existing `cs2.cpp` coverage
- v1 should avoid depending on unproven converter behavior

Callers still use enums such as:

- `TiltPlayGameState`
- `DemoDiscMenuState`

The FSM implementation remains reusable while the consuming code preserves strongly typed enum state names.

## API Shape

`FiniteStateMachine<TState>` should expose:

- `CurrentState`
- `PreviousState`
- `HasCurrentState`
- `Initialize(TState initialState)`
- `RegisterState(TState state, FiniteStateDefinition<TState> definition)`
- `RegisterTransition(TState from, TState to, Func<bool> canTransition = null)`
- `CanChangeState(TState nextState)`
- `TryChangeState(TState nextState)`

Supporting types:

- `FiniteStateDefinition<TState>`
- `FiniteStateTransition<TState>`

`FiniteStateDefinition<TState>` should hold:

- optional `Action<TState>` `OnEnter`
- optional `Action<TState>` `OnExit`

`FiniteStateTransition<TState>` should hold:

- `FromState`
- `ToState`
- optional `Func<bool>` `CanTransition`

The utility should not own update-loop behavior by itself. Update-driven components decide when to ask the FSM to transition.

## Initialization And Validation

The FSM should require explicit setup.

Expected usage order:

1. create machine
2. register states
3. optionally register guarded transitions
4. initialize with one starting state
5. call `TryChangeState` from consuming gameplay code

Validation rules:

- `Initialize` should fail if the initial state was not registered
- `RegisterState` should fail on duplicate state registration
- `RegisterTransition` should fail if either endpoint state is not registered
- `CurrentState` and `PreviousState` should not silently fabricate defaults before initialization
- all setup failures should throw explicit exceptions

To keep enum-backed usage intentional, the managed implementation should validate during setup that `TState` resolves to an enum type. This is a runtime validation choice in v1 rather than a generic constraint choice because converter safety is the higher priority.

## Transition Behavior

`TryChangeState` should be deterministic and side-effect safe.

When `TryChangeState(nextState)` is called:

1. validate that the machine has been initialized
2. validate that `nextState` is registered
3. if `nextState` already matches `CurrentState`, return `false`
4. resolve the registered transition from current to next when present
5. if a guard exists and returns `false`, return `false`
6. run current-state `OnExit`
7. update `PreviousState`
8. update `CurrentState`
9. run next-state `OnEnter`
10. return `true`

If no explicit transition registration exists between two registered states, the machine should allow the change by default in v1.

This keeps the first slice lightweight:

- states are always explicit
- guards are opt-in
- the machine does not force every edge to be predeclared

## Failure Semantics

The FSM should fail fast on bad setup and preserve state on rejected transitions.

Rules:

- failed guards must not run `OnExit` or `OnEnter`
- failed guards must leave `CurrentState` and `PreviousState` unchanged
- missing setup should throw, not degrade into best-effort behavior
- callback exceptions should propagate rather than being swallowed

This matches the engine rule set of not masking failures and not inventing default runtime state when valid input is required.

## Data Structures

The implementation should stay straightforward and C++-friendly.

Preferred internal shape:

- dictionary keyed by `TState` for registered state definitions
- dictionary keyed by `(from, to)` equivalent data for guarded transitions

The exact storage may be implemented as:

- `Dictionary<TState, FiniteStateDefinition<TState>>`
- one small transition key type plus `Dictionary<FiniteStateTransitionKey<TState>, FiniteStateTransition<TState>>`

Do not use reflection-driven callback lookup, attribute scanning, or string maps.

## C++ Conversion Compatibility

This feature must be designed with converter verification as part of the work, not as a later follow-up.

Requirements:

- the generic FSM type must remain convertible through `cs2.cpp`
- enum-backed caller usage must be covered by a dedicated converter regression test
- the design must avoid relying on unproven generic enum-constraint syntax in v1

The converter proof should instantiate the FSM with a concrete enum fixture and exercise:

- state registration
- initialization
- guarded transition rejection
- successful transition acceptance

If converter coverage later proves `where T : struct, Enum` safe, the API can be tightened in a later pass without changing the higher-level behavior.

## Placement

Add the new runtime types under:

- `engine/helengine.core/runtime/statemachine/FiniteStateMachine.cs`
- `engine/helengine.core/runtime/statemachine/FiniteStateDefinition.cs`
- `engine/helengine.core/runtime/statemachine/FiniteStateTransition.cs`
- `engine/helengine.core/runtime/statemachine/FiniteStateTransitionKey.cs`

This keeps the feature in reusable runtime infrastructure rather than hiding it inside components or unrelated utility folders.

## Testing

### Engine Tests

Add focused `helengine.editor.tests` coverage for:

- initializing with a registered starting state
- rejecting initialization when the starting state is unregistered
- rejecting duplicate state registration
- rejecting transitions that reference unregistered states
- successful enter and exit ordering
- guard rejection preserving current state
- `PreviousState` updating only on successful transitions
- self-transition requests returning `false`

### Converter Tests

Add one `cs2.cpp.tests` regression fixture that defines:

- one enum state type
- one consumer that instantiates `FiniteStateMachine<TestState>`
- one guard and one successful transition path

The assertion target is generated C++ output that still compiles for enum-backed generic usage.

## Example Consumer Direction

Tilt Trial should be able to define a local enum such as:

- `WaitingToStart`
- `Playing`
- `Failed`
- `Finished`

Gameplay code can then register state hooks and guards without the engine knowing anything about Tilt Trial specifically.

That keeps the engine utility generic while leaving authored behavior in game code where it belongs.

## Impact

This adds one reusable runtime primitive to `helengine.core` that:

- is small and explicit
- stays friendly to the existing C# to C++ conversion model
- avoids premature controller or graph complexity
- gives gameplay code a clean foundation for authored stateful behavior

The result is a narrow but durable FSM layer that can be reused immediately in Tilt Trial and later serve as the basis for higher-level controller systems.
