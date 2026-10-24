# Instant Play B: PSoXide Play session (window + emucap + tapes) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A PSoXide "Play session": a visible window Helena plays in, controllable through emucap, that records every session to one input tape and, on the next launch, replays that tape to where the last session ended, reports same-state or diverged, and hands control to the player.

**Architecture:** The session's emulation core is psoxide-emucap's `Host`/`Machine` (the same core emucap's headless backend uses), so record, replay and agent control share one set of semantics. A new `play_session` module in psoxide-emucap owns tape recording, replay-to-end and the divergence check, and runs headless too (testable without a window). The GUI frontend gets a `play-session` subcommand that opens a window and drives that core: drains emucap jobs between frames, feeds live pad input, presents the display and plays audio. emucap's psoxide backend launches that subcommand when `display: true`.

**Tech Stack:** Rust (PSoXide-emulator workspace: emulator-core, psoxide-emucap, frontend), Rust (emucap).

**Spec:** `docs/superpowers/specs/2026-10-24-instant-console-play-design.md` (helengine b718e801), sections "Emulator window and control", "Native Play pipeline" stage 7, "Calls made by Claude".

**Repos:** `C:\dev\helworks\reference\ps1\PSoXide-emulator` (origin helworks/PSoXide-emulator, main) and `C:\dev\helworks\reference\emucap` (origin helworks/emucap, main). Fetch and rebase on origin/main before starting each repo (PSoXide's upstream moves fast; `upstream` remote is EBonura's).

## Global Constraints

- Work on main in the real checkouts; no worktrees, no branches. Small commits, one per task, ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; push to origin after each repo's tasks pass.
- One tape per project and platform, overwritten; no timestamped copies.
- A fast path must never silently run stale state: a replay that does not reach the recorded state says so.
- Temporary files under `C:\temp\play-session\`, deleted when done.
- Ask Helena before launching a windowed emulator (Task 6). Headless runs in tests are fine.
- Match the surrounding code style and comment density. No tests that string-match source.
- After editing, `git diff --numstat` must show only real changes (no whole-file line-ending rewrites).
- emucap: keep `cargo test --release` green (1927 passed at ebdbe58); PSoXide: `cargo test -p emulator-core --lib --tests`, `-p psoxide-emucap`, `-p psoxide-settings` green (`cargo test -p emulator-core` alone cannot link on Windows because of an unrelated example).

## Review Focus

1. Player closes the window or the process is killed mid-session: the tape on disk is the last periodic flush, never a half-written file (atomic save). Test in Task 2.
2. A code change makes the game poll more or fewer times than the tape holds: replay still reaches the recorded end frame, reports `diverged`, and live input takes over; no hang. Test in Task 2.
3. A tape recorded against a different disc (game hash mismatch): replay still runs and the report says the image changed. Test in Task 2.
4. An agent calls `set_input`/`write_memory`/`load_state`/`reset` during a Play session: refused with `bad_state` so the tape stays a faithful record. Test in Task 3.
5. Tape file missing, empty or corrupt: the session starts fresh (recording from boot) and reports `no-tape` or the parse error; it never refuses to boot. Test in Task 2.

---

### Task 1: Tape end marker (PXITAPE3)

**Files:**
- Modify: `emu/crates/emulator-core/src/input_tape.rs`

**Interfaces:**
- Produces: `pub struct TapeEnd { pub frame: u64, pub display_hash: u64 }`; new field `Tape.end: Option<TapeEnd>`; `write_tape_poll_bound(path: &Path, samples: &[PadSample], start_poll: u64, end: Option<TapeEnd>) -> Result<(), String>` writes `PXITAPE2` when `end` is `None` and `PXITAPE3` when `Some`; `tape_from_bytes` reads both. Also `pub fn tape_poll_bound_bytes(samples, start_poll, end, game_hash: Option<u64>) -> Result<Vec<u8>, String>` (the writer's byte builder, used by Task 2's atomic save).
- PXITAPE3 layout: magic `PXITAPE3`, u32 count, u32 start_poll, u64 end_frame, u64 end_display_hash, u64 game_hash (0 = unknown), then the PXITAPE2 6-byte samples. Exact length is required, as for PXITAPE2.

- [ ] **Step 1: Write failing tests** in the module's test block (beside the existing tape tests at :464+):
  - `poll_bound_tape_with_end_round_trips`: write samples `[from_buttons(0x0001), from_buttons(0x4000)]`, start_poll 7, end `TapeEnd { frame: 1234, display_hash: 0xDEAD_BEEF_0000_0001 }`, game_hash `Some(42)`; read back: same samples, `clock == TapeClock::PadPoll`, `start_poll == 7`, `end == Some(..)` equal, `game_hash == Some(42)`.
  - `pxitape2_reads_with_no_end`: existing PXITAPE2 bytes parse with `end == None`.
  - `truncated_pxitape3_is_rejected`: drop the last byte, expect `Err`.
- [ ] **Step 2:** `cargo test -p emulator-core --lib input_tape` → the new tests fail to compile/fail.
- [ ] **Step 3:** Implement; update every caller of `write_tape_poll_bound` and every `Tape { .. }` literal in the workspace (frontend `playtest_input.rs`, cli.rs, psoxide-emucap) to pass `None` / `end: None`, keeping their behaviour.
- [ ] **Step 4:** `cargo test -p emulator-core --lib --tests` and `cargo build -p psoxide-emulator -p psoxide-emucap` pass.
- [ ] **Step 5:** Commit "Add an end marker to poll-bound input tapes (PXITAPE3)".

### Task 2: `play_session` core in psoxide-emucap

**Files:**
- Create: `emu/crates/psoxide-emucap/src/play_session.rs`
- Modify: `emu/crates/psoxide-emucap/src/machine.rs` (recording hook), `src/lib.rs` (module + `--tape` host flag), `src/host.rs` (`machine_mut`)
- Test: `emu/crates/psoxide-emucap/tests/play_session.rs` (reuse the hand-assembled pad-polling EXE and helpers from `tests/host.rs`; move shared helpers into `tests/common/mod.rs` if both files need them)

**Interfaces:**
- Consumes: Task 1 `TapeEnd`, `tape_poll_bound_bytes`, `read_tape_full`.
- Produces:
  - `Machine::start_recording(&mut self)`; `Machine::recorded_samples(&self) -> &[PadSample]` — one entry per completed port-1 poll since boot holding the sample the guest read at that poll (from a tape or the port override alike), recorded where the machine already detects completed polls for tape playback.
  - `Host::machine_mut(&mut self) -> &mut Machine`.
  - `pub enum ReplayReport { NoTape, TapeUnreadable(String), SameState, Diverged { expected: u64, actual: u64 }, }` plus `pub image_changed: bool` carried alongside (struct `ReplayResult { pub report: ReplayReport, pub image_changed: bool }`).
  - `pub struct PlaySession` with `pub fn begin(machine: &mut Machine, tape_path: PathBuf) -> PlaySession` (reads the tape if present; starts tape playback with `stop_on_end = false` when it has samples; always calls `start_recording`), `pub fn replaying(&self) -> bool` (true until the machine's frame reaches the tape's `end.frame`), `pub fn after_frame(&mut self, machine: &mut Machine) -> Option<ReplayResult>` (returns once, at the first frame boundary with `machine.frame() >= end.frame`: stops the tape, compares `machine.display_hash().0` with `end.display_hash`; returns `NoTape`/`TapeUnreadable` once right after begin when there is nothing to replay), `pub fn save(&self, machine: &Machine) -> Result<(), String>` (writes `recorded_samples`, start_poll 0, `TapeEnd { frame: machine.frame(), display_hash: machine.display_hash().0 }`, the boot image's game hash, to `<path>.tmp` then renames over `<path>`), `pub fn frames_since_save(&self) -> u64` for the periodic flush.
  - A tape whose end frame is reached before its samples run out, or whose samples run out first, behaves the same: the tape stops at `end.frame`, missing polls read the neutral pad, then live input takes over.
  - Headless host flag `--tape <path>`: `run` creates a `PlaySession`, calls `after_frame` after every free-running frame, emits the result as a `play_replay_result` event (`{"report": "same_state"|"diverged"|"no_tape"|"tape_unreadable", "expected", "actual", "image_changed"}`) through `Machine::push_event`, saves every 600 frames and on shutdown.

- [ ] **Step 1: Write failing tests** in `tests/play_session.rs`:
  - `recorded_session_replays_to_same_state`: boot the pad EXE, `begin` with a fresh temp path, run 120 frames setting port-1 override to Cross on frames 10-20, `save`; boot a new machine, `begin` with the same path, run frames until `after_frame` returns: `report == SameState`, `image_changed == false`, and RAM at the EXE's pad-result address equals the first run's at the end frame.
  - `changed_input_reports_diverged`: same, but hand-edit one recorded sample in the saved tape before the second run (rewrite via `tape_poll_bound_bytes`): `Diverged { expected, actual }` with `expected != actual`.
  - `missing_tape_reports_no_tape_and_records`: `begin` on a non-existent path → first `after_frame` returns `NoTape`; after 30 frames `save` creates a readable PXITAPE3 with `end.frame == 30`.
  - `corrupt_tape_reports_unreadable_and_starts_fresh`: path holds `b"garbage"` → `TapeUnreadable(_)`, session records from boot.
  - `short_tape_hands_over_after_end_frame`: a tape with fewer samples than the guest polls before `end.frame` still yields a result at `end.frame` and `replaying()` turns false.
  - `save_is_atomic`: after `save`, no `<path>.tmp` remains; the file parses.
  - `different_image_sets_image_changed`: save with one EXE, replay against a byte-modified copy → `image_changed == true`.
- [ ] **Step 2:** `cargo test -p psoxide-emucap --test play_session` → fails.
- [ ] **Step 3:** Implement as specified above.
- [ ] **Step 4:** `cargo test -p psoxide-emucap` (all, including existing 36) passes.
- [ ] **Step 5:** Commit "Add Play sessions to psoxide-emucap: record every session, replay to its end, report divergence".

### Task 3: Play sessions own the input

**Files:**
- Modify: `emu/crates/psoxide-emucap/src/host.rs`
- Test: `emu/crates/psoxide-emucap/tests/host.rs`

**Interfaces:**
- Produces: `Host::set_play_session(&mut self, on: bool)`. While on, methods that would put input or state into the guest outside the tape (`set_input`, `press_buttons`, `tap`/any input-injecting method the host implements, `write_memory`, `load_state`, `reset`, starting an input tape) return error kind `bad_state` with message `"a Play session owns input and state; stop the session to inject"`. Reads, screenshots, pause/resume, frame/instruction/poll steps and breakpoints stay allowed. `capability_notes` gains `"play_session": true` when on.

- [ ] **Step 1:** Tests `play_session_refuses_input_injection` (each refused method → `err_kind == "bad_state"`), `play_session_allows_reads_and_steps` (read_memory, screenshot, step frames → ok).
- [ ] **Step 2:** Run → fail. **Step 3:** Implement; `--tape` in `run` turns it on. **Step 4:** `cargo test -p psoxide-emucap` passes.
- [ ] **Step 5:** Commit "Refuse input injection while a Play session records".

### Task 4: Frontend `play-session` window

**Files:**
- Create: `emu/crates/frontend/src/play_session_window.rs`
- Modify: `emu/crates/frontend/src/cli.rs` (subcommand), `src/main.rs` (dispatch: this subcommand opens a window, unlike other subcommands at :173-181), `src/audio.rs`/`src/gfx.rs` only if the presenter or audio need a `&Bus`-based entry point extracted
- Modify: `emu/crates/psoxide-emucap/src/transport.rs`: `pub fn drain_jobs(host: &mut Host, jobs: &Receiver<Job>) -> bool` (non-blocking; executes pending jobs; returns false when every sender is gone), with a unit test beside the existing transport tests.

**Interfaces:**
- Consumes: Task 2 `PlaySession`, `Host::machine_mut`; Task 3 `set_play_session`; `transport::run_producer` and `ProducerConfig` from the emucap env (`EMUCAP_PORT`, `EMUCAP_SESSION_TOKEN`, `EMUCAP_LAUNCH_ID`, `EMUCAP_NAME`, `EMUCAP_CONTENT`) exactly as `psoxide_emucap::run` reads them.
- Produces: `psoxide-emulator play-session --disc <cue|bin|iso|exe> --tape <path> [--emucap] [--windowed]`.
  - Boot the content into a `Machine` the way `psoxide_emucap::run` does (HLE BIOS, same pad profile as the headless host), wrap it in a `Host`, `set_play_session(true)`, `PlaySession::begin`.
  - With `--emucap`, start `run_producer` on a thread exactly as the headless host does; the window loop calls `drain_jobs` at the top of every redraw.
  - Per guest frame while the host is Running: if `!session.replaying()`, map the live keyboard/gamepad port-1 state (the existing input model, `Port1PadSample::from_host` → `PortInput`) to `machine_mut().set_port_input(0, Some(..))`; `host.run_free_frame()`; `session.after_frame` → on a result print `[play-session] replay: same_state|diverged expected=.. actual=..|no_tape|tape_unreadable: ..` (plus `image_changed`) to stderr and push the `play_replay_result` event; run the SPU and push audio as main.rs:1689-1704 does; save every 600 frames.
  - While replaying, run frames unthrottled (no pacing wait) and present at most once per redraw; after hand-over, pace to the guest's vblank period like `serve_jobs`.
  - Present the display from `host.machine().bus()` with the frontend's existing presenter (no egui panels). Window close: `session.save`, then exit 0.
  - The `mcp` feature's server is not started in this mode.

- [ ] **Step 1:** `drain_jobs` test in transport.rs (two queued jobs executed, returns false after the sender drops) → fails. **Step 2:** implement → passes.
- [ ] **Step 3:** Implement the subcommand. Keep window/present/audio glue thin; all session logic stays in Task 2's tested module.
- [ ] **Step 4:** `cargo build --release -p psoxide-emulator --features emucap` succeeds; `cargo test -p psoxide-emucap` passes; `psoxide-emulator play-session --help` lists the flags.
- [ ] **Step 5:** Commit "Add a play-session window driven by the emucap Play core". Push PSoXide-emulator (Tasks 1-4) after a final `cargo fmt --all --check`.

### Task 5: emucap launches the window for `display: true`

**Files:**
- Modify: `C:\dev\helworks\reference\emucap\src\launch\psoxide.rs`, `src\launch\psoxide_tests.rs`, `src\bin\emucap-mcp\launch\run.rs` (~:857-863 headless-only rejection), `src\bin\emucap-mcp\launch\plan.rs` (~:1173 text), `src\bin\emucap-mcp\args.rs` (new launch arg), `src\bin\emucap-mcp\launch_tests.rs`, `AGENT_GUIDE.md`, `AGENT_GUIDE.ko.md`, `CHANGELOG.md`

**Interfaces:**
- Produces: launch/launch_plan arg `play_tape: Option<String>` (absolute path; psoxide backend only; `bad_params` elsewhere).
  - `display: true` + psoxide: binary from `EMUCAP_PSOXIDE_GUI_BIN`, else `psoxide-emulator(.exe)` beside the emucap binary, else `target/release`, else PATH; args `play-session --emucap --disc <content> --tape <play_tape>`; same env as the headless host.
  - `display: true` without `play_tape` is `bad_params` ("a PSoXide window is a Play session; pass play_tape").
  - Headless + `play_tape`: the headless host gets `--tape <play_tape>`.
  - `runtime_paths`/status show the GUI binary and `EMUCAP_PSOXIDE_GUI_BIN`.

- [ ] **Step 1:** Tests in psoxide_tests.rs: `display_launch_uses_the_play_session_window` (binary name, args, env), `headless_launch_forwards_play_tape`, `display_launch_without_play_tape_is_rejected`; in launch_tests.rs: plan with `display: true` + `play_tape` is ready.
- [ ] **Step 2:** Run → fail. **Step 3:** Implement and update docs/CHANGELOG. **Step 4:** `cargo test --release` → all pass (≥1927 + new).
- [ ] **Step 5:** Commit "Launch PSoXide Play sessions in a window for display:true"; push emucap.

### Task 6: End-to-end check (ask Helena first: opens an emulator window)

- [ ] **Step 1:** Ask Helena for permission to open the PSoXide window.
- [ ] **Step 2:** Through emucap-mcp: `launch` psx, `psx_backend=psoxide`, `display: true`, content `C:\dev\helworks\builds\ps1\package\helengine_ps1.cue`, `play_tape` `C:\temp\play-session\demodisc.pxtape`; Helena plays a few seconds in the menu and closes the window.
- [ ] **Step 3:** Launch again the same way: the stderr line and the `play_replay_result` event say `same_state`; the menu sits where she left it; live input works after the hand-over; an agent `read_memory` and `screenshot` succeed and `write_memory` is refused.
- [ ] **Step 4:** Report the results; delete `C:\temp\play-session\`.
