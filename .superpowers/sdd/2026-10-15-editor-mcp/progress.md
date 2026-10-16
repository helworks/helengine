# SDD ledger — plan: C:\dev\helworks\helengine\.worktrees\editor-mcp\docs\superpowers\plans\2026-10-15-editor-mcp.md

Base: bf7f163a94d6d17dd4c61e8a965b20e4aa7342ef; branch feature/editor-mcp.
User authorizes maximum two subagents, both gpt-6-luna xhigh. Reuse implementer/reviewer; neither may spawn children. New architectural decisions require Helena feedback.
Native worktree failed outside Git root; Git fallback created this checkout. Git Bash needs elevated sandbox access; translated task extraction to PowerShell because helper matches English Task headings only.

## Preflight
| Task | Internal consistency |
| --- | --- |
| 1 | Checked create/modify paths, interface and requested assertions; compatible with global constraints. Dependencies consumed only after their tasks. |
| 2 | Checked create/modify paths, interface and requested assertions; compatible with global constraints. Dependencies consumed only after their tasks. |
| 3 | Checked create/modify paths, interface and requested assertions; compatible with global constraints. Dependencies consumed only after their tasks. |
| 4 | Checked create/modify paths, interface and requested assertions; compatible with global constraints. Dependencies consumed only after their tasks. |
| 5 | Checked create/modify paths, interface and requested assertions; compatible with global constraints. Dependencies consumed only after their tasks. |
| 6 | Checked create/modify paths, interface and requested assertions; compatible with global constraints. Dependencies consumed only after their tasks. |
| 7 | Checked create/modify paths, interface and requested assertions; compatible with global constraints. Dependencies consumed only after their tasks. |
| 8 | Checked create/modify paths, interface and requested assertions; compatible with global constraints. Dependencies consumed only after their tasks. |
| 9 | Checked create/modify paths, interface and requested assertions; compatible with global constraints. Dependencies consumed only after their tasks. |
| 10 | Checked create/modify paths, interface and requested assertions; compatible with global constraints. Dependencies consumed only after their tasks. |
| 11 | Checked create/modify paths, interface and requested assertions; compatible with global constraints. Dependencies consumed only after their tasks. |
| 12 | Checked create/modify paths, interface and requested assertions; compatible with global constraints. Dependencies consumed only after their tasks. |
| 13 | Checked create/modify paths, interface and requested assertions; compatible with global constraints. Dependencies consumed only after their tasks. |
| 14 | Checked create/modify paths, interface and requested assertions; compatible with global constraints. Dependencies consumed only after their tasks. |
| 15 | Checked create/modify paths, interface and requested assertions; compatible with global constraints. Dependencies consumed only after their tasks. |
| 16 | Checked create/modify paths, interface and requested assertions; compatible with global constraints. Dependencies consumed only after their tasks. |
| 17 | Checked create/modify paths, interface and requested assertions; compatible with global constraints. Dependencies consumed only after their tasks. |
| 18 | Checked create/modify paths, interface and requested assertions; compatible with global constraints. Dependencies consumed only after their tasks. |
| 19 | Checked create/modify paths, interface and requested assertions; compatible with global constraints. Dependencies consumed only after their tasks. |
| 20 | Checked create/modify paths, interface and requested assertions; compatible with global constraints. Dependencies consumed only after their tasks. |
| 21 | Checked create/modify paths, interface and requested assertions; compatible with global constraints. Dependencies consumed only after their tasks. |
| 22 | Checked create/modify paths, interface and requested assertions; compatible with global constraints. Dependencies consumed only after their tasks. |
| 23 | Checked create/modify paths, interface and requested assertions; compatible with global constraints. Dependencies consumed only after their tasks. |

| Tasks | Shared file / contract | Producer-consumer assessment |
| --- | --- | --- |
| 1, 23 | C:\dev\helworks\helengine\engine\helengine.editor.automation\editor-tools.json | Sequential ownership; later task extends prior integration, no competing implementation. |
| 4, 5 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 4, 6 | C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorOperationDispatcher.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 4, 9 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 4, 10 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 4, 11 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 4, 15 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 4, 16 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 4, 18 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 4, 19 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 4, 20 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 4, 21 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 5, 9 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 5, 10 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 5, 11 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 5, 15 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 5, 16 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 5, 18 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 5, 19 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 5, 20 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 5, 21 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 9, 10 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 9, 11 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 9, 15 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 9, 16 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 9, 18 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 9, 19 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 9, 20 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 9, 21 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 10, 11 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 10, 15 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 10, 16 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 10, 18 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 10, 19 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 10, 20 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 10, 21 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 11, 15 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 11, 16 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 11, 18 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 11, 19 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 11, 20 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 11, 21 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 15, 16 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 15, 18 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 15, 19 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 15, 20 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 15, 21 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 16, 18 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 16, 19 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 16, 20 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 16, 21 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 18, 19 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 18, 20 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 18, 21 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 19, 20 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 19, 21 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 20, 21 | C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs | Sequential ownership; later task extends prior integration, no competing implementation. |
| 01, 02–23 | DTOs, catalog, limits | Global section 2 is single source; later tasks consume, no CLR objects in results. |
| 03, 07–09, 15, 23 | binding identity/generation | Server owns identity; connector routing must compare identity and generation. |
| 04, 05–23 | dispatcher/handler | Frame execution remains synchronous; long work returns job. |
| 05, 10–16, 18–21 | revisions/history | UI and MCP both advance; history saved-state separate. |
| 06, 07–23 | operation ledger | Dedup precedes revision revalidation; binding release must preserve in-flight results. |
| 09, 10 | editor_close/save policy | 09 delegates policy; 10 completes explicit transitions before end-to-end acceptance. |
| 17, 18–21, 23 | jobs | Request token independent; capture frame data before background work. |
| 08, 22–23 | stable connector | Distribution must preserve installed command across editor versions. |

