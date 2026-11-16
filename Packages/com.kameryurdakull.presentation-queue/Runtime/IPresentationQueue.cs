using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Presentations
{
    /// <summary>
    /// Plays shows one after another, so no two of them share the screen. A show never cuts into the one playing;
    /// among the waiting ones a higher priority goes first, and equal priorities keep their order. The first show
    /// after an idle spell starts on the next frame, so shows queued in the same frame line up by priority.
    /// </summary>
    public interface IPresentationQueue
    {
        /// <summary>True while a show plays or waits its turn.</summary>
        bool IsBusy { get; }

        /// <summary>Raised with true when the first show is queued after an idle spell, false when the last ends.</summary>
        event Action<bool> BusyChanged;

        /// <summary>
        /// Queues the show; the task completes once it has played, and is cancelled if it was dropped. Cancelling the
        /// token drops a waiting show or stops a playing one.
        /// </summary>
        UniTask EnqueueAsync(IPresentation presentation, int priority = 0, CancellationToken cancellation = default);

        /// <summary>Queues a show given as a method; see <see cref="EnqueueAsync(IPresentation, int, CancellationToken)"/>.</summary>
        UniTask EnqueueAsync(Func<CancellationToken, UniTask> play, int priority = 0,
            CancellationToken cancellation = default);

        /// <summary>Completes once nothing plays or waits; at once when the queue is idle.</summary>
        UniTask WaitUntilIdleAsync(CancellationToken cancellation = default);

        /// <summary>Drops every waiting show and stops the one playing.</summary>
        void Clear();
    }
}
