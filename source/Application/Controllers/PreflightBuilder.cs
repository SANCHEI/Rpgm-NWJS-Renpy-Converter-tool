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
        public bool OutputExists { get; set; }
        public string TopExtensions { get; set; }
        public string LargestInputs { get; set; }
        public string RouteHints { get; set; }
        public string UnityArchivePreview { get; set; }
        public string ExcludedFolders { get; set; }
        public string DetectionConfidence { get; set; }
        public string DetectionNotes { get; set; }
        public string CollectionWarning { get; set; }
    }

    internal static class PreflightBuilder
    {
        public static string Build(PreflightInfo info)
        {
            if (info == null) return "";
            List<string> lines = new List<string>();
            lines.Add("Preflight: " + (info.EngineName ?? "Unknown"));
            lines.Add("Profile: " + (info.ProfileName ?? "Auto: Images + Video"));
            if (!string.IsNullOrWhiteSpace(info.DetectionConfidence))
                lines.Add("Detection: " + info.DetectionConfidence + (string.IsNullOrWhiteSpace(info.DetectionNotes) ? "" : " | " + info.DetectionNotes));
            if (!string.IsNullOrWhiteSpace(info.CollectionWarning))
                lines.Add("Warning: " + info.CollectionWarning);
            lines.Add("Output: " + (info.OutputDir ?? ""));
            lines.Add("Input: " + info.FileCount + " candidate file(s), " + info.ArchiveCount + " archive(s), " + (info.TotalSize ?? "--"));
            lines.Add(info.OutputExists
                ? "Existing output: will ask, then clear this exact folder before extraction."
                : "Existing output: no matching output folder yet; extraction starts without cleanup prompt.");

            if (info.DiagnosticsOnly)
                lines.Add("Action: diagnostics only; extraction will not start.");
            else if (info.UsesPortableRuntime)
                lines.Add("Runtime: built-in portable Python will be prepared silently if needed.");

            if (info.IsRpgMaker)
                lines.Add(info.HasManualKey ? "Key: current HEX field will be used." : "Key: auto-detect/reconstruct will be attempted.");
            if (info.IsUnreal)
                lines.Add("Unreal: AES may be auto-discovered; Oodle uses built-in fallback or local official DLL when available.");
            if (info.IsUnity)
                lines.Add("Unity: " + (info.UnityMode ?? "media") + " profile; skipped objects are usually filtered engine metadata, scripts, shaders or unsupported/protected entries.");
            if (!string.IsNullOrWhiteSpace(info.UnknownExtensions))
                lines.Add("Unknown extensions: " + info.UnknownExtensions);

            return string.Join(Environment.NewLine, lines.ToArray());
        }
    }
}