Task 1: pending baseline; BASE bf7f163a94d6d17dd4c61e8a965b20e4aa7342ef. No product changes yet.
Baseline: 25/25 existing construction/dispose tests passed, exit 0. Existing IDE style warnings present before product changes. Evidence C:\dev\helworks\builds\helengine\editor-mcp\baseline-20261015\baseline.log. Task 1: implementation authorized; BASE bf7f163a94d6d17dd4c61e8a965b20e4aa7342ef; agent /root/implementer (gpt-6-luna xhigh).
Task 1: pending Helena architecture feedback (async question): SDK advertises schemas but does not enforce incoming arguments. Choice: bounded validator owned by catalog vs JSON Schema library dependency. Worker may continue DTO/catalog/transport proof; no validator implementation until answered. Runtime enforcement required regardless of choice.
Task 1: Helena answered: Biblioteca de JSON Schema, adicionando uma dependência. Runtime validation library authorized; worker selects pinned net9-compatible version, tests real evaluation, shared remains independent of engine/UI/MCP SDK. Previous architecture question resolved.
Task 1 progress: sandbox restore NU1301 blocked at loopback proxy; escalated restore approved and succeeded without disabling signature validation. Worker reports valid RED: catalog missing type fails and SDK HTTP/stdio test passes. Evidence pending full report at task01-red-20261015-a. Worker selected pinned JsonSchema.Net9.4.1 after official docs/net9 compatibility check; final task review will verify runtime enforcement and actual transport evidence.
Task 1: implementation reported DONE at 7b3f5eb858540aab4820707645af81de3f0e4bfb; 8/8 tests and framework-dependent publish passed. Review dispatched /root/reviewer (gpt-6-luna xhigh); package task-1-review-package.txt covers bf7f163a..7b3f5eb8. Pending review, not complete.
Task 1: review spec compliant, quality needs fixes. Important: shared mutable EditorToolDefinition allows advertised schema to diverge from compiled validator. Fix round1/5 dispatched implementer; FIX_BASE7b3f5eb858540aab4820707645af81de3f0e4bfb.
Task 1: minor (deferred): implemented-marker assertion checks CLR properties rather than catalog JSON; reviewer found no marker in current JSON. May be corrected alongside important fix, otherwise final review must triage.
Task 1: cross-task review caveats resolved as future-owned requirements: dynamic patch enforcement belongs tasks10–21 and canonical path/root enforcement tasks02/13/22. Global context requires dynamic schema validation, and task13 requires root confinement; task01 does not expose real mutations yet. Carry caveats to final review.
Task 1: fix round1 implemented cf9bdfdd99879714dd7ab1cae1d1f660a1e92b9b, RED7pass1fail -> GREEN8/8 catalog-only. Minor also corrected. Re-review dispatched with task-1-fix1-review-package.txt; awaiting verdict.
Task 1: fix round 1/5 (2 addressed, 0 open; commits 7b3f5eb8..cf9bdfdd). Re-review clean.
Task 1: complete (commits bf7f163a..cf9bdfdd, review clean). Deferred minor implemented-marker test resolved.
Task 2: starting; BASE cf9bdfdd99879714dd7ab1cae1d1f660a1e92b9b; implementer /root/implementer. Ownership discovery classes and registry tests.
Task 2: GREEN sandbox access denied after fixture DACL restricted to current user; sandbox requires its own inherited group ACE, incompatible with intended product restriction. Worker instructed to run focused ACL tests via approved escalation in same authorized builds directory, preserving ACL requirement and capturing environment limitation. No product security relaxation.
Task 2 diagnosis: File.Move overwrite fails despite ReadWrite|Delete sharing; File.Replace then measured actual destination absence (58 File.Exists false, 43 zero candidate reads, callback FileNotFound/sharing errors). Worker testing native FileRenameInfoEx REPLACE_IF_EXISTS|POSIX_SEMANTICS without changing approved storage model; must verify Windows support, preserve atomicity, report unsupported errors. Any storage/coordination model or support-policy change requires Helena feedback.
Task 2: POSIX rename direct-open failure traced to publisher temporary handle using FileShare.None after rename. Changed compatible sharing; direct-open concurrency1/1 and full filter5/5 passed; enumeration is not a snapshot and tested quiescent separately.
Task 2: self-review found TOCTOU remove race despite double reads then File.Delete(path). Worker implementing identity verification and deletion through same stable handle with deterministic replacement-race coverage; no public API/storage model change.
Task 2: pending Helena async compatibility choice: require modern Windows for MCP atomic rename vs old WindowsServer runtime support. Minimum1607 based documented RS1 field gate is under further factual verification; do not claim tested support there. Work on current-machine fix continues independently.
Compatibility correction: Microsoft FILE_INFORMATION_CLASS documents FileRenameInformationEx available starting Windows10 1709; RS1 macro1607 gates header field only. Corrected pending Helena question to1709 and informed implementer. Sources https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/wdm/ne-wdm-_file_information_class and MS-FSCC rename specification. No old-version support claim accepted.
Task 2: implementation reported DONE_WITH_CONCERNS at950dc45faeb7285c7d020b5b6e9ad644dff4c6a0; 6/6 current-Windows tests, xUnit2013 warning plan count assertion. Compatibility policy pending. Technical review dispatched /root/reviewer with task-2-review-package.txt covering cf9bdfdd..950dc45f. Not marked complete.
Task 2: technical review spec compliant in verifiable scope; quality Approved at950dc45f. Not complete until Helena compatibility policy resolved.
Task 2: minor (deferred): atomic-publication test does not deterministically ensure a read occurs during replacement interval; final review must triage strengthening synchronization.
Task 2: minor (deferred): brief-mandated Assert.Equal(2,Candidates.Count) causes xUnit2013; preserve two-candidate requirement while addressing assertion hygiene in later test change/final review.
Task 3: read-only preparation complete, no edits. Pending Helena async contract choice: preserve Get(string)->EditorBindingDescriptor and use typed service exception converted to MCP result vs change service to success/error result. Constructor defensive EditorInstanceDescriptor+EditorAutomationLimits composition routine; detailed states/errors to follow approved choice.
Controller checkpoint: waiting on required user architecture feedback per AGENTS instruction. No task03 implementation or task04+ work started. Both existing agents reusable, max two total, gpt-6-luna xhigh. Latest HEAD950dc45faeb7285c7d020b5b6e9ad644dff4c6a0; worktree should be clean.

