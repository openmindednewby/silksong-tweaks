using System.Collections.Generic;
using System.IO;
using BepInEx;

namespace SilksongTweaks.Ui
{
    /// <summary>
    /// Detects the three mods this one replaces.
    ///
    /// All four behaviours patch the same death and health code paths, so running them alongside
    /// each other produces contradictory results that are miserable to diagnose from a log. It is
    /// far cheaper to detect the situation once at startup than to debug it later.
    /// </summary>
    public static class Conflicts
    {
        private static readonly string[] Known =
        {
            "ReBack.dll",
            "com.blueraja.rosaries_never_permanently_lost.dll",
            "CustomDifficulty",
        };

        public static IReadOnlyList<string> Detect()
        {
            var found = new List<string>();

            try
            {
                var dir = Paths.PluginPath;
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return found;

                foreach (var name in Known)
                {
                    var asFile = Path.Combine(dir, name);
                    if (File.Exists(asFile) || Directory.Exists(asFile))
                    {
                        found.Add(name);
                        continue;
                    }

                    foreach (var hit in Directory.GetFiles(dir, name, SearchOption.AllDirectories))
                    {
                        found.Add(Path.GetFileName(hit));
                        break;
                    }
                }
            }
            catch
            {
                // Detection is a convenience. Never let it break startup.
            }

            return found;
        }
    }
}
