# Python Specialist Processing Engine

This is a first-class specialist engine in the product architecture. It is selected by the C# application for specialist analytical jobs only; normal accounting workflows use the C# core engine and do not require Python.

The eventual installer will bundle and manage a local Python runtime plus specialist dependencies (such as pandas and NumPy). Users must not need Python on PATH, pip, virtual environments, or manually installed packages.

Phase 1.1 supplies a JSON-lines protocol skeleton (`tally_prime_connector_specialist/specialist_engine.py`) and the C# integration boundary. It does not implement algorithms, package a runtime, or invoke a global Python installation.
