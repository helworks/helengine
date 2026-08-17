# Build Platform Waiter Output Handshake Design

## Purpose

Close the remaining same-output completion race in `build-waiter`. A later build targeting the same final output must wait until the earlier waiter has verified that invocation's terminal state and required artifacts. Waiting only until the earlier wrapper releases its output mutex is insufficient because the later build can replace shared artifacts before the earlier waiter inspects them.

This amendment also makes the invocation proof's embedded `buildId` comparison exactly match the canonical lowercase GUID contract.

## Completion Contract

A waiter-controlled build is complete only after all of the following occur:

1. the wrapper's editor child finishes successfully;
2. the wrapper writes shared terminal state and the invocation-specific terminal proof;
3. the waiter verifies the exact invocation proof;
4. the waiter verifies every required artifact while the wrapper still owns the output mutex;
5. the waiter acknowledges that verification was attempted;
6. the wrapper releases its locks and exits successfully.

A later build targeting the same canonical output remains blocked until step 6. After an exact successful proof is established, the acknowledgment releases the wrapper regardless of whether artifact verification passed, so an artifact failure cannot deadlock the output. The waiter, not the wrapper, remains responsible for returning the detailed artifact verification failure.

Direct wrapper invocations without `build-waiter` remain supported. They do not enter the acknowledgment protocol and release their locks after writing terminal state as they do today.

## Protocol Identity and Files

`build-waiter` continues to generate one canonical lowercase GUID in `D` format and passes it through `HELENGINE_BUILD_INVOCATION_ID`.

Waiter-controlled invocations additionally set:

```text
HELENGINE_BUILD_WAITER_PROTOCOL=ack-v1
```

The protocol value is an exact internal contract. A present value other than `ack-v1`, or protocol mode without a caller-supplied canonical invocation ID, is rejected before locks, directory creation, state writes, editor publication, or child-process launch.

The wrapper and waiter derive both files from the canonical output root and invocation ID:

```text
.helengine-build-state.<canonical-guid>.json
.helengine-build-state.<canonical-guid>.ack
```

No environment variable or command-line option supplies either path. Both paths must be strict descendants of the canonical output root. The acknowledgment file contains only the exact canonical lowercase invocation ID with no additional fields or trailing newline. Proof `buildId` and acknowledgment content comparisons use ordinal case-sensitive equality.

A pre-existing acknowledgment file for the selected invocation is a pre-mutation configuration failure. Invocation proof files remain durable. The wrapper removes only the exact acknowledgment file after validating its contents.

## Wrapper Flow

The wrapper retains the established lock order and one shared `-LockTimeout` budget:

1. project global mutex;
2. canonical-output global mutex;
3. cache-local project file lock.

Protocol preflight occurs before those locks and before filesystem mutation. Normal build work proceeds unchanged.

On successful editor completion, the wrapper writes the shared terminal state and invocation-specific terminal proof while still holding every lock. In `ack-v1` mode it then waits for the exact acknowledgment while continuing to hold the output mutex. The acknowledgment wait begins only after the proof has been written and has a fixed 30-second timeout independent of the build lock timeout.

The wrapper polls until the acknowledgment contents exactly equal the invocation ID. A missing, partial, wrong-ID, or wrong-case acknowledgment does not release the locks. After an exact match, the wrapper removes that exact acknowledgment, releases locks in the established reverse order, restores the environment, and exits with the editor child's successful status.

Failed editor builds do not wait for acknowledgment. They write failed terminal state, preserve the native child exit code, and release locks normally.

If a successful wrapper does not receive an exact acknowledgment within 30 seconds, the wrapper records protocol failure in both shared state and the invocation proof, using status `failed` and wrapper exit code `10`. It then releases every lock and exits nonzero. Failure to rewrite either state file is reported without preventing lock release.

## Waiter Flow

The waiter starts terminal verification concurrently with the child wrapper rather than waiting for process exit first. Standard output and error continue to drain asynchronously for the entire child lifetime.

The verification coordinator watches for either the exact invocation proof or child-process exit:

- If the child exits nonzero before a usable proof appears, the waiter preserves the child exit code.
- If the child exits zero without the exact proof, the waiter reports missing invocation proof.
- When the proof appears while the child remains active, the waiter performs existing state validation followed by existing artifact validation.
- State validation requires the proof filename and embedded `buildId` to contain the same canonical lowercase GUID using ordinal equality.
- Artifact validation retains the current output-containment, non-empty-file, and freshness checks. These checks now occur while the wrapper still owns the output mutex.

The waiter writes the acknowledgment only after the invocation proof validates as the exact successful state for that invocation and artifact verification has been attempted. It writes the acknowledgment even when artifact validation fails. The wrapper can then release the output lock, allowing the waiter to await process exit and return the stored detailed artifact result. The acknowledgment is a release signal, not a claim that artifact verification succeeded.

A missing, malformed, stale, failed, foreign-ID, or wrong-case proof is not acknowledged. A failed wrapper does not wait and exits with its original child code. A nominally successful wrapper whose proof cannot be validated remains bounded by the 30-second acknowledgment timeout, records protocol failure, and releases every lock.

