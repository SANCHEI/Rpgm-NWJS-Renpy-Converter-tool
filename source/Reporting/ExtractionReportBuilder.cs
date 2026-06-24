using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RpgmvpConverterWinForms
{
    internal static class ExtractionReportBuilder
    {
        private static readonly HashSet<string> KnownReportExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".aac", ".ani", ".asar", ".assets", ".avif", ".avi", ".bin", ".bmp", ".bundle", ".cfg", ".content",
            ".crn", ".css", ".csv", ".dat", ".data", ".dds", ".dll", ".dts", ".exe", ".flac", ".flv", ".gif", ".html", ".htm",
            ".ico", ".ini", ".jar", ".jpeg", ".jpg", ".js", ".json", ".ks", ".ktx", ".ktx2", ".m4a", ".mid",
            ".midi", ".mkv", ".mov", ".mp3", ".mp4", ".nlch", ".ogg", ".otf", ".pak", ".pck",
            ".pgmexport", ".pgmproject", ".png", ".png_", ".po", ".qsp", ".qproj", ".rag", ".res", ".ress",
            ".resource", ".rgss2a", ".rgss3a", ".rgssad", ".rpa", ".rpgmvp", ".rpy", ".rpyc", ".rpym", ".rpymc",
            ".svg", ".swf", ".srk", ".srpgs", ".sspj", ".tga", ".tif", ".tiff", ".tlg", ".tlp", ".tmx", ".ttf",
            ".txt", ".ucas", ".utoc", ".wav", ".webm", ".webp", ".wolf", ".woff", ".woff2", ".wmv", ".xml",
            ".xp3", ".apk", ".astc", ".pkm", ".pvr", ".qoi", ".rb", ".ruby"
        };

        public static string Build(
            string version,
            string engine,
            string outputDir,
            int extracted,
            long bytes,
            int renamed,
            int skipped,
            int errors,
            TimeSpan duration)
        {
            List<string> lines = new List<string>
            {
                "Game Asset Tool v" + version + " report",
                "Engine: " + engine,
                "Extracted files: " + extracted,
                "Extracted size: " + FormatBytes(bytes),
            };

            ExtractionReportSnapshot snapshot = ExtractionReportSnapshot.Capture(outputDir);
            List<FileInfo> files = snapshot.Files;
            AddOptionalLine(lines, "File types", BuildOutputTypeSummary(files));
            AddOptionalLine(lines, "Unknown extensions", BuildUnknownExtensionSummary(files));
            // Keep the standard report compact; Dry Run still exposes largest input files when useful.
            AddOptionalLine(lines, "Duplicate candidates", BuildDuplicateCandidateSummary(files));
            if (string.Equals(engine, "Unity", StringComparison.OrdinalIgnoreCase))
                AddOptionalLine(lines, "Unity diagnostics", BuildUnityDiagnosticsSummary(outputDir));
            AddOptionalLine(lines, "DAT diagnostics", BuildDatDiagnosticsSummary(outputDir));
            AddOptionalLine(lines, "TLG diagnostics", BuildTlgDiagnosticsSummary(outputDir));
            AddOptionalLine(lines, "APK diagnostics", BuildApkDiagnosticsSummary(outputDir));

            lines.Add("Renamed conflicts: " + renamed);
            lines.Add("Skipped items: " + skipped);
            AddOptionalLine(lines, "Skipped diagnostics", BuildSkippedDiagnosticsSummary(outputDir, skipped));
            lines.Add("Errors: " + errors);
            lines.Add("Elapsed: " + FormatDuration(duration.TotalSeconds));
            AddOptionalLine(lines, "Performance", BuildPerformanceSummary(extracted, bytes, duration));
            lines.Add("Output: " + outputDir);
            lines.Add("Created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            return string.Join(Environment.NewLine, lines.ToArray());
        }

        public static string BuildHtml(string textReport)
        {
            return BuildHtml(textReport, null);
        }

        public static string BuildHtml(string textReport, string outputDir)
        {
            List<KeyValuePair<string, string>> rows = new List<KeyValuePair<string, string>>();
            List<KeyValuePair<string, string>> diagnostics = ReadDiagnosticSections(outputDir);
            string title = "Game Asset Tool report";
            foreach (string rawLine in (textReport ?? "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                string line = rawLine.TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;
                int index = line.IndexOf(':');
                if (index > 0)
                {
                    rows.Add(new KeyValuePair<string, string>(line.Substring(0, index), line.Substring(index + 1).Trim()));
                }
                else if (title == "Game Asset Tool report")
                {
                    title = line;
                }
                else
                {
                    rows.Add(new KeyValuePair<string, string>("Note", line));
                }
            }

            string healthSnapshot = BuildHealthSnapshot(outputDir);
            if (!string.IsNullOrWhiteSpace(healthSnapshot))
                diagnostics.Add(new KeyValuePair<string, string>("Health Check", healthSnapshot));

            string skippedSummary = BuildSkippedHtmlSummary(rows, outputDir, diagnostics);
            if (!string.IsNullOrWhiteSpace(skippedSummary))
                diagnostics.Insert(0, new KeyValuePair<string, string>("Skipped summary", skippedSummary));

            StringBuilder html = new StringBuilder();
            html.AppendLine("<!doctype html>");
            html.AppendLine("<html><head><meta charset=\"utf-8\"><title>" + Html(title) + "</title>");
            html.AppendLine("<style>");
            html.AppendLine("body{margin:0;background:#111318;color:#eff3f8;font-family:Segoe UI,Arial,sans-serif}");
            html.AppendLine(".wrap{max-width:1040px;margin:32px auto;padding:24px;background:#191d24;border:1px solid #323a46;border-radius:10px}");
            html.AppendLine("h1{margin:0 0 18px;font-size:24px;color:#44c5ff}.tabs{display:flex;gap:8px;margin:0 0 18px}.tabbtn{background:#2d3440;color:#eff3f8;border:1px solid #465268;border-radius:6px;padding:8px 12px;cursor:pointer}.tabbtn.active{background:#44c5ff;color:#0b1016;border-color:#44c5ff}.tab{display:none}.tab.active{display:block}");
            html.AppendLine("table{width:100%;border-collapse:collapse}th,td{padding:10px 12px;border-bottom:1px solid #303743;vertical-align:top}th{width:220px;text-align:left;color:#9ba7b5;font-weight:600}td{color:#f5f7fb}.value{white-space:pre-wrap;word-break:break-word}");
            html.AppendLine(".ok{color:#46cc78}.warn{color:#ffb74d}.path{font-family:Consolas,monospace;font-size:13px;word-break:break-all}.copy{float:right;margin-left:12px;background:#26303d;color:#dce8f6;border:1px solid #4b5a70;border-radius:5px;padding:4px 8px;cursor:pointer}.copy:hover{border-color:#44c5ff}.diag{margin:0 0 18px}.diag h2{font-size:17px;color:#ffb74d;margin:0 0 8px}.diag pre{white-space:pre-wrap;word-break:break-word;background:#0a0e14;border:1px solid #303743;border-radius:8px;padding:12px;color:#dce8f6;max-height:520px;overflow:auto}.footer{margin-top:18px;color:#9ba7b5;font-size:12px}");
            html.AppendLine("</style><script>function copyText(b){var t=b.getAttribute('data-copy')||'';if(navigator.clipboard){navigator.clipboard.writeText(t);}else{var a=document.createElement('textarea');a.value=t;document.body.appendChild(a);a.select();document.execCommand('copy');document.body.removeChild(a);}}function showTab(id){var tabs=document.querySelectorAll('.tab');for(var i=0;i<tabs.length;i++)tabs[i].classList.remove('active');var buttons=document.querySelectorAll('.tabbtn');for(var j=0;j<buttons.length;j++)buttons[j].classList.remove('active');document.getElementById(id).classList.add('active');document.getElementById('btn-'+id).classList.add('active');}</script>");
            html.AppendLine("</head><body><div class=\"wrap\">");
            html.AppendLine("<h1>" + Html(title) + "</h1>");
            if (diagnostics.Count > 0)
            {
                html.AppendLine("<div class=\"tabs\"><button id=\"btn-summary\" class=\"tabbtn active\" onclick=\"showTab('summary')\">Summary</button><button id=\"btn-diagnostics\" class=\"tabbtn\" onclick=\"showTab('diagnostics')\">Diagnostics</button></div>");
            }
            html.AppendLine("<div id=\"summary\" class=\"tab active\"><table>");
            foreach (KeyValuePair<string, string> row in rows)
            {
                string cls = row.Key.Equals("Errors", StringComparison.OrdinalIgnoreCase) && row.Value != "0" ? "warn" : "";
                if (row.Key.Equals("Output", StringComparison.OrdinalIgnoreCase) || row.Key.Equals("Report", StringComparison.OrdinalIgnoreCase))
                    cls = "path";
                html.AppendLine("<tr><th>" + Html(row.Key) + "</th><td class=\"" + cls + "\"><button class=\"copy\" data-copy=\"" + Html(row.Value) + "\" onclick=\"copyText(this)\">Copy</button><span class=\"value\">" + Html(row.Value) + "</span></td></tr>");
            }
            html.AppendLine("</table></div>");
            if (diagnostics.Count > 0)
            {
                html.AppendLine("<div id=\"diagnostics\" class=\"tab\">");
                foreach (KeyValuePair<string, string> diagnostic in diagnostics)
                {
                    html.AppendLine("<section class=\"diag\"><h2>" + Html(diagnostic.Key) + " <button class=\"copy\" data-copy=\"" + Html(diagnostic.Value) + "\" onclick=\"copyText(this)\">Copy</button></h2><pre>" + Html(diagnostic.Value) + "</pre></section>");
                }
                html.AppendLine("</div>");
            }
            html.AppendLine("<div class=\"footer\">Generated by Game Asset Tool. The HTML report contains the summary, diagnostics, skipped details and health snapshot.</div>");
            html.AppendLine("</div></body></html>");
            return html.ToString();
        }


        private static string BuildSkippedHtmlSummary(List<KeyValuePair<string, string>> rows, string outputDir, List<KeyValuePair<string, string>> diagnostics)
        {
            string skipped = GetRowValue(rows, "Skipped items");
            bool hasSkipped = IsPositiveText(skipped);
            List<string> reasons = new List<string>();
            List<string> types = new List<string>();

            string reportReason = GetRowValue(rows, "Skipped diagnostics");
            if (!string.IsNullOrWhiteSpace(reportReason)) AddDistinct(reasons, reportReason);
            AddExtractionQualityHints(GetRowValue(rows, "Engine"), GetRowValue(rows, "Extracted files"), skipped, reasons);

            foreach (KeyValuePair<string, string> diagnostic in diagnostics)
            {
                string name = diagnostic.Key ?? "";
                string text = diagnostic.Value ?? "";
                if (name.Equals("GameAssetTool-unity-diagnostics.txt", StringComparison.OrdinalIgnoreCase))
                {
                    string unitySkipped = ReadDiagnosticValue(text, "Skipped Unity objects");
                    if (IsPositiveText(unitySkipped))
                    {
                        hasSkipped = true;
                        AddDistinct(types, "Unity non-exported objects: " + unitySkipped);
                        AddDistinct(reasons, "Unity: internal archive entries were filtered by the selected profile, unsupported by the exporter, or came from protected/zero-output archives.");
                    }
                    AddUnityZeroOutputTypes(text, types);
                }
                else if (name.Equals("GameAssetTool-zip-diagnostics.tsv", StringComparison.OrdinalIgnoreCase))
                {
                    AddZipSkippedTypes(text, types, reasons);
                    if (!string.IsNullOrWhiteSpace(text)) hasSkipped = true;
                }
                else if (text.IndexOf("skipped", StringComparison.OrdinalIgnoreCase) >= 0
                    || text.IndexOf("protected", StringComparison.OrdinalIgnoreCase) >= 0
                    || text.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hasSkipped = true;
                    AddDistinct(types, name + ": see diagnostics below");
                }
            }

            if (!hasSkipped && reasons.Count == 0 && types.Count == 0) return "";
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Skipped items: " + (string.IsNullOrWhiteSpace(skipped) ? "0" : skipped));
            builder.AppendLine();
            builder.AppendLine("Likely reasons:");
            if (reasons.Count == 0)
                builder.AppendLine("- Filtered non-media, unsupported archive entries, directory entries, safety limits, or protected/encrypted blocks.");
            else
                foreach (string reason in reasons.Take(8)) builder.AppendLine("- " + reason);
            builder.AppendLine();
            builder.AppendLine("Skipped type candidates:");
            if (types.Count == 0)
                builder.AppendLine("- No per-file skip type data was available for this engine; see engine diagnostics below if present.");
            else
                foreach (string type in types.Take(12)) builder.AppendLine("- " + type);
            return builder.ToString().TrimEnd();
        }

        private static string GetRowValue(List<KeyValuePair<string, string>> rows, string key)
        {
            foreach (KeyValuePair<string, string> row in rows)
                if (row.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) return row.Value;
            return "";
        }

        private static bool IsPositiveText(string value)
        {
            return ParseLeadingInt(value) > 0;
        }

        private static int ParseLeadingInt(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            Match match = Regex.Match(value.Trim(), @"^\d+");
            int number;
            return match.Success && int.TryParse(match.Value, out number) ? number : 0;
        }

        private static void AddExtractionQualityHints(string engine, string extractedText, string skippedText, List<string> reasons)
        {
            int extracted = ParseLeadingInt(extractedText);
            int skipped = ParseLeadingInt(skippedText);
            if (skipped <= 0 && extracted > 0) return;

            if (extracted == 0)
                AddDistinct(reasons, "No files were extracted. Try Dry Run / Scan, verify the selected engine, and check whether the archive is encrypted or protected.");
            else if (skipped > Math.Max(100, extracted * 3))
                AddDistinct(reasons, "Many entries were skipped compared with extracted files. This can be normal for filtered profiles; try Everything or Force engine if the output looks too small.");

            string value = engine ?? "";
            if (value.IndexOf("Unity", StringComparison.OrdinalIgnoreCase) >= 0)
                AddDistinct(reasons, "Unity: media profiles intentionally skip scripts, shaders, materials, meshes, animation clips and internal metadata unless Everything is selected.");
            else if (value.IndexOf("Godot", StringComparison.OrdinalIgnoreCase) >= 0)
                AddDistinct(reasons, "Godot: imported cache files may remain as .ctex/.stex when no embedded image preview can be recovered; encrypted PCKs need a valid key.");
            else if (value.IndexOf("Unreal", StringComparison.OrdinalIgnoreCase) >= 0)
                AddDistinct(reasons, "Unreal: encrypted PAK/IoStore, protected blocks or unsupported compression can be skipped; provide AES keys through the key field or keys.txt when required.");
            else if (value.IndexOf("NWJS", StringComparison.OrdinalIgnoreCase) >= 0)
                AddDistinct(reasons, "NWJS: media profiles skip scripts and data files; use Loose: All if you also need JSON, JS or other loose resources.");
        }

        private static string ReadDiagnosticValue(string text, string key)
        {
            foreach (string raw in (text ?? "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                int index = raw.IndexOf(':');
                if (index <= 0) continue;
                if (raw.Substring(0, index).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                    return raw.Substring(index + 1).Trim();
            }
            return "";
        }

        private static void AddUnityZeroOutputTypes(string text, List<string> types)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in (text ?? "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                string line = raw.Trim();
                if (!line.StartsWith("- ", StringComparison.Ordinal)) continue;
                string path = line.Substring(2).Split('|')[0].Trim().TrimEnd(':');
                int sizeIndex = path.LastIndexOf(" (", StringComparison.Ordinal);
                if (sizeIndex > 0) path = path.Substring(0, sizeIndex);
                string extension = Path.GetExtension(path);
                if (string.IsNullOrWhiteSpace(extension)) extension = "Unity archive";
                if (!counts.ContainsKey(extension)) counts[extension] = 0;
                counts[extension]++;
            }
            foreach (KeyValuePair<string, int> item in counts.OrderByDescending(kv => kv.Value).Take(6))
                AddDistinct(types, "Unity zero-output " + item.Key + ": " + item.Value);
        }

        private static void AddZipSkippedTypes(string text, List<string> types, List<string> reasons)
        {
            Dictionary<string, int> extensionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> reasonCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in (text ?? "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).Skip(1))
            {
                string[] parts = raw.Split('\t');
                if (parts.Length < 3) continue;
                string extension = Path.GetExtension(parts[1]);
                if (string.IsNullOrWhiteSpace(extension)) extension = "<no extension>";
                if (!extensionCounts.ContainsKey(extension)) extensionCounts[extension] = 0;
                extensionCounts[extension]++;
                string reason = parts[2].Trim();
                if (!string.IsNullOrWhiteSpace(reason))
                {
                    if (!reasonCounts.ContainsKey(reason)) reasonCounts[reason] = 0;
                    reasonCounts[reason]++;
                }
            }
            foreach (KeyValuePair<string, int> item in extensionCounts.OrderByDescending(kv => kv.Value).Take(8))
                AddDistinct(types, "ZIP entry " + item.Key + ": " + item.Value);
            foreach (KeyValuePair<string, int> item in reasonCounts.OrderByDescending(kv => kv.Value).Take(5))
                AddDistinct(reasons, item.Key + " x" + item.Value);
        }
        private static string BuildPerformanceSummary(int extracted, long bytes, TimeSpan duration)
        {
            List<string> parts = new List<string>();
            double seconds = Math.Max(0.001D, duration.TotalSeconds);
            if (extracted > 0) parts.Add("files/sec: " + (extracted / seconds).ToString("0.##"));
            if (bytes > 0) parts.Add("data/sec: " + FormatBytes((long)(bytes / seconds)) + "/s");
            try
            {
                using (Process process = Process.GetCurrentProcess())
                {
                    parts.Add("working set: " + FormatBytes(process.WorkingSet64));
                    parts.Add("private memory: " + FormatBytes(process.PrivateMemorySize64));
                }
            }
            catch
            {
            }
            try
            {
                parts.Add("managed heap: " + FormatBytes(GC.GetTotalMemory(false)));
            }
            catch
            {
            }
            return string.Join(" | ", parts.ToArray());
        }
        private static string BuildHealthSnapshot(string outputDir)
        {
            try
            {
                return HealthCheckRunner.RunForReport(outputDir);
            }
            catch (Exception ex)
            {
                return "Health snapshot failed: " + ex.Message;
            }
        }
        private static List<KeyValuePair<string, string>> ReadDiagnosticSections(string outputDir)
        {
            List<KeyValuePair<string, string>> sections = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrWhiteSpace(outputDir) || !Directory.Exists(outputDir)) return sections;
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> paths = new List<string>();
            string[] preferred = new[]
            {
                "GameAssetTool-unity-diagnostics.txt",
                "GameAssetTool-apk-diagnostics.txt",
                "GameAssetTool-zip-diagnostics.tsv",
                "GameAssetTool-wolf-diagnostics.txt"
            };
            foreach (string name in preferred)
            {
                string path = Path.Combine(outputDir, name);
                if (File.Exists(path) && seen.Add(path)) paths.Add(path);
            }
            foreach (string path in Directory.EnumerateFiles(outputDir, "GameAssetTool-*", SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileName(path);
                if (name.IndexOf("diagnostic", StringComparison.OrdinalIgnoreCase) < 0
                    && name.IndexOf("skipped", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (seen.Add(path)) paths.Add(path);
            }
            foreach (string path in paths.Take(8))
            {
                try
                {
                    sections.Add(new KeyValuePair<string, string>(Path.GetFileName(path), File.ReadAllText(path, Encoding.UTF8)));
                }
                catch (Exception ex)
                {
                    sections.Add(new KeyValuePair<string, string>(Path.GetFileName(path), "Could not read diagnostics: " + ex.Message));
                }
            }
            return sections;
        }
        public static string BuildFileExtensionSummary(IEnumerable<string> files, int limit)
        {
            if (files == null) return "";
            List<string> list = files.ToList();
            if (list.Count == 0) return "";
            return string.Join(", ", list
                .GroupBy(delegate(string path)
                {
                    string extension = Path.GetExtension(path);
                    return string.IsNullOrWhiteSpace(extension) ? "<no extension>" : extension.ToLowerInvariant();
                }, StringComparer.OrdinalIgnoreCase)
                .Select(delegate(IGrouping<string, string> group)
                {
                    long bytes = group.Sum(delegate(string path) { return SafeFileLength(path); });
                    return new ExtensionGroup(group.Key, group.Count(), bytes);
                })
                .OrderByDescending(delegate(ExtensionGroup group) { return group.Count; })
                .ThenByDescending(delegate(ExtensionGroup group) { return group.Bytes; })
                .Take(Math.Max(limit, 1))
                .Select(delegate(ExtensionGroup group)
                {
                    return group.Extension + " x" + group.Count + " (" + FormatBytes(group.Bytes) + ")";
                })
                .ToArray());
        }

        public static string BuildLargestFileSummary(string rootPath, IEnumerable<string> files, int limit)
        {
            if (files == null) return "";
            List<FileInfo> infos = files
                .Select(delegate(string path)
                {
                    try { return new FileInfo(path); }
                    catch { return null; }
                })
                .Where(delegate(FileInfo info) { return info != null && info.Exists; })
                .OrderByDescending(delegate(FileInfo info) { return info.Length; })
                .Take(Math.Max(limit, 1))
                .ToList();
            if (infos.Count == 0) return "";
            return string.Join("; ", infos.Select(delegate(FileInfo info)
            {
                return ShortenPath(MakeRelativePathSafe(rootPath, info.FullName), 88) + " (" + FormatBytes(info.Length) + ")";
            }).ToArray());
        }

        private static void AddOptionalLine(List<string> lines, string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                lines.Add(label + ": " + value);
        }

        private static string BuildOutputTypeSummary(List<FileInfo> files)
        {
            if (files == null || files.Count == 0) return "";
            int images = 0;
            int svg = 0;
            int audio = 0;
            int video = 0;
            int other = 0;

            foreach (FileInfo info in files)
            {
                string extension = info.Extension.ToLowerInvariant();
                if (extension == ".svg") svg++;
                else if (MediaTypeRegistry.IsImage(extension)) images++;
                else if (MediaTypeRegistry.IsAudio(extension)) audio++;
                else if (MediaTypeRegistry.IsVideo(extension)) video++;
                else other++;
            }

            return "Images: " + images
                + " | SVG: " + svg
                + " | Audio: " + audio
                + " | Video: " + video
                + " | Other: " + other;
        }

        private static string BuildUnknownExtensionSummary(List<FileInfo> files)
        {
            if (files == null || files.Count == 0) return "";
            List<string> unknown = files
                .Select(delegate(FileInfo info)
                {
                    string extension = info.Extension;
                    return string.IsNullOrWhiteSpace(extension) ? "<no extension>" : extension.ToLowerInvariant();
                })
                .Where(delegate(string extension)
                {
                    return extension.Equals("<no extension>", StringComparison.OrdinalIgnoreCase)
                        || !KnownReportExtensions.Contains(extension);
                })
                .ToList();
            if (unknown.Count == 0) return "";
            return string.Join(", ", unknown
                .GroupBy(delegate(string extension) { return extension; }, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(delegate(IGrouping<string, string> group) { return group.Count(); })
                .ThenBy(delegate(IGrouping<string, string> group) { return group.Key; }, StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .Select(delegate(IGrouping<string, string> group) { return group.Key + " x" + group.Count(); })
                .ToArray());
        }

        private static string BuildLargestFileSummary(string outputDir, List<FileInfo> files, int limit)
        {
            if (files == null || files.Count == 0) return "";
            return string.Join("; ", files
                .OrderByDescending(delegate(FileInfo info) { return info.Length; })
                .Take(Math.Max(limit, 1))
                .Select(delegate(FileInfo info)
                {
                    return ShortenPath(MakeRelativePathSafe(outputDir, info.FullName), 88) + " (" + FormatBytes(info.Length) + ")";
                })
                .ToArray());
        }

        private static string BuildDuplicateCandidateSummary(List<FileInfo> files)
        {
            if (files == null || files.Count == 0) return "";
            string sameNames = string.Join("; ", files
                .GroupBy(delegate(FileInfo info) { return info.Name; }, StringComparer.OrdinalIgnoreCase)
                .Where(delegate(IGrouping<string, FileInfo> group) { return group.Count() > 1; })
                .OrderByDescending(delegate(IGrouping<string, FileInfo> group) { return group.Count(); })
                .ThenBy(delegate(IGrouping<string, FileInfo> group) { return group.Key; }, StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .Select(delegate(IGrouping<string, FileInfo> group) { return group.Key + " x" + group.Count(); })
                .ToArray());

            string sameSizeType = string.Join("; ", files
                .Where(delegate(FileInfo info) { return info.Length >= 4096; })
                .GroupBy(delegate(FileInfo info) { return info.Extension.ToLowerInvariant() + "\t" + info.Length; }, StringComparer.OrdinalIgnoreCase)
                .Where(delegate(IGrouping<string, FileInfo> group) { return group.Count() > 1; })
                .OrderByDescending(delegate(IGrouping<string, FileInfo> group) { return group.Count(); })
                .ThenByDescending(delegate(IGrouping<string, FileInfo> group) { return group.First().Length; })
                .Take(5)
                .Select(delegate(IGrouping<string, FileInfo> group)
                {
                    FileInfo sample = group.First();
                    string extension = string.IsNullOrWhiteSpace(sample.Extension) ? "<no extension>" : sample.Extension.ToLowerInvariant();
                    return extension + " " + FormatBytes(sample.Length) + " x" + group.Count();
                })
                .ToArray());

            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(sameNames)) parts.Add("same names: " + sameNames);
            if (!string.IsNullOrWhiteSpace(sameSizeType)) parts.Add("same size/type: " + sameSizeType);
            return string.Join(" | ", parts.ToArray());
        }

        private static string BuildDatDiagnosticsSummary(string outputDir)
        {
            if (string.IsNullOrWhiteSpace(outputDir) || !Directory.Exists(outputDir)) return "";
            try
            {
                List<string> datFiles = Directory.EnumerateFiles(outputDir, "*.dat", SearchOption.AllDirectories).ToList();
                if (datFiles.Count == 0) return "";
                int spak = 0;
                int openMedia = 0;
                foreach (string file in datFiles)
                {
                    byte[] header = ReadFileHeader(file, 16);
                    if (header.Length >= 4 && header[0] == (byte)'S' && header[1] == (byte)'P' && header[2] == (byte)'A' && header[3] == (byte)'K') spak++;
                    else if (LooksLikeKnownMedia(header)) openMedia++;
                }
                return "DAT files: " + datFiles.Count
                    + " | SPAK-like: " + spak
                    + " | media-like: " + openMedia
                    + " | unknown/protected: " + Math.Max(0, datFiles.Count - spak - openMedia);
            }
            catch
            {
                return "";
            }
        }

        private static string BuildUnityDiagnosticsSummary(string outputDir)
        {
            if (string.IsNullOrWhiteSpace(outputDir)) return "";
            string path = Path.Combine(outputDir, "GameAssetTool-unity-diagnostics.txt");
            if (!File.Exists(path)) return "";
            try
            {
                Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    int index = line.IndexOf(':');
                    if (index <= 0) continue;
                    string key = line.Substring(0, index).Trim();
                    string value = line.Substring(index + 1).Trim();
                    if (!values.ContainsKey(key)) values.Add(key, value);
                }

                List<string> parts = new List<string>();
                AddKeyValue(parts, values, "Archives", "archives");
                AddKeyValue(parts, values, "Bundles", "bundles");
                AddKeyValue(parts, values, "Assets", "assets");
                AddKeyValue(parts, values, "Direct files", "direct");
                AddKeyValue(parts, values, "Archive input size", "input");
                AddKeyValue(parts, values, "Archives with zero output", "zero-output");
                AddKeyValue(parts, values, "Archives with errors", "error-archives");
                if (parts.Count == 0) return "";
                parts.Add("details: HTML diagnostics tab");
                return string.Join(" | ", parts.ToArray());
            }
            catch
            {
                return "";
            }
        }

        private static void AddKeyValue(List<string> parts, Dictionary<string, string> values, string key, string label)
        {
            string value;
            if (values.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value))
                parts.Add(label + ": " + value);
        }

        private static string BuildTlgDiagnosticsSummary(string outputDir)
        {
            if (string.IsNullOrWhiteSpace(outputDir) || !Directory.Exists(outputDir)) return "";
            try
            {
                int tlgFiles = Directory.EnumerateFiles(outputDir, "*.tlg", SearchOption.AllDirectories).Count();
                if (tlgFiles == 0) return "";
                int previewNotes = Directory.EnumerateFiles(outputDir, "*.preview-note.txt", SearchOption.AllDirectories).Count();
                int pngPreviews = Directory.EnumerateFiles(outputDir, "*.png", SearchOption.AllDirectories)
                    .Count(delegate(string path)
                    {
                        string tlg = Path.ChangeExtension(path, ".tlg");
                        return File.Exists(tlg);
                    });
                return "TLG files: " + tlgFiles
                    + " | PNG previews: " + pngPreviews
                    + " | Preview notes: " + previewNotes;
            }
            catch
            {
                return "";
            }
        }

        private static string BuildApkDiagnosticsSummary(string outputDir)
        {
            if (string.IsNullOrWhiteSpace(outputDir)) return "";
            string path = Path.Combine(outputDir, "GameAssetTool-apk-diagnostics.txt");
            if (!File.Exists(path)) return "";
            try
            {
                List<string> detected = new List<string>();
                List<string> routes = new List<string>();
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    if (line.StartsWith("Detected inside:", StringComparison.OrdinalIgnoreCase))
                        AddDistinct(detected, line.Substring("Detected inside:".Length).Trim());
                    else if (line.StartsWith("Suggested route:", StringComparison.OrdinalIgnoreCase))
                        AddDistinct(routes, line.Substring("Suggested route:".Length).Trim());
                }
                List<string> parts = new List<string>();
                if (detected.Count > 0) parts.Add("inside: " + string.Join(", ", detected.ToArray()));
                if (routes.Count > 0) parts.Add("route: " + string.Join("; ", routes.Take(3).ToArray()));
                return string.Join(" | ", parts.ToArray());
            }
            catch
            {
                return "";
            }
        }

        private static string BuildSkippedDiagnosticsSummary(string outputDir, int skipped)
        {
            if (skipped <= 0) return "";
            List<string> details = new List<string>();
            try
            {
                if (!string.IsNullOrWhiteSpace(outputDir) && Directory.Exists(outputDir))
                {
                    bool hasUnityDiagnostics = File.Exists(Path.Combine(outputDir, "GameAssetTool-unity-diagnostics.txt"));
                    string[] diagnosticFiles = Directory.EnumerateFiles(outputDir, "GameAssetTool-*", SearchOption.TopDirectoryOnly)
                        .Select(Path.GetFileName)
                        .Where(delegate(string name)
                        {
                            if (name.Equals("GameAssetTool-unity-diagnostics.txt", StringComparison.OrdinalIgnoreCase)) return false;
                            return name.IndexOf("diagnostic", StringComparison.OrdinalIgnoreCase) >= 0
                                || name.IndexOf("skipped", StringComparison.OrdinalIgnoreCase) >= 0;
                        })
                        .Take(6)
                        .ToArray();
                    if (diagnosticFiles.Length > 0)
                        details.Add("details: " + string.Join(", ", diagnosticFiles));
                    if (hasUnityDiagnostics)
                        details.Add("Unity details: HTML diagnostics tab");
                }
            }
            catch { }

            details.Insert(0, "usually filtered non-media, unsupported archive entries or protected blocks");
            return string.Join(" | ", details.ToArray());
        }

        private static void AddDistinct(List<string> values, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (!values.Contains(value, StringComparer.OrdinalIgnoreCase)) values.Add(value);
        }

        private static byte[] ReadFileHeader(string path, int count)
        {
            try
            {
                byte[] buffer = new byte[count];
                using (FileStream stream = File.OpenRead(path))
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read == buffer.Length) return buffer;
                    byte[] resized = new byte[read];
                    Array.Copy(buffer, resized, read);
                    return resized;
                }
            }
            catch
            {
                return new byte[0];
            }
        }

        private static bool LooksLikeKnownMedia(byte[] header)
        {
            if (header.Length >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4e && header[3] == 0x47) return true;
            if (header.Length >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff) return true;
            if (header.Length >= 4 && header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F') return true;
            if (header.Length >= 4 && header[0] == (byte)'O' && header[1] == (byte)'g' && header[2] == (byte)'g' && header[3] == (byte)'S') return true;
            if (header.Length >= 4 && header[0] == 0x1a && header[1] == 0x45 && header[2] == 0xdf && header[3] == 0xa3) return true;
            return false;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024L * 1024L) return (bytes / 1024d).ToString("N1") + " KB";
            if (bytes < 1024L * 1024L * 1024L) return (bytes / (1024d * 1024d)).ToString("N1") + " MB";
            return (bytes / (1024d * 1024d * 1024d)).ToString("N2") + " GB";
        }

        private static string FormatDuration(double seconds)
        {
            TimeSpan span = TimeSpan.FromSeconds(Math.Max(seconds, 0));
            if (span.TotalHours >= 1) return string.Format("{0:00}:{1:00}:{2:00}", (int)span.TotalHours, span.Minutes, span.Seconds);
            return string.Format("{0:00}:{1:00}", span.Minutes, span.Seconds);
        }

        private static long SafeFileLength(string path)
        {
            try { return new FileInfo(path).Length; }
            catch { return 0; }
        }

        private static string MakeRelativePathSafe(string rootPath, string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rootPath)) return path;
                Uri root = new Uri(AppendDirectorySeparator(Path.GetFullPath(rootPath)));
                Uri file = new Uri(Path.GetFullPath(path));
                return Uri.UnescapeDataString(root.MakeRelativeUri(file).ToString()).Replace('/', Path.DirectorySeparatorChar);
            }
            catch
            {
                return path;
            }
        }

        private static string AppendDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString()) ? path : path + Path.DirectorySeparatorChar;
        }

        private static string ShortenPath(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength) return value;
            return "..." + value.Substring(value.Length - Math.Max(1, maxLength - 3));
        }

        private static string Html(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        private sealed class ExtensionGroup
        {
            public ExtensionGroup(string extension, int count, long bytes)
            {
                Extension = extension;
                Count = count;
                Bytes = bytes;
            }

            public string Extension { get; private set; }
            public int Count { get; private set; }
            public long Bytes { get; private set; }
        }
    }
}
