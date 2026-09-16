---
name: tally-prime-connector
description: Build or modify Tally Prime Connector integrations, workflows, MCP tools, CLI commands, and processing features while preserving local-first security boundaries.
---

# Tally Prime Connector

Keep Tally communication in `TallyPrimeConnector.Tally` behind existing contracts. Use only verified supported TallyPrime XML/HTTP, ODBC, or TDL mechanisms. If a protocol detail is not verified, mark it `TODO: VERIFY WITH TALLYPRIME`; do not invent it.

The WPF UI, CLI, and MCP adapter call Core/application contracts rather than Tally protocol code directly. Never add bypasses for company security, passwords, protected data, memory, or Tally database files.

Use C# Core Processing for normal workflows. Route only explicit `specialist.*` jobs to the Python Specialist Processing boundary. Python receives normalized DTOs and never communicates with Tally, controls the UI, or owns application lifecycle.

Use phase-based reporting in `docs/projectstatus/`: create a report only when the main prompt supplies a new phase number; otherwise update the current phase report with changes, verification, and blockers.
