using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using RpgmvpConverterWinForms;

public static class FixtureTest
{
    public static int Main(string[] args)
    {
        // args: mode(xp3|gm|rpa|rpgm) gameDir outDir
        string mode = args[0];
        string game = args[1];
        string output = args[2];
        Directory.CreateDirectory(output);
        if (mode == "xp3")
        {
            Xp3ExtractionResult total = KirikiriXp3Extractor.ExtractAll(game, output, null, null);
            Console.WriteLine("RESULT:" + total.Extracted + ":" + total.Bytes + ":" + total.Errors + ":" + total.Renamed);
        }
        else if (mode == "gm")
        {
            GameMakerResult total = GameMakerExtractor.ExtractAll(game, output, null, null);
            Console.WriteLine("RESULT:" + total.Extracted + ":" + total.Bytes + ":" + total.Errors + ":" + total.Renamed + ":" + total.Skipped);
        }
        else if (mode == "rpa")
        {
            int files = 0;
            long bytes = 0;
            int errors = 0;
            foreach (string archive in Directory.GetFiles(game, "arch*"))
            {
                try
                {
                    string subfolder = Path.Combine(output, Path.GetFileNameWithoutExtension(archive));
                    Directory.CreateDirectory(subfolder);
                    RpaExtractionResult result = RpaExtractor.ExtractArchive(archive, subfolder, null);
                    files += result.Extracted;
                    bytes += result.Bytes;
                }
                catch (Exception ex)
                {
                    errors++;
                    Console.WriteLine("ERR " + Path.GetFileName(archive) + ": " + ex.Message);
                }
            }
            Console.WriteLine("RESULT:" + files + ":" + bytes + ":" + errors);
        }
        else if (mode == "rpgm")
        {
            string hex = File.ReadAllText(Path.Combine(game, "key.hex.txt")).Trim();
            byte[] key = new byte[hex.Length / 2];
            for (int i = 0; i < key.Length; i++) key[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            int files = 0;
            long bytes = 0;
            int errors = 0;
            foreach (string source in Directory.GetFiles(game, "*.rpgmvp"))
            {
                try
                {
                    byte[] raw = File.ReadAllBytes(source);
                    if (!RpgmCrypt.HasEncryptedHeader(raw) || !RpgmCrypt.ValidateKey(raw, key))
                        throw new InvalidDataException("bad fixture file");
                    byte[] data = RpgmCrypt.Decrypt(raw, key);
                    string ext = RpgmCrypt.DetectImageExtension(data);
                    if (ext == null) throw new InvalidDataException("unknown format");
                    File.WriteAllBytes(Path.Combine(output, Path.GetFileNameWithoutExtension(source) + ext), data);
                    files++;
                    bytes += data.Length;
                }
                catch (Exception ex)
                {
                    errors++;
                    Console.WriteLine("ERR " + Path.GetFileName(source) + ": " + ex.Message);
                }
            }
            Console.WriteLine("RESULT:" + files + ":" + bytes + ":" + errors);
        }
        else if (mode == "godot")
        {
            string hex = File.ReadAllText(Path.Combine(game, "key.hex.txt")).Trim();
            List<string> archives = new List<string>(Directory.GetFiles(game));
            archives.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string archive in archives)
            {
                string lower = archive.ToLowerInvariant();
                if (!(lower.EndsWith(".pck") || lower.EndsWith(".exe"))) continue;
                string subfolder = Path.Combine(output, Path.GetFileNameWithoutExtension(archive));
                Directory.CreateDirectory(subfolder);
                GodotExtractionResult result = GodotExtractor.ExtractAll(archive, subfolder, hex, null, null);
                Console.WriteLine("GODOT:" + Path.GetFileName(archive) + ":" + result.Extracted + ":" + result.Bytes + ":" + result.Errors + ":" + result.PreviewCount);
            }
            foreach (string diagnostics in Directory.GetFiles(output, "*diagnostics*", SearchOption.AllDirectories))
                File.Delete(diagnostics);
        }
        else if (mode == "alice")
        {
            AliceSactResult result = AliceSactExtractor.ExtractAll(game, output, null, null);
            Console.WriteLine("ALICE:" + result.Archives + ":" + result.Files + ":" + result.OpaqueEntries
                + ":" + result.Bytes + ":" + result.Errors);
        }
        else
        {
            Console.WriteLine("Unknown mode: " + mode);
            return 2;
        }
        foreach (string file in SortedFiles(output))
            Console.WriteLine("FILE:" + RelativePath(output, file) + ":" + Sha256(file));
        return 0;
    }

    private static List<string> SortedFiles(string root)
    {
        List<string> files = new List<string>(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }

    private static string RelativePath(string root, string path)
    {
        string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(path);
        if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return full.Substring(prefix.Length);
        return path;
    }

    private static string Sha256(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
}
