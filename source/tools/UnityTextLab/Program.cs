using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace RpgmvpConverterWinForms
{
    internal static class Program
    {
        private const string ScriptResource = "RpgmvpConverterWinForms.scripts.unity_text_lab.py";

        [STAThread]
        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            if (args.Length < 1)
                return RunGui();

            if (args[0] == "--help" || args[0] == "-h")
            {
                PrintUsage();
                return 0;
            }

            string gamePath = Path.GetFullPath(args[0].Trim('"'));
            if (!Directory.Exists(gamePath))
            {
                Console.Error.WriteLine("Game folder was not found: " + gamePath);
                return 2;
            }

            string outputPath = args.Length >= 2 && !string.IsNullOrWhiteSpace(args[1])
                ? Path.GetFullPath(args[1].Trim('"'))
                : UniqueOutputPath(Path.Combine(gamePath, "extracted", "unity-text-lab"));

            return RunExtraction(gamePath, outputPath);
        }

        private static int RunGui()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose a Unity game folder for text-candidate extraction";
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
                    return 0;

                string gamePath = dialog.SelectedPath;
                string outputPath = UniqueOutputPath(Path.Combine(gamePath, "extracted", "unity-text-lab"));
                int exitCode = RunExtraction(gamePath, outputPath);
                MessageBox.Show(
                    "UnityTextLab finished with exit code " + exitCode + Environment.NewLine + Environment.NewLine + outputPath,
                    "UnityTextLab",
                    MessageBoxButtons.OK,
                    exitCode == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                return exitCode;
            }
        }

        private static int RunExtraction(string gamePath, string outputPath)
        {
            try
            {
                Directory.CreateDirectory(outputPath);
                string scriptPath = PortableRuntime.CreateSessionFilePath("unity_text_lab.py");
                File.WriteAllText(scriptPath, EmbeddedScripts.ReadText(ScriptResource), new UTF8Encoding(false));

                ProcessStartInfo psi = PortableRuntime.CreatePythonProcessInfo();
                psi.Arguments = Quote(scriptPath);
                psi.EnvironmentVariables["GAME_PATH"] = gamePath;
                psi.EnvironmentVariables["OUTPUT_PATH"] = outputPath;
                psi.EnvironmentVariables["EXTRACT_MODE"] = "translation-candidates";
                psi.EnvironmentVariables["INCLUDE_BUNDLES"] = "1";
                psi.EnvironmentVariables["MAX_WORKERS"] = Math.Min(Math.Max(Environment.ProcessorCount - 1, 2), 8).ToString();
                psi.EnvironmentVariables["PNG_COMPRESSION_LEVEL"] = "1";
                psi.EnvironmentVariables["MAX_WARNINGS"] = "80";
                psi.EnvironmentVariables["SAVE_WORKERS"] = "2";

                Console.WriteLine("Unity Text Lab");
                Console.WriteLine("Game: " + gamePath);
                Console.WriteLine("Output: " + outputPath);
                Console.WriteLine();

                int exitCode = RunProcess(psi);
                Console.WriteLine();
                Console.WriteLine("Finished with exit code: " + exitCode);
                Console.WriteLine("Output: " + outputPath);
                return exitCode;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
            finally
            {
                PortableRuntime.Cleanup();
            }
        }

        private static int RunProcess(ProcessStartInfo psi)
        {
            using (Process process = new Process())
            {
                process.StartInfo = psi;
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null) Console.WriteLine(e.Data);
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null) Console.Error.WriteLine(e.Data);
                };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                return process.ExitCode;
            }
        }

        private static string UniqueOutputPath(string basePath)
        {
            if (!Directory.Exists(basePath) && !File.Exists(basePath)) return basePath;
            for (int index = 2; index < 1000; index++)
            {
                string candidate = basePath + "-" + index;
                if (!Directory.Exists(candidate) && !File.Exists(candidate)) return candidate;
            }
            return basePath + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }

        private static void PrintUsage()
        {
            Console.WriteLine("UnityTextLab - experimental Unity text candidate extractor");
            Console.WriteLine("Usage:");
            Console.WriteLine("  UnityTextLab.exe <Unity game folder> [output folder]");
            Console.WriteLine("  UnityTextLab.exe   # opens a small folder-picker GUI");
            Console.WriteLine();
            Console.WriteLine("The output contains only translation-candidates JSON/TSV and Unity diagnostics.");
        }
    }
}