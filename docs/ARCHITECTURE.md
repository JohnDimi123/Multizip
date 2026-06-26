# Architecture

Multizip is split into a UI-free **engine** (`Multizip.Core`) and a thin
**WinForms presentation layer** (`Multizip.App`). Everything the UI does goes
through the engine's small, stable contract, which keeps the app simple and makes
the engine reusable and testable on its own.

```
+-----------------------------------------------------------+
|                     Multizip.App (WinForms)               |
|  MainForm (launcher) · CompressForm · ExtractForm ·       |
|  BrowseForm · ProgressForm · ChecksumForm · BenchmarkForm |
|  SettingsForm · QueueForm · PreviewForm · AboutForm       |
|  ThemeManager · Glyphs · ShellIntegration                 |
+----------------------------+------------------------------+
                             | AppServices (Settings, Engine, Queue, Theme)
                             v
+-----------------------------------------------------------+
|                   Multizip.Core (engine)                  |
|                                                           |
|  FormatDetector ──► ArchiveEngine ──► IArchiveProvider    |
|                          │                ├ SevenZipProvider (7z.dll)   |
|                          │                ├ SharpCompressProvider (managed) |
|                          │                └ <plugins…>     |
|  JobQueue · ChecksumGenerator · CompressionBenchmark      |
|  AppSettings/Profiles/RecentFiles · Logger · UpdateChecker|
+-----------------------------------------------------------+
```

## Key flows

### Opening / listing an archive
1. `FormatDetector.Detect()` reads the first 512 bytes and matches them against a
   priority-ordered signature table (`Formats/FormatSignature.cs`). The file
   extension is only used to disambiguate the ZIP family (jar/apk/war/…).
2. `ArchiveEngine.List()` picks the **highest-priority provider** whose
   `CanRead(format)` is true and asks it for an `ArchiveListing`.
3. `BrowseForm` shows the entries in a **virtual-mode `ListView`** so even huge
   archives load and filter instantly.

### Creating an archive
1. `CompressForm` builds a `CompressionOptions` (format, level, password, volume
   size, threads, SFX, …).
2. `ArchiveEngine.Create()` routes to the highest-priority `CanWrite(format)`
   provider. The native provider handles 7z/zip/tar/gz/bz2/xz; the managed provider
   is the fallback for zip/tar/gz.
3. `ProgressForm` runs the work on a background thread and renders progress from the
   `IProgress<ProgressInfo>` stream.

### Provider selection
Providers declare a `Priority`. The engine sorts them descending and uses the first
match for the requested operation:

| Provider | Priority | Role |
|----------|----------|------|
| `SevenZipProvider` | 100 | native, broadest format coverage |
| `SharpCompressProvider` | 50 | managed fallback / pure-.NET reader |
| plugins | author-defined | extend or override |

If `7z.dll` is missing, `SevenZipProvider.CanRead/CanWrite` return `false`, so the
managed provider transparently takes over for the formats it supports.

## Why these technologies

- **WinForms on .NET Framework 4.8** — native Win32 controls give the authentic
  Windows 7 look (Segoe UI, classic buttons, themed common controls via the app
  manifest), start in well under a second, and use little memory. 4.8 ships in
  Windows 10/11 and installs on Windows 7 SP1, so one build covers the whole range.
- **7-Zip engine** for maximum format coverage (it understands the long tail of
  disk-image and package formats), wrapped via SevenZipSharp.
- **SharpCompress** as a zero-dependency managed fallback so the app is useful even
  with no native DLL present.

## Threading & large archives

- Each operation runs on a background thread; the UI thread only renders progress.
- The providers themselves are multi-threaded (the 7-Zip engine uses all cores via
  the `mt` parameter; thread count is configurable).
- I/O is streamed in 1 MB buffers, never buffering whole files, so 100+ GB archives
  work with flat memory use.
- `JobQueue` serialises *jobs* (not threads) to keep disk thrashing predictable
  under heavy batch workloads.

## Safety

- Extraction paths run through `PathUtil.ResolveDestination`, which strips drive
  letters and `..` segments and verifies the final path stays inside the
  destination — protection against "zip slip" traversal attacks.
- Logging, settings and update checks never throw into the app; failures degrade
  gracefully.

## Extensibility

Implement `IArchivePlugin` in a class library that references `Multizip.Core`, drop
the DLL in the `Plugins\` folder, and the host discovers it at startup. See
[PLUGINS.md](PLUGINS.md) and `src/Multizip.Plugins.Sample`.
