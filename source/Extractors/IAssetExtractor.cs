using System;
using System.Collections.Generic;
using System.Linq;

namespace RpgmvpConverterWinForms
{
    internal interface IAssetExtractor
    {
        string Id { get; }
        string DisplayName { get; }
        bool CanExtract(string inputPath);
        CollectorResult Extract(string inputPath, string outputDir);
    }

    internal static class AssetExtractorRegistry
    {
        private static readonly IAssetExtractor[] extractors =
        {
            new ElectronAsarExtractor(),
            new FlashSwfExtractor(),
            new SpakDatExtractor()
        };

        public static IEnumerable<IAssetExtractor> All
        {
            get { return extractors; }
        }

        public static IAssetExtractor Find(string id)
        {
            IAssetExtractor extractor = extractors.FirstOrDefault(delegate(IAssetExtractor candidate)
            {
                return candidate.Id.Equals(id, StringComparison.OrdinalIgnoreCase);
            });
            if (extractor == null)
                throw new InvalidOperationException("Extractor is not registered: " + id);
            return extractor;
        }

        public static IAssetExtractor Detect(string inputPath)
        {
            return extractors.FirstOrDefault(delegate(IAssetExtractor extractor)
            {
                return extractor.CanExtract(inputPath);
            });
        }
    }
}
