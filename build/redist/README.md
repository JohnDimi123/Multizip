# Native engine redistributables

Drop the 7-Zip native files here before building / packaging to unlock the full
range of formats (cab, iso, wim, rpm, deb, vhd/vhdx, dmg, squashfs, and more) plus
AES-256, multi-volume and self-extracting output:

```
build/redist/7z.dll     <- the 7-Zip library matching your target bitness (x64 recommended)
build/redist/7z.sfx     <- the 7-Zip SFX module (only needed for self-extracting .exe output)
```

Get them from the official 7-Zip distribution (https://www.7-zip.org/). They are
**not** committed to this repository because they ship under the 7-Zip license
(LGPL + unRAR restriction); see `docs/THIRD_PARTY.md`.

If `7z.dll` is absent, Multizip still runs: it automatically falls back to the
fully-managed SharpCompress provider, which handles ZIP, 7z (read), RAR (read),
TAR, GZIP and BZIP2 without any native dependency.

> Bitness must match the app. Multizip builds as AnyCPU and runs 64-bit on 64-bit
> Windows, so use the 64-bit `7z.dll` there. To force 32-bit, set
> `<Prefer32Bit>true</Prefer32Bit>` in `Multizip.App.csproj` and use the 32-bit DLL.