Portability correction from Helena: editor will run on Linux and macOS in the future. Withdraw the pending Windows 10 1709+ product-policy question; that version came from our choice of FileRenameInfoEx, not an MCP requirement. Task 2 reopened for portability review: current implementation is Windows-only and its passing Windows tests do not establish portable support. Spec and plan now record the three-platform requirement. Do not introduce more Windows-only shared implementation or select a replacement storage architecture without Helena feedback. Reassess whether prior BCL replacement failures were affected by the subsequently corrected temporary-file sharing mode before concluding native rename is required. No task 3 implementation authorized by the unresolved error-contract question.

Architecture revision directed by Helena: use a shared local coordinator server, started by the first editor and stopped when all editors close, tracking live editors in memory. This supersedes filesystem discovery and the native Windows task02 implementation. Current work is revision of spec/plan/coverage only by existing implementer; no new product implementation. Preserve per-editor ephemeral MCP HTTP and explicit binding; stable connector discovers via coordinator. Coordinator must survive closure of its launching editor, handle concurrent startup/last-close races and crashed editors, and avoid replay of uncertain mutations. New implementation briefs must be regenerated from revised docs before dispatch; old task02/07/08/09/22 briefs and copied global-context sections are obsolete. Do not resurrect Windows minimum or registry ACL work.
Coordinator documentation revision complete in spec, plan and coverage (no product code edits or commit). Implementer updated Task02 replacement, Tasks07–09/22 and CON-01. Reviewer found three material ambiguities; controller corrected all: coordinator candidate process owns socket with no handoff; EditorMcpLifetime exclusively owns register/lease/unregister while HTTP host only manages endpoint; serialized last-unregister/lease-expiry versus new registration plus initial registration timeout specified and test cases added. git diff --check passed. Authentication/bootstrap still an explicit unresolved implementation decision; affected tasks not ready to dispatch. No tests/builds run for this documentation-only revision. Next execution must regenerate stale briefs. Current file-based code remains untouched and obsolete, not a working coordinator implementation.

Helena authorized implementation again: 'vai' and clarified 'eu quero implementar com o gpt6 luna xhigh'. Reusing exactly existing implementer/reviewer (gpt-6-luna xhigh), no children. Task02 resumed as independently testable in-memory registry/lifecycle core while auth/bootstrap transport decision remains unresolved; reviewer investigating simplest concrete auth options. Implementer owns coordinator core/new focused tests only, no transport, no public registration or credentials API, no obsolete Windows code deletion yet. Core API approved as routine detail: TimeProvider and lease/initial deadline; TryRegister, identity-matched TryHeartbeat/TryUnregister, detached GetSnapshot, TryBeginShutdownIfIdle, IsShuttingDown. Monotonic timeout measurement; initial deadline only before any registration; last unregister after registration allows shutdown immediately. No new architecture decided by controller.
