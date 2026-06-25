using System;
using System.IO;
using System.Text;

namespace ProjectPinner
{
    internal sealed class SelfTestReport
    {
        public bool Passed;
        public string Summary;
        public string Details;
    }

    /// <summary>
    /// Runs entirely in a local temp sandbox - never touches a network path. Proves the
    /// two things that matter: (1) we can create a working alias shortcut (.lnk), and
    /// (2) removing the shortcut does NOT delete the real folder behind it.
    /// </summary>
    internal static class SelfTest
    {
        public static SelfTestReport Run()
        {
            var log = new StringBuilder();
            string root = Path.Combine(Path.GetTempPath(), "ProjectPinner_SelfTest_" + Guid.NewGuid().ToString("N"));
            string realFolder = Path.Combine(root, "RealProjectFolder");
            string sentinel = Path.Combine(realFolder, "important.txt");
            string lnk = Path.Combine(root, "Friendly Name - 123.lnk");

            try
            {
                Directory.CreateDirectory(realFolder);
                File.WriteAllText(sentinel, "do not lose me");
                log.AppendLine("1. Created a fake project folder with a file inside.");

                ProjectsHubService.WriteShortcut(lnk, realFolder);
                if (!File.Exists(lnk))
                    return Fail(log, "The friendly-named shortcut was not created.", log.ToString());
                log.AppendLine("2. Created a friendly-named shortcut (.lnk) pointing at it.");

                string target = ProjectsHubService.ReadTarget(lnk);
                if (!string.Equals(target, realFolder, StringComparison.OrdinalIgnoreCase))
                    return Fail(log, "The shortcut points at the wrong place: " + target, log.ToString());
                log.AppendLine("3. Confirmed the shortcut points at the real folder.");

                ProjectsHubService.RemoveShortcut(lnk);
                if (File.Exists(lnk))
                    return Fail(log, "The shortcut was not removed.", log.ToString());
                log.AppendLine("4. Removed the shortcut.");

                if (!File.Exists(sentinel) || File.ReadAllText(sentinel) != "do not lose me")
                    return Fail(log, "DANGER: the real file was affected by deleting the shortcut!", log.ToString());
                if (!Directory.Exists(realFolder))
                    return Fail(log, "DANGER: the real folder was removed!", log.ToString());
                log.AppendLine("5. Confirmed the real folder and its file are UNTOUCHED. ✓");

                return new SelfTestReport
                {
                    Passed = true,
                    Summary = "All checks passed — aliases are just links, and removing one is safe.",
                    Details = log.ToString()
                };
            }
            catch (Exception ex)
            {
                log.AppendLine("ERROR: " + ex.Message);
                return Fail(log, "Self-test hit an error.", log.ToString());
            }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
            }
        }

        private static SelfTestReport Fail(StringBuilder log, string summary, string details)
        {
            return new SelfTestReport { Passed = false, Summary = summary, Details = details };
        }
    }
}
