using System;
using System.Threading;

namespace RpgmvpConverterWinForms
{
    internal sealed class ExtractionRunContext : IDisposable
    {
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

        public ExtractionRunContext(string operationName)
        {
            OperationName = operationName ?? "";
            StartedUtc = DateTime.UtcNow;
        }

        public string OperationName { get; private set; }
        public DateTime StartedUtc { get; private set; }
        public CancellationToken Token { get { return cancellation.Token; } }
        public bool IsCancellationRequested { get { return cancellation.IsCancellationRequested; } }

        public void Cancel()
        {
            cancellation.Cancel();
        }

        public void ThrowIfCancellationRequested()
        {
            cancellation.Token.ThrowIfCancellationRequested();
        }

        public void Dispose()
        {
            cancellation.Dispose();
        }
    }
}
