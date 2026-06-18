using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace ProjectPinner
{
    /// <summary>
    /// Tiny persisted settings. Uses DataContractJsonSerializer so we depend only on
    /// assemblies that ship with .NET Framework - no extra DLLs next to the exe.
    /// The list of links is NOT stored here: it is derived by enumerating the Links
    /// folder, which keeps the UI self-healing if files are added/removed by hand.
    /// </summary>
    [DataContract]
    internal sealed class Config
    {
        /// <summary>Separator placed between the friendly name and the project number.</summary>
        [DataMember(Name = "separator")]
        public string Separator { get; set; } = " - ";

        /// <summary>Name of the hub folder pinned to Quick Access (holds the shortcuts).</summary>
        [DataMember(Name = "hubFolderName")]
        public string HubFolderName { get; set; } = "Projects";

        /// <summary>If true, pin newly created projects to Quick Access automatically.</summary>
        [DataMember(Name = "autoPin")]
        public bool AutoPin { get; set; } = true;

        /// <summary>If true, mapped network drives (e.g. Z:\) are resolved to their UNC
        /// path (\\server\share) so links survive drive-letter changes.</summary>
        [DataMember(Name = "resolveUncForMappedDrives")]
        public bool ResolveUncForMappedDrives { get; set; } = true;

        // DataContractJsonSerializer creates the object without running constructors or
        // property initializers, so members missing from the JSON would otherwise default
        // to false/null. Re-apply the intended defaults before populating from the file.
        [OnDeserializing]
        private void OnDeserializing(StreamingContext _)
        {
            Separator = " - ";
            HubFolderName = "Projects";
            AutoPin = true;
            ResolveUncForMappedDrives = true;
        }

        public static Config Load()
        {
            try
            {
                if (File.Exists(AppPaths.ConfigPath))
                {
                    using (var fs = File.OpenRead(AppPaths.ConfigPath))
                    {
                        var ser = new DataContractJsonSerializer(typeof(Config));
                        var cfg = (Config)ser.ReadObject(fs);
                        if (cfg != null)
                        {
                            if (string.IsNullOrEmpty(cfg.Separator)) cfg.Separator = " - ";
                            if (string.IsNullOrWhiteSpace(cfg.HubFolderName)) cfg.HubFolderName = "Projects";
                            return cfg;
                        }
                    }
                }
            }
            catch
            {
                // Corrupt/unreadable settings should never block the app.
            }
            return new Config();
        }

        public void Save()
        {
            try
            {
                AppPaths.EnsureDir(AppPaths.InstallRoot);
                var ser = new DataContractJsonSerializer(typeof(Config));
                using (var ms = new MemoryStream())
                {
                    ser.WriteObject(ms, this);
                    File.WriteAllBytes(AppPaths.ConfigPath, ms.ToArray());
                }
            }
            catch
            {
                // Saving settings is best-effort; never crash on it.
            }
        }
    }
}
