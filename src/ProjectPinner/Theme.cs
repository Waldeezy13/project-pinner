using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace ProjectPinner
{
    internal enum AppTheme { Auto, Light, Dark }

    /// <summary>
    /// Centralised colour palette for both windows. The UI XAML carries @@Token@@ placeholders
    /// (e.g. @@WindowBg@@) which <see cref="Apply"/> replaces with the active palette's hex value
    /// just before XamlReader.Parse — exactly the proven string-substitution pattern already used
    /// for @@PATH@@ in QuickPinWindow. Keeping the palette here means both windows share ONE source
    /// of colours instead of duplicated literals.
    ///
    /// Effective theme = the user's choice (Auto/Light/Dark), except High Contrast always wins and
    /// is honoured by deriving the palette from the user's actual system colours.
    /// </summary>
    internal static class Theme
    {
        public static AppTheme Mode = AppTheme.Auto;

        public static void SetMode(string s) => Mode = Parse(s);

        public static string ModeString => Mode.ToString().ToLowerInvariant();

        public static AppTheme Parse(string s)
        {
            if (string.Equals(s, "light", StringComparison.OrdinalIgnoreCase)) return AppTheme.Light;
            if (string.Equals(s, "dark", StringComparison.OrdinalIgnoreCase)) return AppTheme.Dark;
            return AppTheme.Auto;
        }

        public static bool HighContrast
        {
            get { try { return SystemParameters.HighContrast; } catch { return false; } }
        }

        /// <summary>True when the window chrome (and palette) should read as dark. Drives both the
        /// palette choice and the immersive dark-titlebar attribute, so the two never disagree.</summary>
        public static bool IsDark
        {
            get
            {
                if (HighContrast) return IsSystemColorDark();   // follow the HC scheme's background
                switch (Mode)
                {
                    case AppTheme.Light: return false;
                    case AppTheme.Dark: return true;
                    default: return !OsPrefersLightApps();       // Auto = follow the OS app theme
                }
            }
        }

        /// <summary>Replaces every @@Token@@ in the XAML with the active palette's value.</summary>
        public static string Apply(string xaml)
        {
            var p = CurrentPalette();
            foreach (var kv in p)
                xaml = xaml.Replace("@@" + kv.Key + "@@", kv.Value);
            return xaml;
        }

        /// <summary>Hex value of a single token in the active palette (token name without @@).</summary>
        public static string Get(string token)
        {
            var p = CurrentPalette();
            return p.TryGetValue(token, out var v) ? v : "#000000";
        }

        public static Brush WindowBrush()
        {
            try { return (Brush)new BrushConverter().ConvertFromString(Get("WindowBg")); }
            catch { return Brushes.Black; }
        }

        // ---- palette selection -------------------------------------------------

        private static IDictionary<string, string> CurrentPalette()
        {
            if (HighContrast) return HighContrastPalette();
            return IsDark ? Dark : Light;
        }

        // The full set of tokens used by the XAML. Light and Dark MUST define the same keys
        // (cross-checked at build time). Every value is a brush-valued colour.
        private static readonly Dictionary<string, string> Dark = new Dictionary<string, string>
        {
            { "WindowBg",      "#15171C" },
            { "CardBg",        "#1B1E25" },
            { "PanelBg",       "#171A20" },
            { "InputBg",       "#22262E" },
            { "BtnBg",         "#2B303A" },
            { "BtnDisabledBg", "#262A32" },
            { "ItemHover",     "#20252E" },
            { "ItemSelected",  "#26344A" },
            { "DangerBg",      "#3A2A2E" },
            { "Border",        "#2B303A" },
            { "InputBorder",   "#3A3F4A" },
            { "Accent",        "#4F8CFF" },
            { "AccentText",    "#7FB0FF" },
            { "OnAccent",      "#FFFFFF" },
            { "TextPrimary",   "#E6E8EC" },
            { "TextStrong",    "#C7CCD4" },
            { "TextSecondary", "#9AA0AA" },
            { "TextMuted",     "#A6ADB8" },
            { "TextDisabled",  "#8B919B" },
            { "TextFaint",     "#7A828D" },
            { "DangerFg",      "#FF8A82" },
            { "DangerLink",    "#FF8A82" },
        };

        private static readonly Dictionary<string, string> Light = new Dictionary<string, string>
        {
            { "WindowBg",      "#F4F5F7" },
            { "CardBg",        "#FFFFFF" },
            { "PanelBg",       "#ECEEF1" },
            { "InputBg",       "#FFFFFF" },
            { "BtnBg",         "#E4E7EC" },
            { "BtnDisabledBg", "#EDEFF2" },
            { "ItemHover",     "#EAEEF5" },
            { "ItemSelected",  "#D6E4FB" },
            { "DangerBg",      "#FBE9E7" },
            { "Border",        "#D9DCE1" },
            { "InputBorder",   "#C2C7CF" },
            { "Accent",        "#2D6CDF" },
            { "AccentText",    "#2456B8" },
            { "OnAccent",      "#FFFFFF" },
            { "TextPrimary",   "#1B1E25" },
            { "TextStrong",    "#3A3F47" },
            { "TextSecondary", "#5B626D" },
            { "TextMuted",     "#5F6670" },
            { "TextDisabled",  "#9AA0AA" },
            { "TextFaint",     "#8A919B" },
            { "DangerFg",      "#C0392B" },
            { "DangerLink",    "#C0392B" },
        };

        /// <summary>
        /// Under High Contrast we ignore the brand palette and map every token to the user's actual
        /// system colours, so the app respects whatever HC scheme they run. Secondary/muted text all
        /// collapses to the window-text colour (HC trades visual hierarchy for guaranteed contrast).
        /// </summary>
        private static Dictionary<string, string> HighContrastPalette()
        {
            string win = Hex(SystemColors.WindowColor);
            string ctl = Hex(SystemColors.ControlColor);
            string txt = Hex(SystemColors.WindowTextColor);
            string ctlText = Hex(SystemColors.ControlTextColor);
            string hi = Hex(SystemColors.HighlightColor);
            string hiText = Hex(SystemColors.HighlightTextColor);
            string gray = Hex(SystemColors.GrayTextColor);
            string border = Hex(SystemColors.ActiveBorderColor);

            return new Dictionary<string, string>
            {
                { "WindowBg",      win },
                { "CardBg",        win },
                { "PanelBg",       ctl },
                { "InputBg",       win },
                { "BtnBg",         ctl },
                { "BtnDisabledBg", ctl },
                { "ItemHover",     ctl },
                { "ItemSelected",  hi },
                { "DangerBg",      ctl },
                { "Border",        border },
                { "InputBorder",   border },
                { "Accent",        hi },
                { "AccentText",    txt },
                { "OnAccent",      hiText },
                { "TextPrimary",   txt },
                { "TextStrong",    txt },
                { "TextSecondary", txt },
                { "TextMuted",     txt },
                { "TextDisabled",  gray },
                { "TextFaint",     txt },
                { "DangerFg",      ctlText },
                { "DangerLink",    txt },
            };
        }

        // ---- OS detection ------------------------------------------------------

        /// <summary>Reads HKCU Personalize\AppsUseLightTheme (1 = light apps, 0 = dark apps).
        /// Defaults to dark (our brand look) when the value is missing/unreadable.</summary>
        private static bool OsPrefersLightApps()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var v = key?.GetValue("AppsUseLightTheme");
                    if (v is int i) return i != 0;
                }
            }
            catch { }
            return false; // unknown -> dark
        }

        private static bool IsSystemColorDark()
        {
            try
            {
                var c = SystemColors.WindowColor;
                // Rec. 601 luma; < 0.5 means a dark background.
                double luma = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
                return luma < 0.5;
            }
            catch { return true; }
        }

        private static string Hex(Color c) =>
            "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
    }
}
