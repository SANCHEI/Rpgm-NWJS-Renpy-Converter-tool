using System;

namespace RpgmvpConverterWinForms
{
    internal sealed class OperationResult
    {
        public OperationResult(string engine, string outputDir, int extracted, long bytes, int errors, int renamed, TimeSpan duration)
            : this(engine, outputDir, extracted, bytes, errors, renamed, 0, duration)
        {
        }

        public OperationResult(string engine, string outputDir, int extracted, long bytes, int errors, int renamed, int skipped, TimeSpan duration)
        {
            Engine = engine;
            OutputDir = outputDir;
            Extracted = extracted;
            Bytes = bytes;
            Errors = errors;
            Renamed = renamed;
            Skipped = skipped;
            Duration = duration;
        }

        public string Engine { get; private set; }
        public string OutputDir { get; private set; }
        public int Extracted { get; private set; }
        public long Bytes { get; private set; }
        public int Errors { get; private set; }
        public int Renamed { get; private set; }
        public int Skipped { get; private set; }
        public TimeSpan Duration { get; private set; }

        public static OperationResult Failed(string engine, string outputDir, string message)
        {
            return new OperationResult(engine + " - " + message, outputDir, 0, 0, 1, 0, TimeSpan.Zero);
        }

        public string ToReport()
        {
            return ExtractionReportBuilder.Build(
                "2.4.3",
                Engine,
                OutputDir,
                Extracted,
                Bytes,
                Renamed,
                Skipped,
                Errors,
                Duration);
        }
    }
}
