using System.Collections.Generic;

namespace Multizip.Core.Abstractions
{
    /// <summary>What to do when a file already exists at the destination.</summary>
    public enum OverwriteMode
    {
        Ask = 0,
        Overwrite,
        Skip,
        RenameNew
    }

    /// <summary>Everything needed to extract an archive (fully or partially).</summary>
    public sealed class ExtractionOptions
    {
        /// <summary>Destination directory.</summary>
        public string Destination { get; set; }

        /// <summary>Password for encrypted archives.</summary>
        public string Password { get; set; }

        public OverwriteMode Overwrite { get; set; } = OverwriteMode.Overwrite;

        /// <summary>Recreate the stored folder hierarchy under the destination.</summary>
        public bool PreservePaths { get; set; } = true;

        /// <summary>
        /// When non-empty, only these entry paths are extracted. When empty, the
        /// whole archive is extracted.
        /// </summary>
        public List<string> SelectedEntries { get; } = new List<string>();
    }
}
