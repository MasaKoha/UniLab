# AGENTS.md

## Code Style Rules

- Keep control flow flat. Avoid deep nesting by preferring guard clauses, early `return`, early `continue`, and small method extraction.
- Always use braces `{}` for control statements (`if`, `else`, `for`, `foreach`, `while`, etc.), even for single-line bodies.
- Prefer `var` for local variable declarations when the type is obvious from the right-hand side.
- Follow existing editor naming conventions in this repository:
  - private fields (including `[SerializeField]`) use `_camelCase`
  - method names / public members / type names use `PascalCase`
  - local variables and parameters use `camelCase` (prefer `var` when applicable)
- Do not use `[FormerlySerializedAs]` or similar attribute-based migration when renaming `[SerializeField]` fields. Rename the serialized field directly in Prefab/scene data so references are not detached.
- Do not add null checks for variables declared with `[SerializeField]`. Assume Inspector always has a valid instance assigned.
- Do not write end-of-line comments. If a comment is needed, write it on the line immediately above the target code.
