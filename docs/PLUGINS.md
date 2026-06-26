# Writing a Multizip plugin

Multizip discovers plugins at startup by scanning the `Plugins\` folder next to the
executable for DLLs that contain a public, parameterless `IArchivePlugin`. Each
plugin contributes one `IArchiveProvider` to the engine. This is how you add support
for a new format (say Zstandard or LZ4) without touching the app.

## 1. Create a class library

Target `net48`, reference `Multizip.Core`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Multizip.Core\Multizip.Core.csproj" />
  </ItemGroup>
</Project>
```

## 2. Implement the contracts

```csharp
public sealed class MyPlugin : IArchivePlugin
{
    public string Name => "My format support";
    public string Version => "1.0.0";
    public IArchiveProvider CreateProvider() => new MyProvider();
}

public sealed class MyProvider : IArchiveProvider
{
    public string Name => "My provider";
    public int Priority => 60;            // > 50 to win over the managed provider

    public bool CanRead(ArchiveFormat f)  => f == ArchiveFormat.Zstandard;
    public bool CanWrite(ArchiveFormat f) => f == ArchiveFormat.Zstandard;
    public IEnumerable<ArchiveFormat> WritableFormats => new[] { ArchiveFormat.Zstandard };

    public ArchiveListing List(string path, string password = null) { /* ... */ }
    public void Extract(string path, ExtractionOptions o, IProgress<ProgressInfo> p, CancellationToken t) { /* ... */ }
    public void Create (string path, CompressionOptions o, IProgress<ProgressInfo> p, CancellationToken t) { /* ... */ }
    public bool Test   (string path, string password, IProgress<ProgressInfo> p, CancellationToken t) { /* ... */ }
}
```

### Reporting progress
Call `progress.Report(new ProgressInfo(bytesProcessed, bytesTotal, currentFile, elapsed))`
periodically. The UI derives percentage, speed and ETA from it.

### Respecting cancellation
Check `token.ThrowIfCancellationRequested()` inside your read/write loops.

### Path safety
When extracting, build destinations with
`Multizip.Core.Util.PathUtil.ResolveDestination(dest, entryPath, preservePaths)` so a
malicious archive can't write outside the target folder.

## 3. Deploy

Build, then copy the DLL into `Plugins\` next to `Multizip.exe`. Start Multizip — the
About box lists every loaded provider, and the log records discovery. A bad or
incompatible DLL is skipped with a warning; it can never stop the app from starting.

See `src/Multizip.Plugins.Sample` for a complete, working example (a managed GZip
provider).
