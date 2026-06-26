namespace Multizip.Core.Formats
{
    /// <summary>
    /// The full set of archive and disk-image formats Multizip is aware of.
    /// Detection is done by content signature (see <see cref="FormatDetector"/>);
    /// the extension is only ever used as a tie-breaker.
    /// </summary>
    public enum ArchiveFormat
    {
        Unknown = 0,

        // General purpose archives
        Zip,
        SevenZip,
        Rar,        // read / extract only (licensing)
        Tar,
        Gzip,
        Bzip2,
        Xz,
        Lzma,
        Zstandard,
        Lz4,
        Lzip,
        Cab,
        Arj,
        Lzh,        // LHA / LZH
        Cpio,
        Z,          // classic compress (.Z)

        // Package formats (containers)
        Deb,
        Rpm,
        Msi,        // structured-storage / compound file
        Jar,        // zip family
        War,        // zip family
        Ear,        // zip family
        Apk,        // zip family
        Nupkg,      // zip family

        // Disk / file-system images
        Iso,
        Udf,
        Wim,
        Vhd,
        Vhdx,
        Dmg,
        Qcow2,
        Squashfs,
        Cramfs,

        // Compound / misc that 7-Zip understands
        Chm,
        Nsis,
        Compound    // OLE/CFBF compound document (also covers some MSI)
    }
}
