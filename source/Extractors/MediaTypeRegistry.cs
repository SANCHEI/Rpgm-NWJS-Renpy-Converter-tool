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
            ".html", ".htm", ".css", ".js", ".json", ".xml", ".txt", ".csv", ".ini",
            ".po", ".tmx", ".rb", ".ruby",
            ".ttf", ".otf", ".woff", ".woff2",
            ".qsp", ".qproj", ".rpy", ".rpyc", ".rpym", ".rpymc", ".ks",
            ".rts", ".dts", ".srk", ".srpgs", ".pgmproject", ".pgmexport", ".sspj"
        };

        private static readonly HashSet<string> KnownDryRunExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".aac", ".ani", ".asar", ".assets", ".bin", ".bundle", ".cfg", ".content",
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
            return IsMedia(normalized) || LooseResourceExtensions.Contains(normalized);
        }

        public static bool IsKnownDryRunExtension(string extension)
        {
            string normalized = Normalize(extension);
            return IsMedia(normalized) || KnownDryRunExtensions.Contains(normalized);
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

        public static MediaAssetKind GetMediaKind(string pathOrExtension)
        {
            string extension = Normalize(pathOrExtension);
            if (IsImage(extension)) return MediaAssetKind.Image;
            if (IsVector(extension)) return MediaAssetKind.Vector;
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
