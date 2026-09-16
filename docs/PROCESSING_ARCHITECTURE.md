# Processing architecture

Tally Prime Connector uses two complementary processing layers. C# is the core engine for ordinary accounting workflows; Python is the specialist engine for advanced analytical workloads. The C# desktop application remains the orchestrator.

```text
TallyPrime → C# Tally Integration → Normalized data → C# Core Processing → Reports / Excel

Specialist path:
TallyPrime → C# Tally Integration → Normalized data → C# Core Processing
                                                        ↓
                                           Python Specialist Processing
                                                        ↓
                                             C# result integration
```

Tally communication, SQLite lifecycle, WPF UI, jobs, and Excel remain in C#. Python receives normalized DTOs only and cannot connect to Tally or control the application. The specialist runtime will be bundled later; a global Python installation is not required for basic operation.

Phase 1.2 adds the read-only C# discovery boundary: verified XML-over-HTTP transport for the documented `EXPORT`/`COLLECTION` ledger request, typed XML parsing, mock discovery, and extraction orchestration. Unverified company, group, and voucher request formats remain explicitly blocked with `TODO: VERIFY WITH TALLYPRIME`.
