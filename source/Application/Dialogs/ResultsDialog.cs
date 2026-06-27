using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace RpgmvpConverterWinForms
{
    internal sealed class ResultsDialog : Form
    {
        public ResultsDialog(OperationResult result, string htmlReportPath, string reportText, bool russian, Action<ApkFollowupAction> runSuggested)
        {
            Text = russian ? "Результаты извлечения" : "Extraction Results";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(620, 440);
            MinimumSize = new Size(620, 440);
            BackColor = Color.FromArgb(17, 19, 24);
            ForeColor = Color.FromArgb(239, 243, 248);
            ApplicationIcon.Apply(this);
            ApkFollowupAction followup = ApkDiagnosticBuilder.TryReadSuggestedFollowup(result.OutputDir);

            Controls.Add(new Label
            {
                Text = result.Errors == 0
                    ? (russian ? "Извлечение завершено" : "Extraction complete")
                    : (russian ? "Извлечение завершено с предупреждениями" : "Extraction complete with warnings"),
                Location = new Point(20, 18),
                Size = new Size(560, 30),
                Font = new Font("Segoe UI Semibold", 14f),
                ForeColor = result.Errors == 0 ? Color.FromArgb(70, 204, 120) : Color.FromArgb(255, 183, 77)
            });

            TextBox summary = new TextBox
            {
                Location = new Point(20, 62),
                Size = new Size(560, 210),
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.FromArgb(10, 12, 16),
                ForeColor = Color.FromArgb(239, 243, 248),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 10f),
                Text = BuildDialogSummary(result, reportText),
                TabStop = false,
                HideSelection = true
            };
            Controls.Add(summary);

            Button openButton = new Button
            {
                Text = russian ? "Открыть результат" : "Open Output Folder",
                Location = new Point(20, 292),
                Size = new Size(145, 34),
                BackColor = Color.FromArgb(68, 197, 255),
                FlatStyle = FlatStyle.Flat
            };
            openButton.Click += delegate
            {
                OpenShellPath(result.OutputDir);
            };
            Controls.Add(openButton);


            Button htmlButton = new Button
            {
                Text = russian ? "HTML отчёт" : "HTML Report",
                Location = new Point(175, 292),
                Size = new Size(125, 34),
                BackColor = Color.FromArgb(45, 50, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = File.Exists(htmlReportPath)
            };
            htmlButton.Click += delegate
            {
                OpenShellPath(htmlReportPath);
            };
            Controls.Add(htmlButton);
            Button galleryButton = new Button
            {
                Text = russian ? "Галерея файлов" : "Results Gallery",
                Location = new Point(310, 292),
                Size = new Size(125, 34),
                BackColor = Color.FromArgb(45, 50, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            galleryButton.Click += delegate
            {
                using (ResultsGalleryForm gallery = new ResultsGalleryForm(result.OutputDir, russian))
                    gallery.ShowDialog(this);
            };
            Controls.Add(galleryButton);

            Button closeButton = new Button
            {
                Text = russian ? "Закрыть" : "Close",
                Location = new Point(480, 334),
                Size = new Size(110, 34),
                BackColor = Color.FromArgb(45, 50, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            closeButton.Click += delegate { Close(); };
            Controls.Add(closeButton);
            AcceptButton = closeButton;
            Shown += delegate
            {
                summary.SelectionStart = 0;
                summary.SelectionLength = 0;
                ActiveControl = closeButton;
            };

            if (followup != null && runSuggested != null)
            {
                Button followupButton = new Button
                {
                    Text = BuildFollowupButtonText(followup, russian),
                    Location = new Point(20, 334),
                    Size = new Size(190, 34),
                    BackColor = Color.FromArgb(70, 204, 120),
                    ForeColor = Color.Black,
                    FlatStyle = FlatStyle.Flat
                };
                followupButton.Click += delegate
                {
                    Close();
                    runSuggested(followup);
                };
                Controls.Add(followupButton);
            }
        }
        private static void OpenShellPath(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && (Directory.Exists(path) || File.Exists(path)))
                    Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch { }
        }
        private static string BuildFollowupButtonText(ApkFollowupAction followup, bool russian)
        {
            string engine = followup == null ? "" : followup.EngineKey;
            if (engine.Equals("Unity", StringComparison.OrdinalIgnoreCase)) return russian ? "Запустить Unity" : "Run Unity extractor";
            if (engine.Equals("Godot", StringComparison.OrdinalIgnoreCase)) return russian ? "Запустить Godot" : "Run Godot extractor";
            if (engine.Equals("GameMaker", StringComparison.OrdinalIgnoreCase)) return russian ? "Запустить GameMaker" : "Run GameMaker extractor";
            if (engine.Equals("Html", StringComparison.OrdinalIgnoreCase)) return russian ? "Запустить HTML/NWJS" : "Run HTML/NWJS extractor";
            if (engine.Equals("JavaJar", StringComparison.OrdinalIgnoreCase)) return russian ? "Запустить Java" : "Run Java extractor";
            return russian ? "Запустить найденный путь" : "Run detected extractor";
        }
        private static string BuildDialogSummary(OperationResult result, string reportText)
        {
            Dictionary<string, string> values = ReadReportValues(reportText, result.ToReport());
            List<string> lines = new List<string>();
            string title = GetValue(values, "__title");
            if (string.IsNullOrWhiteSpace(title)) title = "Game Asset Tool report";

            AddLine(lines, "", title);
            AddLine(lines, "Engine", GetValue(values, "Engine"));
            string extracted = GetValue(values, "Extracted files");
            string size = GetValue(values, "Extracted size");
            if (!string.IsNullOrWhiteSpace(extracted) || !string.IsNullOrWhiteSpace(size))
                lines.Add("Extracted: " + (string.IsNullOrWhiteSpace(extracted) ? "0" : extracted) + " files / " + (string.IsNullOrWhiteSpace(size) ? "0 B" : size));

            string types = CompactFileTypes(GetValue(values, "File types"));
            if (!string.IsNullOrWhiteSpace(types)) lines.Add("Types: " + types);

            string unity = CompactUnityDiagnostics(GetValue(values, "Unity diagnostics"));
            if (!string.IsNullOrWhiteSpace(unity)) lines.Add("Unity: " + unity);
            AddCompactDiagnostic(lines, "DAT", GetValue(values, "DAT diagnostics"));
            AddCompactDiagnostic(lines, "TLG", GetValue(values, "TLG diagnostics"));
            AddCompactDiagnostic(lines, "APK", GetValue(values, "APK diagnostics"));
            string skipped = GetValue(values, "Skipped items");
            string errorsValue = GetValue(values, "Errors");
            AddPositiveLine(lines, "Skipped", skipped);
            AddLine(lines, "Errors", string.IsNullOrWhiteSpace(errorsValue) ? result.Errors.ToString() : errorsValue);
            if (IsPositive(skipped) || IsPositive(errorsValue))
                lines.Add("Details: open HTML Report for full diagnostics");
            AddLine(lines, "Elapsed", GetValue(values, "Elapsed"));
            return string.Join(Environment.NewLine, lines.ToArray());
        }

        private static Dictionary<string, string> ReadReportValues(string reportText, string fallback)
        {
            if (string.IsNullOrWhiteSpace(reportText)) reportText = fallback ?? "";
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in reportText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                string line = raw.TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;
                int index = line.IndexOf(':');
                if (index <= 0)
                {
                    if (!values.ContainsKey("__title") && line.StartsWith("Game Asset Tool", StringComparison.OrdinalIgnoreCase))
                        values["__title"] = line;
                    continue;
                }

                string key = line.Substring(0, index).Trim();
                string value = line.Substring(index + 1).Trim();
                if (!values.ContainsKey(key)) values.Add(key, value);
            }
            return values;
        }

        private static string CompactFileTypes(string value)
        {
            Dictionary<string, string> parts = ParsePipeValues(value);
            List<string> visible = new List<string>();
            AddNonZeroPart(visible, parts, "Images");
            AddNonZeroPart(visible, parts, "SVG");
            AddNonZeroPart(visible, parts, "Text");
            AddNonZeroPart(visible, parts, "Audio");
            AddNonZeroPart(visible, parts, "Video");
            AddNonZeroPart(visible, parts, "Other");
            return string.Join(", ", visible.ToArray());
        }

        private static string CompactUnityDiagnostics(string value)
        {
            Dictionary<string, string> parts = ParsePipeValues(value);
            List<string> visible = new List<string>();
            string archives = GetValue(parts, "archives");
            if (!string.IsNullOrWhiteSpace(archives)) visible.Add(archives + " archives");
            string zero = GetValue(parts, "zero-output");
            if (IsPositive(zero)) visible.Add(zero + " zero-output");
            string input = GetValue(parts, "input");
            if (!string.IsNullOrWhiteSpace(input)) visible.Add(input + " input");
            string errors = GetValue(parts, "error-archives");
            if (IsPositive(errors)) visible.Add(errors + " error archives");
            return string.Join(", ", visible.ToArray());
        }

        private static void AddCompactDiagnostic(List<string> lines, string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            Dictionary<string, string> parts = ParsePipeValues(value);
            List<string> visible = new List<string>();
            foreach (KeyValuePair<string, string> part in parts)
            {
                if (part.Key.Equals("details", StringComparison.OrdinalIgnoreCase)) continue;
                visible.Add(part.Key + " " + part.Value);
                if (visible.Count >= 3) break;
            }
            if (visible.Count > 0) lines.Add(label + ": " + string.Join(", ", visible.ToArray()));
        }

        private static Dictionary<string, string> ParsePipeValues(string value)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(value)) return result;
            foreach (string rawPart in value.Split('|'))
            {
                string part = rawPart.Trim();
                int index = part.IndexOf(':');
                if (index <= 0) continue;
                string key = part.Substring(0, index).Trim();
                string partValue = part.Substring(index + 1).Trim();
                if (!result.ContainsKey(key)) result.Add(key, partValue);
            }
            return result;
        }

        private static void AddLine(List<string> lines, string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            lines.Add(string.IsNullOrWhiteSpace(label) ? value : label + ": " + value);
        }

        private static void AddPositiveLine(List<string> lines, string label, string value)
        {
            if (IsPositive(value)) AddLine(lines, label, value);
        }

        private static void AddNonZeroPart(List<string> visible, Dictionary<string, string> parts, string key)
        {
            string value = GetValue(parts, key);
            if (IsPositive(value)) visible.Add(key + " " + value);
        }

        private static string GetValue(Dictionary<string, string> values, string key)
        {
            string value;
            return values != null && values.TryGetValue(key, out value) ? value : "";
        }

        private static bool IsPositive(string value)
        {
            int number;
            return int.TryParse((value ?? "").Trim(), out number) && number > 0;
        }
    }
}
