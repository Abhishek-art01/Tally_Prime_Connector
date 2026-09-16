# Tally Prime Connector agent rules

## Product boundaries

- The Windows application is the product. The website is limited to landing, download, documentation, support, privacy, and terms.
- TallyPrime communication stays local and only in the C# Tally integration layer.
- Do not invent Tally protocol behavior. Mark unknown integration details `TODO: VERIFY WITH TALLYPRIME`.
- Never bypass Tally company security, passwords, protected data, memory, or database files.

## Processing

- C# Core Processing is the default engine for ordinary accounting and workflow processing.
- Python Specialist Processing is a required architectural component for specialist analytical jobs, but global Python installation is never a basic application prerequisite.
- Python receives normalized application data only. It does not communicate with TallyPrime or control the UI, SQLite, or application lifecycle.

## Architecture and quality

- Keep UI, CLI, and MCP code dependent on Core/contracts rather than direct Tally protocol implementations.
- Use decimal for financial values and cancellation-aware async I/O.
- Keep application-owned data in SQLite; Tally remains accounting-data source of truth.
- Do not claim builds, tests, connections, or runtime capabilities that have not actually been verified.

## Reporting

- Reports are phase-based, not prompt-based. Create a new report only for a new main prompt that supplies a phase number; otherwise update the applicable existing phase report using that phase-number prefix.
- Include the request, changed files/behavior, verification performed, and blockers.

## Repository agent resources

- Reusable skills belong in `.agent/skills/`.
- Apply `.agent/skills/tally-prime-connector/SKILL.md` for Tally Prime Connector implementation work.
