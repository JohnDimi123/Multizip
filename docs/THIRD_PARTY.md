# Third-party components & licensing

Multizip itself is MIT-licensed. It integrates the following open-source components,
each of which keeps its own license. Nothing here is a new compression format —
Multizip deliberately reuses proven libraries.

| Component | License | Used for | Notes |
|-----------|---------|----------|-------|
| [7-Zip](https://www.7-zip.org/) (`7z.dll`, `7z.sfx`) | GNU LGPL v2.1 (with unRAR restriction) | Native engine: broad read/extract + 7z/zip/tar/gz/bz2/xz creation, AES-256, volumes, SFX | Not committed to the repo; supplied at build/package time. |
| [Squid-Box.SevenZipSharp](https://github.com/squid-box/SevenZipSharp) | LGPL v3 | Managed wrapper for `7z.dll` | NuGet. |
| [SharpCompress](https://github.com/adamhathcock/sharpcompress) | MIT | Fully-managed reader/writer fallback | NuGet. |
| [Newtonsoft.Json](https://www.newtonsoft.com/json) | MIT | Settings serialization | NuGet. |

## The RAR restriction

The 7-Zip "unRAR" code carries a restriction: it **must not** be used to create a
RAR-compatible archiver. Multizip honours this — it can **read and extract** RAR
archives but never offers RAR as a creation format. This is enforced in the engine:
`Rar` is absent from every provider's `WritableFormats`.

## LGPL compliance

`7z.dll` and SevenZipSharp are LGPL. Multizip links to them dynamically (the native
DLL is a separate file; the managed wrapper is a separate assembly), and this project
is source-available, so users can replace those components with their own builds. If
you redistribute Multizip, ship the corresponding library notices and keep the
libraries as separate, replaceable files (the installer already does this).

## Attribution in-app

The About dialog lists the active providers, the loaded `7z.dll` path and a summary
of these licenses so end users can see what's running.
