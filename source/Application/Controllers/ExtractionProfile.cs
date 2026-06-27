using System;

namespace RpgmvpConverterWinForms
{
    internal enum ExtractionProfileKind
    {
        AutoImagesVideo,
        ImagesOnly,
        ImagesVideo,
        TextOnly,
        ImagesText,
        Everything,
        DiagnosticsOnly,
        Recovery
    }

    internal sealed class ExtractionProfile
    {
        private ExtractionProfile(ExtractionProfileKind kind, string displayName)
        {
            Kind = kind;
            DisplayName = displayName;
        }

        public ExtractionProfileKind Kind { get; private set; }
        public string DisplayName { get; private set; }

        public bool IsImagesOnly { get { return Kind == ExtractionProfileKind.ImagesOnly; } }
        public bool IsImagesVideo { get { return Kind == ExtractionProfileKind.AutoImagesVideo || Kind == ExtractionProfileKind.ImagesVideo; } }
        public bool IsTextOnly { get { return Kind == ExtractionProfileKind.TextOnly; } }
        public bool IsImagesText { get { return Kind == ExtractionProfileKind.ImagesText; } }
        public bool IsEverything { get { return Kind == ExtractionProfileKind.Everything; } }
        public bool IsDiagnosticsOnly { get { return Kind == ExtractionProfileKind.DiagnosticsOnly; } }
        public bool IsRecovery { get { return Kind == ExtractionProfileKind.Recovery; } }

        public string UnityMode(int unityComboIndex)
        {
            if (IsEverything) return "all";
            if (IsImagesOnly) return "textures";
            if (IsImagesVideo) return "media";
            if (IsTextOnly) return "text";
            if (IsImagesText) return "textures-text";
            switch (unityComboIndex)
            {
                case 0: return "media";
                case 1: return "textures";
                case 2: return "videos";
                case 3: return "audios";
                case 4: return "meshes";
                default: return "all";
            }
        }

        public string JavaMode(int javaComboIndex)
        {
            if (IsEverything) return "all";
            if (IsImagesOnly) return "images";
            if (IsImagesVideo) return "images-svg";
            if (IsTextOnly) return "text";
            if (IsImagesText) return "images-text";
            switch (javaComboIndex)
            {
                case 0: return "images";
                case 2: return "all";
                default: return "images-svg";
            }
        }

        public string LooseMode(int looseComboIndex)
        {
            if (IsEverything) return "all";
            if (IsImagesOnly) return "images";
            if (IsImagesVideo) return "media";
            if (IsTextOnly) return "text";
            if (IsImagesText) return "images-text";
            switch (looseComboIndex)
            {
                case 1: return "images";
                case 2: return "all";
                default: return "media";
            }
        }

        public static ExtractionProfile FromIndex(int selectedIndex, string displayName)
        {
            ExtractionProfileKind kind;
            switch (selectedIndex)
            {
                case 1: kind = ExtractionProfileKind.ImagesOnly; break;
                case 2: kind = ExtractionProfileKind.ImagesVideo; break;
                case 3: kind = ExtractionProfileKind.TextOnly; break;
                case 4: kind = ExtractionProfileKind.ImagesText; break;
                case 5: kind = ExtractionProfileKind.Everything; break;
                case 6: kind = ExtractionProfileKind.DiagnosticsOnly; break;
                case 7: kind = ExtractionProfileKind.Recovery; break;
                default: kind = ExtractionProfileKind.AutoImagesVideo; break;
            }

            if (string.IsNullOrWhiteSpace(displayName))
                displayName = DefaultDisplayName(kind);
            return new ExtractionProfile(kind, displayName);
        }

        private static string DefaultDisplayName(ExtractionProfileKind kind)
        {
            switch (kind)
            {
                case ExtractionProfileKind.ImagesOnly: return "Images only";
                case ExtractionProfileKind.ImagesVideo: return "Images + Video";
                case ExtractionProfileKind.TextOnly: return "Text only";
                case ExtractionProfileKind.ImagesText: return "Images + Text";
                case ExtractionProfileKind.Everything: return "Everything";
                case ExtractionProfileKind.DiagnosticsOnly: return "Diagnostics only";
                case ExtractionProfileKind.Recovery: return "Recovery mode";
                default: return "Auto: Images + Video";
            }
        }
    }
}
