using Multizip.Core.Abstractions;

namespace Multizip.Core.Plugins
{
    /// <summary>
    /// Entry point a plugin assembly exposes. Dropping a DLL that contains a public,
    /// parameterless-constructible implementation of this interface into the
    /// "Plugins" folder makes its provider available to the whole app at startup -
    /// this is how new archive formats are added without recompiling Multizip.
    /// </summary>
    public interface IArchivePlugin
    {
        /// <summary>Display name, e.g. "ACE format support".</summary>
        string Name { get; }

        /// <summary>Plugin version string for the about box.</summary>
        string Version { get; }

        /// <summary>The provider this plugin contributes to the engine.</summary>
        IArchiveProvider CreateProvider();
    }
}
