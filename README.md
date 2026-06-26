# Multizip

**The all-in-one archive manager with a classic Windows look.**

Multizip combines what you'd reach for in 7-Zip, WinRAR and other archive tools
into a single, lightweight program. It creates, opens, browses, extracts and tests
archives in dozens of formats, detects formats automatically from their content
(not just the extension), and wraps it all in a deliberately simple, ImgBurn-style
interface that would feel at home on Windows 7.

> Native Win32 / WinForms. C# on .NET Framework 4.8. Runs on Windows 7 → 11.
> Tiny, fast to start, low memory, fully multi-threaded under the hood.

---

## Highlights

- **Automatic format detection** from magic bytes — rename a `.zip` to `.dat` and
  Multizip still opens it correctly.
- **Create** ZIP, 7z, TAR, GZIP, BZIP2 and XZ, with adjustable compression levels.
- **Extract** essentially everything the bundled engines understand (see below).
- **Browse archives** without extracting — a virtual-mode list stays snappy on
  archives with hundreds of thousands of entries.
- **Fast in-archive search** and **file preview** for text and images.
- **Password protection with AES-256** (7z always; ZIP via AES-256) and optional
  encrypted file names.
- **Multi-volume (split) archives**, **self-extracting `.exe`** output.
- **Integrity testing** and **repair where the format allows it**.
- **Job queue** for batching multiple compress/extract operations.
- **Detailed progress**: percentage, current file, speed, elapsed/remaining time
  and live size readout.
- **Checksum tool** (CRC-32, MD5, SHA-1/256/512) with one-pass multi-hash and
  verify-against-expected.
- **Benchmark** the built-in codecs.
- **Drag & drop**, **recent files**, **favorites**, **custom compression profiles**.
- **Windows Explorer context-menu integration** (installer or per-user).
- **Dark mode** that keeps the flat, classic aesthetic.
- **Portable build** (no install, settings kept beside the exe) and an
  **installer** with shell integration.
- **Plugin architecture** — drop a DLL in `Plugins\` to add new formats.

## Supported formats

| Operation | Formats |
|-----------|---------|
| **Create** | ZIP, 7z, TAR, GZIP, BZIP2, XZ |
| **Read / extract** | ZIP, 7z, RAR\*, TAR, GZIP, BZIP2, XZ, LZMA, CAB, ARJ, LZH/LHA, CPIO, Z, DEB, RPM, MSI, JAR/WAR/EAR/APK/NUPKG, ISO, UDF, WIM, VHD, VHDX, DMG, QCOW2, SquashFS, CramFS, CHM, NSIS |
| **Detected** | All of the above plus Zstandard, LZ4 and LZIP (extraction of these needs a codec plugin) |

\* RAR is **read/extract only** — creating RAR archives is intentionally not
supported for licensing reasons (see `docs/THIRD_PARTY.md`).

Read/extract coverage of the long-tail formats (cab, iso, wim, rpm, vhd, dmg, …)
comes from the native **7-Zip engine** (`7z.dll`). If that DLL isn't present,
Multizip automatically falls back to the fully-managed **SharpCompress** provider,
which still handles ZIP, 7z (read), RAR (read), TAR, GZIP and BZIP2.

## Getting started

### Run the installer
Download / build `Multizip-Setup.exe`, run it, and optionally tick "Add Multizip
to the Windows Explorer right-click menu".

### Or run portable
Grab `Multizip-Portable.zip`, unzip anywhere, run `Multizip.exe`. Settings and logs
stay in a `Data\` folder next to the exe; nothing touches the registry.

## Building from source

You need **Visual Studio 2022** (or the .NET SDK) with **.NET Framework 4.8**
targeting/dev pack. Then:

```powershell
git clone https://github.com/johndimi123/multizip.git
cd multizip
# Build + stage plugin and (optional) native engine:
./build/build.ps1
# Optional portable zip:
./build/make-portable.ps1
```

To enable the full native format range, drop `7z.dll` (and `7z.sfx` for SFX output)
into `build/redist/` first — see `build/redist/README.md`. Full details in
[docs/BUILD.md](docs/BUILD.md).

## How it's put together

```
Multizip.Core      class library  - the engine (no UI)
  Formats/         content-based format detection
  Abstractions/    provider contract, entries, options, progress
  Providers/       7-Zip (native) + SharpCompress (managed) + engine router
  Hashing/         CRC-32 and the streaming checksum generator
  Jobs/            background job queue
  Settings/        settings, profiles, recent files, portable paths
  Plugins/         IArchivePlugin contract + loader
Multizip.App       WinForms app   - the ImgBurn-style UI
Multizip.Plugins.Sample            - reference plugin
```

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the full tour and
[docs/PLUGINS.md](docs/PLUGINS.md) to write your own format provider.

## License

Multizip is released under the [MIT License](LICENSE). It builds on third-party
open-source components that keep their own licenses — see
[docs/THIRD_PARTY.md](docs/THIRD_PARTY.md).
