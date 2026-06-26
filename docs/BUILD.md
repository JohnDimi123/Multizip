# Building Multizip

## Prerequisites

- **Windows** (the app is Win32/WinForms).
- One of:
  - **Visual Studio 2022** with the *.NET desktop development* workload, **or**
  - the **.NET SDK** (6.0+) *plus* the **.NET Framework 4.8 targeting pack**.
- Optional: **Inno Setup 6** to compile the installer.
- Optional: `7z.dll` (+ `7z.sfx`) from <https://www.7-zip.org/> for full native
  format support.

The target framework is **.NET Framework 4.8** (`net48`). That single target runs on
Windows 7 SP1 through Windows 11. The projects are SDK-style, so NuGet packages are
restored automatically on first build.

## NuGet dependencies

Restored automatically (versions pinned in the `.csproj` files):

| Package | Purpose |
|---------|---------|
| `Squid-Box.SevenZipSharp` | managed wrapper around the native 7-Zip engine |
| `SharpCompress` | fully-managed archive reader/writer (fallback) |
| `Newtonsoft.Json` | settings serialization |

> If a restore fails because a pinned version was removed from nuget.org, bump it to
> the nearest available build — the wrapper code only touches long-lived, stable API
> on these packages.

## Build (command line)

From a *Developer PowerShell for VS* (so `dotnet`/`msbuild` are on PATH):

```powershell
./build/build.ps1                      # Release build + stage plugin/native engine
./build/build.ps1 -Configuration Debug # Debug build
```

Or directly:

```powershell
dotnet build Multizip.sln -c Release
```

Output: `src/Multizip.App/bin/Release/net48/Multizip.exe`.

## Native 7-Zip engine (optional but recommended)

1. Download 7-Zip and copy the matching-bitness `7z.dll` (and `7z.sfx` if you want
   self-extracting output) into `build/redist/`.
2. Re-run `build/build.ps1` — it copies them next to `Multizip.exe`.

Without `7z.dll` the app runs on the managed SharpCompress provider (ZIP, 7z read,
RAR read, TAR, GZIP, BZIP2). The status bar shows which engine is active.

## Portable package

```powershell
./build/make-portable.ps1
```

Creates `dist/Multizip-Portable.zip`: the app folder plus an empty `multizip.portable`
marker that makes the app keep settings/logs in a local `Data\` folder.

## Installer

1. Build Release (above).
2. Open `build/Multizip.iss` in Inno Setup 6 and click *Compile* (or
   `iscc build/Multizip.iss`).
3. Result: `build/output/Multizip-Setup.exe`, including the optional Explorer
   context-menu integration task.

## Project layout

```
Multizip.sln
src/
  Multizip.Core/           engine (class library)
  Multizip.App/            WinForms application (WinExe)
  Multizip.Plugins.Sample/ reference plugin
build/
  build.ps1                build + stage
  make-portable.ps1        portable zip
  Multizip.iss             Inno Setup installer
  redist/                  put 7z.dll / 7z.sfx here
docs/
```

## Notes for non-Windows contributors

The engine (`Multizip.Core`) is plain C# and largely portable, but the app targets
WinForms/`net48` and **must be built and run on Windows**. There is no Linux/macOS
GUI build.
