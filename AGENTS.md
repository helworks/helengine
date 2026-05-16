# Repository Guidelines

- One class per file.
- Add XML comments to every class, function and or property. Make sure the code is as easy as possible to be read by humans.
- XML comments must be substantive (avoid placeholder text) and explain the role/behavior. All members (fields, properties, methods, constructors) should have XML comments.
- All projects use the default Global Usings; no need to keep adding back imports.
- Order members using standard C# layout (constants/fields, constructors, properties, methods) to keep files predictable.
- Do not add redundant `private` modifiers; members without an access modifier are assumed private in C#.
- All fields should be PascalCase.
- C# code should follow TypeScript indentation rules for end and close brackets, and opening braces should be on the same line as declarations or control statements.
- Do not use tuples.
- Follow MVC: keep logic in separate classes (controllers/services/managers) and keep UI classes focused only on presentation and input wiring.
- Avoid half-measures that patch broken state; ensure systems are correctly initialized or fix the underlying cause instead of bolting on runtime fixes (e.g., do not add auto-attach helpers like `EnsureCameraControls`).
- Do not mask failures with "best-effort" patches (for example, catching formatter errors and returning the original output) unless explicitly requested; preserve existing failure behavior and fix the root cause.
- Avoid `Mathf`; use `double` math and convert back to `float` where needed (modern processors handle `double` faster in this context).
- Do not create local helper functions; if a helper is needed, add it to the appropriate Utils class or to a related type (for example, quaternion math should live on the quaternion type).
- Avoid repeated `as` casts; prefer engine events or other patterns to avoid per-call conversions.
- Do not create default values when a valid value is required; throw exceptions instead of silently constructing defaults (example: do not replace null `CoreInitializationOptions` with a new instance).
- Nullable reference types are disabled; do not use nullable annotations or nullable patterns in code.
- Use a well-formatted `if / else if` chain for mutually exclusive null checks.
- Run the smallest validation necessary for the scope of the change.
## Command Output
Protect context usage. **Any command with unknown or potentially large output must be byte-capped.**
Default pattern:
```bash
COMMAND 2>&1 | head -c 4000
```