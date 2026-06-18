using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProjectPinner
{
    /// <summary>Loads the embedded app.ico for use as a WPF window icon (cached, frozen).</summary>
    internal static class AppIcon
    {
        private static ImageSource _cached;
        private static bool _loaded;

        private static string ResourceName()
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("app.ico", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Writes the embedded app.ico to disk so the shell (right-click menu,
        /// shortcut) can reference it as a standalone icon file.</summary>
        public static void WriteIcoToDisk(string path)
        {
            var asm = Assembly.GetExecutingAssembly();
            string name = ResourceName();
            if (name == null) return;
            using (var s = asm.GetManifestResourceStream(name))
            {
                if (s == null) return;
                using (var fs = File.Create(path)) s.CopyTo(fs);
            }
        }

        public static ImageSource Get()
        {
            if (_loaded) return _cached;
            _loaded = true;
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                string name = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("app.ico", StringComparison.OrdinalIgnoreCase));
                if (name == null) return null;
                using (var s = asm.GetManifestResourceStream(name))
                {
                    if (s == null) return null;
                    var frame = BitmapFrame.Create(s, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    frame.Freeze();
                    _cached = frame;
                }
            }
            catch { _cached = null; }
            return _cached;
        }
    }
}
