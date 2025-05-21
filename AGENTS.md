# Repository Guidelines

This repository contains a small CSV parsing/writing library and accompanying MSTest suite.
The code targets multiple frameworks (including `netstandard2.0`, `net8.0` and `net9.0`).

## Contributing with Codex

* Run `dotnet test` from the repository root to execute the test suite.
* Use four spaces for indentation in C# files and follow the existing brace style.
* Commit messages should follow a short `type: description` format (e.g. `feat: add cool feature`, `fix: handle edge case`).
* Keep the project files (`*.csproj` and the solution) compatible with the current target frameworks unless specifically asked to change them.
* If tests or builds fail due to missing tools or network access, mention this in your PR.
