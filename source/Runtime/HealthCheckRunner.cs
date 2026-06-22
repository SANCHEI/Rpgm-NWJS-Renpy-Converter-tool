using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace RpgmvpConverterWinForms
{
    internal static class HealthCheckRunner
    {
        private static readonly string[] RequiredResources =
        {
            "RpgmvpConverterWinForms.runtime.runtime-win-x64.zip",
            "RpgmvpConverterWinForms.tools.UberWolfCli.exe",
            "RpgmvpConverterWinForms.tools.resvg.exe",
            "RpgmvpConverterWinForms.tools.SW_Decensor_v0.7.4.2.zip",
            "RpgmvpConverterWinForms.scripts.extract_unity.py",
            "RpgmvpConverterWinForms.scripts.extract_godot.py",
            "RpgmvpConverterWinForms.scripts.extract_xp3.py",
            "RpgmvpConverterWinForms.scripts.extract_unreal.py",
            "RpgmvpConverterWinForms.scripts.extract_gamemaker.py",
            "RpgmvpConverterWinForms.scripts.extract_spite.py"
        };

        public static string Run()
        {
            List<string> lines = new List<string>();
            lines.Add("Game Asset Tool Health Check");
            lines.Add("Created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            lines.Add("OS: " + Environment.OSVersion);
            lines.Add(".NET: " + Environment.Version);
            lines.Add("");

            CheckFolder(lines, "LocalAppData", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            CheckFolder(lines, "Temp", Path.GetTempPath());
            CheckEmbeddedResources(lines);
            CheckPortableRuntime(lines);
            CheckEmbeddedTools(lines);

            lines.Add("");
            lines.Add("Result: " + (HasFailure(lines) ? "warnings/errors found" : "OK"));
            return string.Join(Environment.NewLine, lines.ToArray());
        }


        public static string RunForReport(string outputDir)
        {
            List<string> lines = new List<string>();
            lines.Add("Game Asset Tool Health Snapshot");
            lines.Add("Created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            lines.Add("OS: " + Environment.OSVersion);
            lines.Add(".NET: " + Environment.Version);
            lines.Add("");

            CheckFolder(lines, "LocalAppData", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            CheckFolder(lines, "Temp", Path.GetTempPath());
            if (!string.IsNullOrWhiteSpace(outputDir))
                CheckFolder(lines, "Output", outputDir);
            CheckEmbeddedResources(lines);
            lines.Add("INFO Python runtime: " + (PortableRuntime.IsReady ? "already extracted for this session" : "not extracted at report time"));
            lines.Add("INFO Tool process preflight: skipped in HTML report to avoid slowing extraction; use the Health button for the full live check.");

            lines.Add("");
            lines.Add("Result: " + (HasFailure(lines) ? "warnings/errors found" : "OK"));
            return string.Join(Environment.NewLine, lines.ToArray());
        }
        private static void CheckFolder(List<string> lines, string label, string folder)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folder))
                    throw new InvalidOperationException("path is empty");
                Directory.CreateDirectory(folder);
                string probe = Path.Combine(folder, "GameAssetTool-health-" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(probe, "ok", Encoding.UTF8);
                File.Delete(probe);
                lines.Add("OK  " + label + ": writable (" + folder + ")");
            }
            catch (Exception ex)
            {
                lines.Add("ERR " + label + ": " + ex.Message);
            }
        }

        private static void CheckEmbeddedResources(List<string> lines)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            foreach (string name in RequiredResources)
            {
                try
                {
                    using (Stream stream = assembly.GetManifestResourceStream(name))
                    {
                        if (stream == null)
                            lines.Add("ERR resource missing: " + name);
                        else
                            lines.Add("OK  resource: " + name + " (" + stream.Length + " bytes)");
                    }
                }
                catch (Exception ex)
                {
                    lines.Add("ERR resource " + name + ": " + ex.Message);
                }
            }
        }

        private static void CheckPortableRuntime(List<string> lines)
        {
            try
            {
                string python = PortableRuntime.EnsureExtracted();
                lines.Add("OK  Python runtime extracted: " + python);
                string version = RunProcess(python, "--version", Path.GetDirectoryName(python), 8000);
                lines.Add("OK  Python preflight: " + version);
            }
            catch (Exception ex)
            {
                lines.Add("ERR Python runtime: " + ex.Message);
            }
        }

        private static void CheckEmbeddedTools(List<string> lines)
        {
            try
            {
                string wolf = ToolRuntime.EnsureWolfCliExtracted();
                lines.Add("OK  UberWolf CLI extracted: " + wolf);
            }
            catch (Exception ex)
            {
                lines.Add("ERR UberWolf CLI: " + ex.Message);
            }

            try
            {
                string resvg = ToolRuntime.EnsureResvgExtracted();
                lines.Add("OK  resvg extracted: " + resvg);
                string version = RunProcess(resvg, "--version", Path.GetDirectoryName(resvg), 8000);
                lines.Add("OK  resvg preflight: " + version);
            }
            catch (Exception ex)
            {
                lines.Add("WARN resvg: " + ex.Message);
            }
        }

        private static string RunProcess(string fileName, string arguments, string workingDirectory, int timeoutMs)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Environment.CurrentDirectory : workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using (Process process = Process.Start(psi))
            {
                if (process == null)
                    throw new InvalidOperationException("process did not start");
                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(); }
                    catch { }
                    throw new TimeoutException(Path.GetFileName(fileName) + " did not finish within " + (timeoutMs / 1000) + " seconds.");
                }

                string output = (process.StandardOutput.ReadToEnd() + " " + process.StandardError.ReadToEnd()).Trim();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException(Path.GetFileName(fileName) + " exited with code " + process.ExitCode + ": " + output);
                return string.IsNullOrWhiteSpace(output) ? "exit code 0" : output;
            }
        }

        private static bool HasFailure(IEnumerable<string> lines)
        {
            foreach (string line in lines)
            {
                if (line.StartsWith("ERR ", StringComparison.Ordinal) || line.StartsWith("WARN ", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
