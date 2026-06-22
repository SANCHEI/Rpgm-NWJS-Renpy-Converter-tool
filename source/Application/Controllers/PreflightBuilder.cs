using System;
using System.Collections.Generic;

namespace RpgmvpConverterWinForms
{
    internal sealed class PreflightInfo
    {
        public string EngineName { get; set; }
        public string ProfileName { get; set; }
        public string OutputDir { get; set; }
        public int FileCount { get; set; }
        public int ArchiveCount { get; set; }
        public string TotalSize { get; set; }
        public string ExistingOutputPolicy { get; set; }
        public bool DiagnosticsOnly { get; set; }
        public bool UsesPortableRuntime { get; set; }
        public bool IsRpgMaker { get; set; }
        public bool HasManualKey { get; set; }
        public bool IsUnreal { get; set; }
        public bool IsUnity { get; set; }
        public string UnityMode { get; set; }
        public string UnknownExtensions { get; set; }
    }

    internal static class PreflightBuilder
    {
        public static string Build(PreflightInfo info)
        {
            if (info == null) return "";
            List<string> lines = new List<string>();
            lines.Add("Preflight: " + (info.EngineName ?? "Unknown"));
            lines.Add("Profile: " + (info.ProfileName ?? "Auto: Images + Video"));
            lines.Add("Output: " + (info.OutputDir ?? ""));
            lines.Add("Input: " + info.FileCount + " candidate file(s), " + info.ArchiveCount + " archive(s), " + (info.TotalSize ?? "--"));
            lines.Add("Existing extracted: ask before delete");

            if (info.DiagnosticsOnly)
                lines.Add("Action: diagnostics only; extraction will not start.");
            else if (info.UsesPortableRuntime)
                lines.Add("Runtime: built-in portable Python will be prepared silently if needed.");

            if (info.IsRpgMaker)
                lines.Add(info.HasManualKey ? "Key: current HEX field will be used." : "Key: auto-detect/reconstruct will be attempted.");
            if (info.IsUnreal)
                lines.Add("Unreal: AES may be auto-discovered; Oodle uses built-in fallback or local official DLL when available.");
            if (info.IsUnity)
                lines.Add("Unity: " + (info.UnityMode ?? "media") + " profile; bundle prompt may appear when .bundle files are found.");
            if (!string.IsNullOrWhiteSpace(info.UnknownExtensions))
                lines.Add("Unknown extensions: " + info.UnknownExtensions);

            return string.Join(Environment.NewLine, lines.ToArray());
        }
    }
}
