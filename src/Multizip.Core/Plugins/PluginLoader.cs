using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Multizip.Core.Abstractions;
using Multizip.Core.Logging;

namespace Multizip.Core.Plugins
{
    /// <summary>
    /// Discovers and instantiates <see cref="IArchivePlugin"/> implementations from
    /// the Plugins directory. Failures are isolated: one bad DLL never prevents the
    /// rest (or the app) from loading.
    /// </summary>
    public static class PluginLoader
    {
        public static IReadOnlyList<LoadedPlugin> Load(string pluginsDirectory)
        {
            var result = new List<LoadedPlugin>();
            if (string.IsNullOrEmpty(pluginsDirectory) || !Directory.Exists(pluginsDirectory))
                return result;

            foreach (string dll in Directory.GetFiles(pluginsDirectory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    Assembly asm = Assembly.LoadFrom(dll);
                    foreach (Type type in SafeGetTypes(asm))
                    {
                        if (!typeof(IArchivePlugin).IsAssignableFrom(type)) continue;
                        if (type.IsAbstract || type.IsInterface) continue;
                        if (type.GetConstructor(Type.EmptyTypes) == null) continue;

                        var plugin = (IArchivePlugin)Activator.CreateInstance(type);
                        IArchiveProvider provider = plugin.CreateProvider();
                        if (provider == null) continue;

                        result.Add(new LoadedPlugin(plugin, provider, dll));
                        Logger.Info($"Loaded plugin '{plugin.Name}' v{plugin.Version} ({Path.GetFileName(dll)})");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Skipping plugin '{Path.GetFileName(dll)}': {ex.Message}");
                }
            }
            return result;
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
        }
    }

    public sealed class LoadedPlugin
    {
        public IArchivePlugin Plugin { get; }
        public IArchiveProvider Provider { get; }
        public string Path { get; }

        public LoadedPlugin(IArchivePlugin plugin, IArchiveProvider provider, string path)
        {
            Plugin = plugin;
            Provider = provider;
            Path = path;
        }
    }
}
