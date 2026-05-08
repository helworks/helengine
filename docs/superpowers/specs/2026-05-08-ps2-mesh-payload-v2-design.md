# PS2 Mesh Payload V2 Design

**Goal:** Allow the PS2 build pipeline to consume the current mesh component payload format, rewrite logical asset references to physical disc paths, and emit a valid PS2 ISO for the city project.

**Problem:** The editor currently packages mesh component payloads using version `2` via the shared `MeshComponentScenePayloadSerializer`. The PS2 builder still manually parses only version `1` inside `Ps2CookedAssetPathRewriter`, so headless PS2 builds fail with `Unsupported mesh component payload version '2'`.

**Decision:** The PS2 builder will stop hand-parsing the legacy mesh payload format as its primary path. Instead, it will read mesh payloads using the same version-aware semantics as the engine serializer, rewrite the decoded model and material references, and write the payload back using the current format. Legacy version `1` input remains readable, but rewritten payload output is normalized to version `2`.

**Why this approach:** The bug is a compatibility gap in the PS2 rewrite stage, not in the upstream writer. Fixing the PS2 builder keeps the engine serializer authoritative, removes duplicated format assumptions, and prevents PS2 from drifting behind future payload changes.

**Testing:** Add a PS2 builder regression test that stages a scene containing a version `2` mesh payload, proves the initial build fails before the code change, and then verifies the rewritten disc scene contains the expected physical model and material paths after the fix.
