using System;
using System.Collections.Generic;
using System.IO;

namespace RpgmvpConverterWinForms
{
    internal enum MediaAssetKind
    {
        Other,
        Image,
        Vector,
        Text,
        Audio,
        Video
    }

    internal static class MediaTypeRegistry
    {
        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".avif", ".heic", ".ico",
            ".tif", ".tiff", ".tga", ".dds", ".ktx", ".ktx2", ".pvr", ".pkm", ".astc",
            ".basis", ".crn", ".qoi", ".tlg"
        };

        private static readonly HashSet<string> VectorExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".svg"
        };

        private static readonly HashSet<string> TextExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".json", ".xml", ".csv", ".tsv", ".ini", ".cfg", ".conf", ".yaml", ".yml",
            ".po", ".pot", ".mo", ".tmx", ".strings", ".lang", ".loc", ".rpy", ".rpym", ".ks",
            ".js", ".css", ".html", ".htm", ".bytes"
        };

        private static readonly HashSet<string> AudioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".ogg", ".wav", ".flac", ".m4a", ".aac", ".mid", ".midi", ".opus", ".wma"
        };

        private static readonly HashSet<string> VideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".webm", ".nlch", ".avi", ".wmv", ".mov", ".mkv", ".flv", ".m4v", ".ogv", ".mpg", ".mpeg"
        };

        private static readonly HashSet<string> LooseResourceExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".rb", ".ruby",
            ".ttf", ".otf", ".woff", ".woff2",
            ".qsp", ".qproj", ".rpyc", ".rpymc",
            ".rts", ".dts", ".srk", ".srpgs", ".pgmproject", ".pgmexport", ".sspj"
        };

        private static readonly HashSet<string> KnownDryRunExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".aac", ".ani", ".asar", ".assets", ".bin", ".bundle", ".unity3d", ".cfg", ".content",
            ".dat", ".data", ".dll", ".dts", ".exe", ".jar", ".ks", ".pak", ".pck",
            ".png_", ".qsp", ".rag", ".res", ".ress", ".resource", ".rgss2a", ".rgss3a",
            ".rgssad", ".rpa", ".rpgmvp", ".rpy", ".po", ".qproj", ".rpyc", ".rpym",
            ".rpymc", ".srk", ".srpgs", ".swf", ".tmx", ".ucas", ".utoc", ".wolf",
            ".xp3", ".apk", ".pgmproject", ".pgmexport", ".sspj", ".tlp"
        };

        public static bool IsImage(string extension)
        {
            return ImageExtensions.Contains(Normalize(extension));
        }

        public static bool IsVector(string extension)
        {
            return VectorExtensions.Contains(Normalize(extension));
        }

        public static bool IsImageLike(string extension)
        {
            return IsImage(extension) || IsVector(extension);
        }

        public static bool IsText(string extension)
        {
            return TextExtensions.Contains(Normalize(extension));
        }

        public static bool IsAudio(string extension)
        {
            return AudioExtensions.Contains(Normalize(extension));
        }

        public static bool IsVideo(string extension)
        {
            return VideoExtensions.Contains(Normalize(extension));
        }

        public static bool IsMedia(string extension)
        {
            return IsImageLike(extension) || IsAudio(extension) || IsVideo(extension);
        }

        public static bool IsLooseResource(string extension)
        {
            string normalized = Normalize(extension);
            return IsMedia(normalized) || IsText(normalized) || LooseResourceExtensions.Contains(normalized);
        }

        public static bool IsKnownDryRunExtension(string extension)
        {
            string normalized = Normalize(extension);
            return IsMedia(normalized) || IsText(normalized) || KnownDryRunExtensions.Contains(normalized);
        }

        public static bool IsClassicBitmap(string extension)
        {
            string normalized = Normalize(extension);
            return normalized == ".png"
                || normalized == ".jpg"
                || normalized == ".jpeg"
                || normalized == ".bmp"
                || normalized == ".gif"
                || normalized == ".tif"
                || normalized == ".tiff";
        }

        // Скрытые медиа хранятся с расширением вида .png_/.jpg_; восстанавливаем
        // правильное расширение по содержимому, если сигнатура файла совпадает.
        // Возвращает путь к возможному nuevo файлу (File.Move уже выполнен).
        public static string RestoreMediaExtension(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;
            string ext = Path.GetExtension(path);
            if (ext.Length <= 2 || ext[ext.Length - 1] != '_') return path;
            string candidate = ext.Substring(0, ext.Length - 1);
            if (!IsMedia(candidate) && !IsText(candidate) && !IsLooseResource(candidate))
                return path;
            try
            {
                byte[] header = new byte[16];
                using (FileStream stream = File.OpenRead(path))
                {
                    int read = stream.Read(header, 0, header.Length);
                    if (!SignalMatches(header, read, candidate)) return path;
                }
            }
            catch
            {
                return path;
            }
            string renamed = Path.ChangeExtension(path, candidate);
            if (string.Equals(renamed, path, StringComparison.OrdinalIgnoreCase)) return path;
            renamed = ExtractionPathUtils.GetUniqueFilePath(renamed, out bool _);
            try { File.Move(path, renamed); }
            catch { return path; }
            return renamed;
        }

        private static bool SignalMatches(byte[] header, int read, string candidate)
        {
            string c = candidate.ToLowerInvariant();
            if (c == ".png" && read >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) return true;
            if ((c == ".jpg" || c == ".jpeg") && read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return true;
            if (c == ".webp" && read >= 12 && header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F' && header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P') return true;
            if (c == ".ogg" && read >= 4 && header[0] == (byte)'O' && header[1] == (byte)'g' && header[2] == (byte)'g' && header[3] == (byte)'S') return true;
            if (c == ".mp3" && read >= 2 && ((header[0] == 0xFF && (header[1] & 0xE0) == 0xE0) || (header[0] == (byte)'I' && header[1] == (byte)'D' && header[2] == (byte)'3'))) return true;
            if (c == ".flac" && read >= 4 && header[0] == (byte)'f' && header[1] == (byte)'L' && header[2] == (byte)'a' && header[3] == (byte)'C') return true;
            if (c == ".wav" && read >= 12 && header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F' && header[8] == (byte)'W' && header[9] == (byte)'A' && header[10] == (byte)'V' && header[11] == (byte)'E') return true;
            if ((c == ".webm" || c == ".mkv") && read >= 4 && header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3) return true;
            if (c == ".mp4" && read >= 8 && header[4] == (byte)'f' && header[5] == (byte)'t' && header[6] == (byte)'y' && header[7] == (byte)'p') return true;
            if (c == ".gif" && read >= 6 && header[0] == (byte)'G' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'8') return true;
            if (c == ".bmp" && read >= 2 && header[0] == (byte)'B' && header[1] == (byte)'M') return true;
            return false;
        }

        public static MediaAssetKind GetMediaKind(string pathOrExtension)
        {
            string extension = Normalize(pathOrExtension);
            if (IsImage(extension)) return MediaAssetKind.Image;
            if (IsVector(extension)) return MediaAssetKind.Vector;
            if (IsText(extension)) return MediaAssetKind.Text;
            if (IsVideo(extension)) return MediaAssetKind.Video;
            if (IsAudio(extension)) return MediaAssetKind.Audio;
            return MediaAssetKind.Other;
        }

        private static string Normalize(string pathOrExtension)
        {
            if (string.IsNullOrWhiteSpace(pathOrExtension)) return "";
            string value = pathOrExtension.Trim();
            if (value.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) >= 0)
                value = Path.GetExtension(value);
            if (value.Length > 0 && value[0] != '.')
                value = "." + value;
            return value.ToLowerInvariant();
        }
    }
}
