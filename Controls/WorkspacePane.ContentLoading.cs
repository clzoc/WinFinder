using System.Threading;

namespace WinFinder.Controls {
    public partial class WorkspacePane {
        private readonly object contentLoadSync = new object();
        private CancellationTokenSource contentLoadCancellation = new CancellationTokenSource();
        private long contentLoadGeneration;

        private CancellationToken BeginContentLoad(out long generation) {
            lock (contentLoadSync) {
                contentLoadCancellation.Cancel();
                contentLoadCancellation = new CancellationTokenSource();
                generation = Interlocked.Increment(ref contentLoadGeneration);
                return contentLoadCancellation.Token;
            }
        }

        private void CancelContentLoad() {
            lock (contentLoadSync) {
                contentLoadCancellation.Cancel();
                Interlocked.Increment(ref contentLoadGeneration);
            }
        }

        private bool IsCurrentContentLoad(long generation, CancellationToken cancellationToken) {
            return !cancellationToken.IsCancellationRequested
                && generation == Interlocked.Read(ref contentLoadGeneration);
        }
    }
}
