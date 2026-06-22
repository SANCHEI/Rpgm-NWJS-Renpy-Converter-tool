using System;

namespace RpgmvpConverterWinForms
{
    internal sealed class ExtractionProgressEvent
    {
        public ExtractionProgressEvent(string operation, int processed, int total, long bytes)
        {
            Operation = operation ?? "";
            Processed = processed;
            Total = total;
            Bytes = bytes;
            CreatedUtc = DateTime.UtcNow;
        }

        public string Operation { get; private set; }
        public int Processed { get; private set; }
        public int Total { get; private set; }
        public long Bytes { get; private set; }
        public DateTime CreatedUtc { get; private set; }
    }
}
