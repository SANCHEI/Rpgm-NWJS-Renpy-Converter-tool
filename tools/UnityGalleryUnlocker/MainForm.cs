using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace UnityGalleryUnlocker
{
    public partial class MainForm : Form
    {
        private TextBox pathBox;
        private Button browseBtn;
        private Button unlockBtn;
        private Button clearSaveBtn;
        private Button extractGalleriesBtn;
        private Button patchGameBtn;
        private TextBox logBox;

        public MainForm()
        {
            Text = "Unity Gallery Unlocker v1.1";
            Width = 750;
            Height = 550;
            StartPosition = FormStartPosition.CenterScreen;

            var pathLabel = new Label { Text = "Game Folder:", Left = 20, Top = 20 };
            pathBox = new TextBox { Left = 120, Top = 18, Width = 450, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            browseBtn = new Button { Text = "Browse...", Left = 580, Top = 16, Width = 120 };
            browseBtn.Click += Browse_Click;

            var btnTop = 60;
            unlockBtn = new Button { Text = "Unlock Gallery", Left = 20, Top = btnTop, Width = 150, Height = 35 };
            unlockBtn.Click += Unlock_Click;

            clearSaveBtn = new Button { Text = "Clear Save Data", Left = 180, Top = btnTop, Width = 150, Height = 35 };
            clearSaveBtn.Click += ClearSave_Click;

            extractGalleriesBtn = new Button { Text = "Extract Galleries", Left = 340, Top = btnTop, Width = 150, Height = 35 };
            extractGalleriesBtn.Click += ExtractGalleries_Click;

            patchGameBtn = new Button { Text = "Patch Game Code", Left = 500, Top = btnTop, Width = 150, Height = 35 };
            patchGameBtn.Click += PatchGame_Click;

            logBox = new TextBox {
                Left = 20, Top = 110, Width = 690, Height = 390,
                Multiline = true, ScrollBars = ScrollBars.Vertical,
                Font = new System.Drawing.Font("Consolas", 9),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            Controls.Add(pathLabel);
            Controls.Add(pathBox);
            Controls.Add(browseBtn);
            Controls.Add(unlockBtn);
            Controls.Add(clearSaveBtn);
            Controls.Add(extractGalleriesBtn);
            Controls.Add(patchGameBtn);
            Controls.Add(logBox);

            pathBox.Text = @"D:\!!downloads\!!browser\JNZ Anchor Archetype";
        }

        private void Browse_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                    pathBox.Text = dialog.SelectedPath;
            }
        }

        private void WriteLog(string msg)
        {
            logBox.AppendText(msg + "\r\n");
            logBox.SelectionStart = logBox.Text.Length;
            logBox.ScrollToCaret();
        }

        private void Unlock_Click(object sender, EventArgs e)
        {
            string gamePath = pathBox.Text.Trim();
            if (!Directory.Exists(gamePath))
            {
                MessageBox.Show("Game folder not found!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            WriteLog("=== Unity Gallery Unlocker v1.1 ===");
            WriteLog("Game: " + Path.GetFileName(gamePath));
            WriteLog("");

            string dataFolder = Path.Combine(gamePath, "jnz_Data");
            if (!Directory.Exists(dataFolder))
            {
                string dataDir = Path.Combine(gamePath, "_Data");
                if (Directory.Exists(dataDir))
                    dataFolder = dataDir;
            }

            if (!Directory.Exists(dataFolder))
            {
                WriteLog("ERROR: Game data folder not found");
                return;
            }

            WriteLog("Data folder: " + dataFolder);
            WriteLog("");

            // Find Unity PlayerPrefs locations
            string gameName = Path.GetFileName(gamePath);
            string companyName = GetCompanyName(dataFolder);
            if (string.IsNullOrEmpty(companyName))
                companyName = "Forbidden Dreams"; // Common default

            WriteLog("Searching for PlayerPrefs...");

            var prefsLocations = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), companyName, gameName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), companyName, gameName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), companyName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), gameName),
            };

            foreach (var loc in prefsLocations.Distinct())
            {
                if (Directory.Exists(loc))
                {
                    WriteLog("  Found: " + loc);
                    var files = Directory.GetFiles(loc, "*", SearchOption.AllDirectories);
                    WriteLog("    Contains " + files.Length + " files:");
                    foreach (var f in files.Take(5))
                        WriteLog("      - " + Path.GetFileName(f));
                    if (files.Length > 5)
                        WriteLog("      ... and " + (files.Length - 5) + " more");
                }
            }

            // Find save files in game folder
            WriteLog("");
            WriteLog("Searching for Save Files in game folder...");

            var saveFiles = new List<string>();
            string[] patterns = { "*.sav", "*.save", "*.dat", "*.bin", "*save*", "*slot*" };
            foreach (var p in patterns)
                saveFiles.AddRange(Directory.GetFiles(gamePath, p, SearchOption.AllDirectories));

            foreach (var f in saveFiles.Distinct().Take(20))
                WriteLog("  - " + Path.GetFileName(f) + " (" + new FileInfo(f).Length + " bytes)");

            // Check AppData/LocalLow
            string localLow = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string gameFolderName = Path.GetFileName(gamePath);
            var localLowSave = Path.Combine(localLow, companyName, gameFolderName);
            if (Directory.Exists(localLowSave))
            {
                WriteLog("");
                WriteLog("AppData/LocalLow save folder: " + localLowSave);
                foreach (var f in Directory.GetFiles(localLowSave))
                    WriteLog("  - " + Path.GetFileName(f));
            }

            WriteLog("");
            WriteLog("=== Gallery Unlock Options ===");
            WriteLog("1. Clear Save Data - Delete save files to reset progress");
            WriteLog("2. Patch Game Code - Attempt to bypass gallery unlock checks");
            WriteLog("3. Modify PlayerPrefs - If game stores unlock state in prefs");
            WriteLog("");

            // Analyze game structure
            WriteLog("=== Game Analysis ===");
            var assets = Directory.GetFiles(dataFolder, "*.assets");
            WriteLog("Assets files: " + assets.Length);

            var bundles = Directory.GetFiles(dataFolder, "*.bundle", SearchOption.AllDirectories);
            WriteLog("Bundle files: " + bundles.Length);

            // Check if using Addressables
            string streamingAssets = Path.Combine(dataFolder, "StreamingAssets", "aa");
            if (Directory.Exists(streamingAssets))
            {
                WriteLog("Uses Addressables: Yes (catalog found)");
                WriteLog("  -> Gallery content is stored as Addressable assets");
            }
        }

        private string GetCompanyName(string dataFolder)
        {
            // Try to find company name from app.info
            string appInfoPath = Path.Combine(dataFolder, "app.info");
            if (File.Exists(appInfoPath))
            {
                var lines = File.ReadAllLines(appInfoPath);
                if (lines.Length > 0)
                    return lines[0].Trim();
            }
            return "";
        }

        private void ClearSave_Click(object sender, EventArgs e)
        {
            string gamePath = pathBox.Text.Trim();
            if (!Directory.Exists(gamePath))
            {
                MessageBox.Show("Game folder not found!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var result = MessageBox.Show(
                "This will delete all save files and PlayerPrefs.\n\nGame progress will be reset!\n\nContinue?",
                "Clear Save Data",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            WriteLog("Clearing save data...");

            int deleted = 0;

            // Delete save files in game folder
            string[] patterns = { "*.sav", "*.save", "*.dat", "*.bin" };
            foreach (var pattern in patterns)
            {
                var files = Directory.GetFiles(gamePath, pattern, SearchOption.AllDirectories);
                foreach (var f in files)
                {
                    try { File.Delete(f); deleted++; WriteLog("Deleted: " + f); }
                    catch { }
                }
            }

            // Delete AppData/LocalLow save files
            string companyName = "Forbidden Dreams";
            string gameName = "jnz";
            string localLowSave = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), companyName, gameName);
            if (Directory.Exists(localLowSave))
            {
                foreach (var f in Directory.GetFiles(localLowSave))
                {
                    try { File.Delete(f); WriteLog("Deleted: " + f); deleted++; }
                    catch { }
                }
            }

            // Delete PlayerPrefs
            string[] prefsLocations = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), companyName, gameName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), companyName),
            };

            foreach (var loc in prefsLocations)
            {
                if (Directory.Exists(loc))
                {
                    try
                    {
                        Directory.Delete(loc, true);
                        WriteLog("Deleted PlayerPrefs: " + loc);
                    }
                    catch { }
                }
            }

            WriteLog("Total deleted: " + deleted + " files");
            WriteLog("Save data cleared. Game will start fresh on next run.");
        }

        private void ExtractGalleries_Click(object sender, EventArgs e)
        {
            string gamePath = pathBox.Text.Trim();

            WriteLog("=== Gallery Extraction ===");
            WriteLog("This will extract all textures/videos from game files.");
            WriteLog("");
            WriteLog("Please use the main GameAssetTool.exe instead:");
            WriteLog("  1. Run GameAssetTool.exe");
            WriteLog("  2. Select game folder: " + gamePath);
            WriteLog("  3. Use 'Extract' button for Unity extraction");
            WriteLog("  4. Check extracted/ folder for gallery images");
            WriteLog("");
            WriteLog("Looking for gallery paths in game data...");

            string dataFolder = Path.Combine(gamePath, "jnz_Data");
            if (!Directory.Exists(dataFolder))
                dataFolder = Path.Combine(gamePath, "_Data");

            if (File.Exists(Path.Combine(dataFolder, "resources.assets")))
            {
                WriteLog("Found resources.assets - checking for gallery content...");
                WriteLog("  Use Unity Extractor to scan for images");
            }
        }

        private void PatchGame_Click(object sender, EventArgs e)
        {
            string gamePath = pathBox.Text.Trim();

            WriteLog("=== Game Code Patching ===");
            WriteLog("Attempting to patch gallery unlock checks...");
            WriteLog("");

            // Unity games store code in:
            // - GameAssembly.dll (IL2CPP)
            // - Assembly-CSharp.dll (if not IL2CPP)

            string dataFolder = Path.Combine(gamePath, "jnz_Data");
            string gameAsm = Path.Combine(dataFolder, "GameAssembly.dll");

            if (File.Exists(gameAsm))
            {
                WriteLog("Found GameAssembly.dll (IL2CPP game)");
                WriteLog("  Size: " + (new FileInfo(gameAsm).Length / 1024 / 1024) + " MB");
                WriteLog("");
                WriteLog("This game uses IL2CPP - direct patching is complex.");
                WriteLog("  Options:");
                WriteLog("  1. Use a hex editor to search for unlock checks");
                WriteLog("  2. Use il2cppdumper to get method names");
                WriteLog("  3. Try using Cheat Engine to find unlock variables");
                WriteLog("");
                WriteLog("Common unlock check patterns in Unity games:");
                WriteLog("  - 'GalleryUnlocked' / 'IsGalleryUnlocked'");
                WriteLog("  - 'UnlockAllGalleries' / 'gallery_unlocked'");
                WriteLog("  - Save file check: read 'gallery' as 0/1");
                WriteLog("");

                // Try to find unlock-related strings in the DLL
                try
                {
                    var dllData = File.ReadAllBytes(gameAsm);
                    var searchTerms = new[] { "Gallery", "gallery", "Unlock", "unlock", "CG", "cg" };

                    WriteLog("Searching GameAssembly.dll for unlock patterns...");
                    foreach (var term in searchTerms)
                    {
                        var termBytes = Encoding.ASCII.GetBytes(term);
                        int count = 0;
                        for (int i = 0; i < dllData.Length - termBytes.Length; i++)
                        {
                            if (dllData[i] == termBytes[0])
                            {
                                bool match = true;
                                for (int j = 1; j < termBytes.Length; j++)
                                {
                                    if (dllData[i+j] != termBytes[j]) { match = false; break; }
                                }
                                if (match) count++;
                            }
                        }
                        if (count > 0)
                            WriteLog("  '" + term + "': found " + count + " times");
                    }
                }
                catch (Exception ex)
                {
                    WriteLog("Error reading DLL: " + ex.Message);
                }
            }
            else
            {
                WriteLog("GameAssembly.dll not found - trying Assembly-CSharp...");
                string asmCSharp = Path.Combine(dataFolder, "Managed", "Assembly-CSharp.dll");
                if (File.Exists(asmCSharp))
                {
                    WriteLog("Found Assembly-CSharp.dll");
                    WriteLog("  This is a non-IL2CPP game, easier to patch");
                }
                else
                {
                    WriteLog("No game DLL found");
                }
            }

            WriteLog("");
            WriteLog("For advanced patching, consider:");
            WriteLog("  - dnSpy/ILSpy to decompile and modify");
            WriteLog("  - Cheat Engine to find runtime values");
            WriteLog("  - Unity assembly to edit save data directly");
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}