# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

bstrings is a "better strings" CLI for DFIR, part of Eric Zimmerman's tool suite. It extracts code page (ASCII) and UTF-16LE strings from binary files and can filter them by literal strings or regexes, including a built-in pattern library (GUIDs, IPs, SIDs, crypto wallet addresses, etc.). Usage and options are in README.md.

## Commands

The SDK is pinned in `global.json` (.NET 10).

```bash
dotnet build -c Release                               # builds bstrings.slnx, both net472 and net10.0, plus tests
dotnet build bstrings/bstrings.csproj -f net10.0      # one target only
dotnet test -c Release                                # all tests (CI runs this after the build)
dotnet test --filter "FullyQualifiedName~StringScannerTests"           # one fixture
dotnet test --filter "Name=OffsetsPointAtTheStringBytes"               # one test
dotnet run --project bstrings -f net10.0 -- -f path/to/file --lr guid --off
```

CI (`.github/workflows/build.yml`) builds both targets and runs the tests. There is no lint config.

## Tests

`bstrings.Test` (NUnit 5 + FluentAssertions 7 pinned to `[7.0.0,8.0.0)`, net10.0, `LangVersion` 10). Test data is generated in code; there are no sample files.
- `StringScannerTests` drive `StringScanner` directly (it and `BuiltInPatterns` are internal; `InternalsVisibleTo` is set in the csproj). Key invariants: hits don't depend on chunk size (down to 1-byte chunks), and every offset points at the string's bytes.
- `BuiltInPatternTests`: a matching and a non-matching sample per built-in pattern (a new pattern needs one), and checks that README.md and `dev-tools/compare-versions-output.ps1` list every pattern.
- `CliTests` run the built `bstrings.dll` (copied next to the tests by the project reference) with `dotnet` as a separate process, for exit codes, stdin, filters, sorting, `-o` and `-d`.

For changes to scanning or output, also compare old and new builds over real files (large `.so`/`.dll`/memory dumps) with `-q -s -o` and diff:
- `dev-tools/benchmark-generator` writes a 512 MB `benchmark.dmp` with strings planted across chunk boundaries; the expected hits are in its comments (`-f benchmark.dmp --ls bstrings --lr unc -b 10 --off`).
- `dev-tools/compare-versions-output.ps1` runs two builds over a memory dump with every built-in regex and asserts identical output.

Exit code 0 means success; 1 means invalid arguments or a file that failed.

## Architecture

The CLI and search loop are in `bstrings/Program.cs`; the scanner is in `bstrings/StringScanner.cs`:

- **`Main`** builds a `System.CommandLine` `RootCommand` and awaits `InvokeAsync`, which calls **`DoWork`**. To add an option: (1) create the `Option<T>`, (2) add it to the `RootCommand` initializer, (3) pass `result.GetValue(...)` through `DoWork` (and `Search` if needed). `-u` was once bound to the wrong option, so check the binding.
- **`DoWork`** validates arguments, handles stdin (only when neither `-f` nor `-d` is given — a redirected stdin must not override `-f`, see issue #17) and calls **`Search`**.
- **`Search`** builds the scanners and filters once, enumerates files, then per file: streams it in `-b` MB chunks through every scanner, collects `Hit`s in a `HashSet` (dedup; with `--off` the offset is part of the key), optionally sorts, filters and writes hits to a buffered stdout writer and/or `-o`. Status messages go through Serilog; hits do not.
- **`StringScanner`** (`StringScanner.cs`) is the core. One instance per encoding/alignment: UTF-16LE at even offsets, UTF-16LE at odd offsets (skip 1 byte), and the `--cp` code page. Each chunk is decoded with a stateful `Decoder` (handles multi-byte sequences split across chunks), and the regex `range{min,max}` runs over it. A run of in-range characters that reaches the end of a chunk is carried over to the next chunk instead of being reported, so strings are never split and results don't depend on chunk size. The in-range test uses a 64K lookup table built from the range (`BuildRangeTable`).
- **Offsets** are exact: fixed-width encodings compute `skip + width * charIndex`; multi-byte code pages use a running `GetByteCount`. Leading whitespace trimmed from a hit is added to its offset.
- **Filters** (`--ls`, `--fs`, `--lr`, `--fr`) match against the hit text only, never the offset suffix. String filters are ordinal ignore-case; regexes are `IgnoreCase | CultureInvariant | Compiled`. A hit is written once even if several filters match (except `--ro`, which writes every regex match).
- **Built-in regexes** live in `BuiltInPatterns.cs` (`Patterns`/`Descriptions`); `--lr <name>` resolves a name, otherwise the value is a raw regex. Adding one means adding it to README, `compare-versions-output.ps1` and `BuiltInPatternTests.Samples` (tests enforce all three). `url3986` uses inline `(?x)` since filters don't use `IgnorePatternWhitespace`.
- **Locked files** (Windows only): when a normal open fails with an IO/access error, the file is read through raw NTFS access (RawDiskLib + DiscUtils.Ntfs), with one file system cached per drive letter.

### Dual-target conditional code

`#if NET9_0_OR_GREATER` separates the targets:
- **net472** aliases `File`/`Directory`/`Path`/`FileInfo` to AlphaFS (long paths) and enumerates with `DirectoryEnumerationFilters`; the scanner makes a string per chunk and uses `Match`/`NextMatch`.
- **net10.0** uses `System.IO` with `FileSystemEnumerable` (skip empty files and inaccessible folders, don't recurse into reparse points); the scanner uses `Regex.EnumerateMatches` over a span, avoiding the per-chunk string.

Keep both branches behaving the same and build both targets. AlphaFS is Win32-only, so the net472 build can't be run under Mono.

### Build details

- Costura.Fody (`bstrings/FodyWeavers.xml`) embeds dependencies into a single exe.
- `LangVersion` is pinned to 10 (no record structs on net472 without polyfills, no `foreach` over `ValueMatchEnumerator`).
- Exceptionless telemetry starts at the top of `Main`.

## Versioning and dependencies

- bstrings is not packable. Its version is `<Version>` in `bstrings/bstrings.csproj` (calendar style `yyyy.mm.x`), bumped by hand per release and shown in the banner. Releases are built, signed and staged manually; there are no GitHub releases.
- Dependabot (`.github/dependabot.yml`) opens monthly PRs for GitHub Actions and NuGet updates.
- Release builds are staged, signed and zipped by `Build-ZimmermanTools.ps1` in the get-zimmermantools repository (cloned next to this one). Its program table says what goes in this tool's download zip (the files taken from the build, folders copied from the repositories, files kept from the current release zip); a new output file, a content folder or a change in how the tool is built (single file, another framework) needs that entry updated.
- pdbs are embedded in the assemblies (`DebugType embedded`, `EmbedUntrackedSources`) and Release builds set `ContinuousIntegrationBuild` (source paths `/_/...`), both in `Directory.Build.props`: stack traces and Exceptionless reports have file and line numbers, and no .pdb file ships. Don't add `DebugType` (or `DebugSymbols`, `IncludeSymbols`, snupkg) to project files.
