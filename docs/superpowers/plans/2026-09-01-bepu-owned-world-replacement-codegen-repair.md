# BEPU Owned-World Replacement Codegen Repair Plan

**Goal:** Release the registration state's owned world slot canonically before assigning its replacement.

**Observed failure:** The exact Release physics3d codegen gate cleared the disposal error, then reported `CPPOWN007` at the assignment in `ReplaceOwnedRuntimeWorld`: `RuntimeWorld` is replaced before its prior value is released.

**Root cause:** The helper disposes a borrowed local alias, `previousWorld`, but never clears the `NativeOwnedMember` slot before overwriting it. Managed behavior is correct, but native owned-member validation requires the release to target the slot.

## Repair

1. Add a RED source contract in `BepuRuntimeComponentRegistrationTests` requiring `NativeOwnership.DisposeAndRelease(ref state.RuntimeWorld)` before `state.RuntimeWorld = replacementWorld` and rejecting direct `previousWorld.Dispose()` cleanup.
2. In `ReplaceOwnedRuntimeWorld`, retain the alias only for identity/detachment checks, then release the owned slot canonically before assigning the replacement.
3. Run focused BEPU registration and targeted physics tests.
4. Rerun the exact direct Release physics3d codegen gate. Do not rerun the full DemoDisc cook until this direct gate exits zero with no codegen window or WerFault.
