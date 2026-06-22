using System;
using System.Diagnostics;
using System.Globalization;

namespace RpgmvpConverterWinForms
{
    internal sealed class UnityExtractionConfig
    {
        private UnityExtractionConfig(string mode, bool includeBundles)
        {
            Mode = mode;
            IncludeBundles = includeBundles;
            ArchiveWorkers = ComputeArchiveWorkers();
            PngCompressionLevel = 1;
            MaxWarnings = 120;
            SaveWorkers = ComputeSaveWorkers();
        }

        public string Mode { get; private set; }
        public bool IncludeBundles { get; private set; }
        public int ArchiveWorkers { get; private set; }
        public int PngCompressionLevel { get; private set; }
        public int MaxWarnings { get; private set; }
        public int SaveWorkers { get; private set; }

        public static UnityExtractionConfig Create(string mode, bool includeBundles)
        {
            return new UnityExtractionConfig(string.IsNullOrWhiteSpace(mode) ? "media" : mode, includeBundles);
        }

        public void ApplyTo(ProcessStartInfo psi, string gamePath, string outputPath)
        {
            psi.EnvironmentVariables["GAME_PATH"] = gamePath;
            psi.EnvironmentVariables["OUTPUT_PATH"] = outputPath;
            psi.EnvironmentVariables["EXTRACT_MODE"] = Mode;
            psi.EnvironmentVariables["INCLUDE_BUNDLES"] = IncludeBundles ? "1" : "0";
            psi.EnvironmentVariables["MAX_WORKERS"] = ArchiveWorkers.ToString(CultureInfo.InvariantCulture);
            psi.EnvironmentVariables["PNG_COMPRESSION_LEVEL"] = PngCompressionLevel.ToString(CultureInfo.InvariantCulture);
            psi.EnvironmentVariables["MAX_WARNINGS"] = MaxWarnings.ToString(CultureInfo.InvariantCulture);
            psi.EnvironmentVariables["SAVE_WORKERS"] = SaveWorkers.ToString(CultureInfo.InvariantCulture);
        }

        private static int ComputeArchiveWorkers()
        {
            int processors = Math.Max(Environment.ProcessorCount, 2);
            return Math.Min(Math.Max(processors - 1, 2), 12);
        }

        private static int ComputeSaveWorkers()
        {
            return Math.Min(Math.Max(Environment.ProcessorCount / 4, 2), 4);
        }
    }
}
