# Contributing to Latchkey

Thanks for your interest in improving Latchkey. This is a small, focused library — a tiny key/value
API over the OS-native credential store — so contributions that keep it small, honest, and
dependency-free are the most welcome.

## Prerequisites

- The **.NET 8** and **.NET 10** SDKs (the libraries multi-target `net8.0;net10.0`).
- Tests use [TUnit](https://tunit.dev/) on the Microsoft.Testing.Platform runner (configured in
  `global.json`); no extra tooling needed.

## Build and test

```sh
dotnet build                 # builds the whole solution (libraries, samples, tests)

dotnet test --project tests/Latchkey.Tests/Latchkey.Tests.csproj    # unit tests (net8.0 + net10.0)
```

Integration tests touch your **real** OS credential store (using unique, cleaned-up service names)
and run by default. To run unit tests only, filter them out:

```sh
dotnet test -- --treenode-filter "/*/*/*/*[Category!=Integration]"
```

Each native backend is tested on its own OS; CI runs the full integration suite across Windows,
macOS, and Linux, plus the `pass` and `systemd-creds` backends on Linux.

## Try the demos

```sh
dotnet run --project samples/Latchkey.QuickStart   # the ~10-line happy path (native store)
dotnet run --project samples/Latchkey.Tour         # a File-backed walkthrough of the whole API
```

## Code style

- The house style is enforced at build time via `.editorconfig` / `.globalconfig`, and warnings are
  treated as errors in the shipping libraries — please run `dotnet build` before pushing.
- The core `Latchkey` package must stay **zero-dependency, Native-AOT-clean, and trim-clean**. The
  AOT/trim analyzers are on; don't introduce reflection or a runtime package dependency in it.
- Match the surrounding code's naming, comment density, and idiom.

## Pull requests

1. Fork and create a topic branch off `main`.
2. Keep changes focused; add or update tests for behavior changes.
3. Make sure `dotnet build` and the unit tests pass locally.
4. Open a PR — CI must be green (the **CI success** check) before it can merge.

## Reporting security issues

Please do **not** open a public issue for vulnerabilities. See [SECURITY.md](SECURITY.md).
