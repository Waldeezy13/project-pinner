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
            _cached = LoadBestFrame();
            return _cached;
        }

        /// <summary>
        /// Loads the icon and returns a frame large enough for a crisp taskbar button.
        /// WPF derives the taskbar (large) HICON by scaling the single ImageSource it is
        /// given, so handing it the 16x16 frame that a bare <see cref="BitmapFrame.Create(System.IO.Stream)"/>
        /// returns leaves the taskbar icon blurry or blank. We decode the whole
        /// multi-resolution .ico and pick the largest frame up to 64px: that downscales
        /// cleanly for the 16px titlebar and stays sharp up to a 200%-DPI 64px taskbar.
        /// </summary>
        private static ImageSource LoadBestFrame()
        {
            var asm = Assembly.GetExecutingAssembly();
            string name = ResourceName();
            if (name == null) return null;
            try
            {
                using (var s = asm.GetManifestResourceStream(name))
                {
                    if (s == null) return null;
                    var decoder = BitmapDecoder.Create(s, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    var within = decoder.Frames.Where(f => f.PixelWidth <= 64).ToList();
                    BitmapFrame best = within.Count > 0
                        ? within.OrderByDescending(f => f.PixelWidth).First()
                        : decoder.Frames.OrderBy(f => f.PixelWidth).First();
                    best.Freeze();
                    return best;
                }
            }
            catch
            {
                // Last resort: a plain single-frame decode still gives the window *an* icon.
                try
                {
                    using (var s = asm.GetManifestResourceStream(name))
                    {
                        if (s == null) return null;
                        var frame = BitmapFrame.Create(s, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                        frame.Freeze();
                        return frame;
                    }
                }
                catch { return null; }
            }
        }
    }
}
