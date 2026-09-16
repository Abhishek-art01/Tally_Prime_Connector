# Tally Prime Connector

Tally Prime Connector is a Windows desktop application for connecting to TallyPrime through supported integration mechanisms and extracting, processing, validating, and exporting accounting data. Tally remains the source of truth.

## Architecture

```
Windows UI → Application Core → Tally Integration → TallyPrime
```

Processing has two deliberate layers: the C# Core Processing Engine handles ordinary accounting workflows, while the Python Specialist Processing Engine is reserved for advanced analytical workloads. The product will bundle its specialist Python runtime; ordinary workflows never require a user-managed Python installation. See [processing architecture](docs/PROCESSING_ARCHITECTURE.md).

The Next.js website is limited to landing, download, documentation, and support. It does not communicate with TallyPrime.

## Development

Requires .NET 10 SDK and Node.js 22+.

```powershell
dotnet build TallyPrimeConnector.sln
dotnet test TallyPrimeConnector.sln
cd apps/TallyPrimeConnector.Site
npm install
npm run dev
```

`Development` uses a mock Tally provider, so a live Tally installation is not necessary for UI development.

## CLI and MCP foundations

`apps/TallyPrimeConnector.Cli` provides a local command-line foundation (`companies`, `connection-test`). `src/TallyPrimeConnector.Tally.Mcp` provides host-neutral, application-contract-backed MCP tools for future local MCP transport hosting. Neither component bypasses TallyPrime security or communicates with Tally outside the Tally integration layer.

Implementation status is recorded by phase in [project status reports](docs/projectstatus/README.md). Repository guidance and reusable skills are in [.agent](.agent/RULES.md).