If acknowledgment creation fails, the waiter reports that protocol error and continues draining and awaiting the child. The wrapper's bounded wait guarantees eventual lock release and nonzero exit. Cancellation must not bypass process-output drainage or leave the wrapper holding the output beyond the same 30-second protocol timeout.

## Components and Responsibilities

### PowerShell Handshake Module

Create `scripts/build-platform/BuildPlatformWaiterHandshake.psm1` to own:

- exact protocol parsing;
- canonical proof and acknowledgment path derivation;
- strict output containment checks;
- pre-existing acknowledgment rejection;
- bounded exact-content acknowledgment waiting;
- exact acknowledgment cleanup.

`scripts/build-platform.ps1` remains responsible for lifecycle orchestration, terminal state updates, and lock release. It imports the module and calls it before mutation and after successful proof publication.

### C# Invocation Paths

Create `tools/build-waiter/BuildInvocationProofPaths.cs` to own canonical invocation-ID validation and deterministic proof/acknowledgment path derivation. `BuildStateVerifier` and the handshake coordinator use this helper rather than constructing filenames independently.

### C# Verification Coordinator

Create `tools/build-waiter/BuildVerificationHandshake.cs` and a focused result type. The coordinator owns proof-or-exit waiting, exact successful-state verification, artifact verification, and best-effort acknowledgment after artifact verification is attempted.

`BuildWaiter` remains responsible for process launch, environment propagation, output forwarding, process exit, and final result precedence. `BuildArtifactVerifier` retains its current filesystem validation responsibilities.

## Error Precedence

Final waiter results follow this order:

1. child process start failure;
2. nonzero child exit code;
3. acknowledgment protocol failure;
4. invocation-proof failure;
5. artifact failure;
6. verified success.

The coordinator stores verification results before acknowledgment. A later successful child exit cannot replace a state or artifact failure. A nonzero child exit remains authoritative because it proves the wrapper did not complete its protocol successfully.

## Test Strategy

Every production change is implemented test-first and observed failing for the intended reason.

### C# Tests

- Reject a proof whose embedded GUID differs only by letter case from the canonical expected ID.
- Reject blank, malformed, uppercase, padded, and non-`D` expected invocation IDs before path construction.
- Verify successful proof and artifacts while the child is still active, then write the exact acknowledgment.
- Verify missing, malformed, stale, failed, foreign-ID, or wrong-case proof failures do not acknowledge.
- Verify missing, empty, stale, rooted, and escaping required artifacts still acknowledge and return the original detailed failure.
- Reproduce the artifact race deterministically: the child writes A's proof and artifact, waits for acknowledgment, then replaces the artifact before exit. A succeeds because verification occurred before acknowledgment and replacement.
- Verify child nonzero exit before proof preserves the child exit code and does not create an acknowledgment.
- Verify child zero exit without proof reports missing invocation proof.
- Verify acknowledgment write failure returns protocol failure and does not replace a more authoritative nonzero child exit.

### PowerShell Tests

- Reject absent invocation ID, malformed protocol value, and pre-existing acknowledgment before any output/cache mutation.
- Accept direct wrapper invocation without protocol mode and prove it does not wait.
- Hold A's output mutex after successful proof publication and prove B cannot launch its controlled editor against the same output before acknowledgment.
- Prove wrong-ID, wrong-case, partial, and newline-suffixed acknowledgment contents do not release A.
- Prove exact acknowledgment releases A, is removed, and then allows B to proceed.
- Prove missing acknowledgment times out, rewrites shared state and proof to failed exit `10`, releases B, and leaves no broad cleanup side effects.
- Prove failed editor builds bypass acknowledgment waiting and preserve their original exit code.

### Regression and Integration Verification

After focused tests pass, run:

- `tools/build-waiter.tests`;
- build-platform workspace, profile, profile-behavior, streaming, locking, maintenance, real-editor ownership, and native ownership PowerShell suites;
- real-editor smoke;
- native stable-cache smoke;
- focused editor build-isolation, build-graph, and CLI build-runner tests;
- scope scans for repository cloning, `robocopy`, invocation-scoped cache directories, and the legacy `C:\dev\helworks\b` path;
- `git diff --check` and a clean-worktree check;
- an independent whole-branch review focused on output-lock lifetime, timeout release, exact identity, and artifact verification ordering.

The three existing point-shadow native tests may retain their documented CMake/MSVC `C1041` failure at 254-character `%TEMP%` object paths. The real stable-cache native smoke must pass.

## Documentation

Update `README.md` to state that waiter-controlled same-output builds remain serialized until terminal state and required artifacts are verified, that direct wrapper calls do not use the acknowledgment phase, and that the acknowledgment timeout is 30 seconds.

## Non-Goals

- Copying, snapshotting, hashing, or retaining invocation-specific artifact payloads.
- Reintroducing repository cloning or invocation-scoped cache trees.
- Changing compact `v2` cache identity or editor/platform cache agreement.
- Changing artifact freshness, non-empty, or containment rules.
- Migrating or pruning existing invocation proof files.
- Extending the acknowledgment protocol to arbitrary wrapper callers.
- Broad changes to editor native-test temporary-directory policy.
