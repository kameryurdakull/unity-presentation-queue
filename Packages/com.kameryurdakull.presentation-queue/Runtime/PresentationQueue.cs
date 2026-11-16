using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Presentations
{
    /// <summary>
    /// The default <see cref="IPresentationQueue"/>: one line of shows played in turn on the main thread. A show
    /// that throws is logged and the line moves on, so one broken show never holds up the ones behind it.
    /// </summary>
    public sealed class PresentationQueue : IPresentationQueue, IDisposable
    {
        private sealed class Entry
        {
            public readonly IPresentation Presentation;
            public readonly int Priority;
            public readonly CancellationToken Cancellation;
            public readonly UniTaskCompletionSource Completion = new();
            public readonly CancellationTokenSource Stop = new();
            public CancellationTokenRegistration Registration;

            public Entry(IPresentation presentation, int priority, CancellationToken cancellation)
            {
                Presentation = presentation;
                Priority = priority;
                Cancellation = cancellation;
            }
        }

        private readonly List<Entry> _waiting = new();
        private readonly CancellationTokenSource _lifetime = new();
        private readonly CancellationToken _alive;
        private Entry _playing;
        private UniTaskCompletionSource _idle;
        private bool _isStarting;
        private bool _isDisposed;

        public PresentationQueue() => _alive = _lifetime.Token;

        public bool IsBusy => _isStarting || _playing != null || _waiting.Count > 0;

        public event Action<bool> BusyChanged;

        public UniTask EnqueueAsync(IPresentation presentation, int priority = 0,
            CancellationToken cancellation = default)
        {
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            if (_isDisposed) throw new ObjectDisposedException(nameof(PresentationQueue));
            if (cancellation.IsCancellationRequested) return UniTask.FromCanceled(cancellation);

            var wasBusy = IsBusy;
            var entry = new Entry(presentation, priority, cancellation);
            _waiting.Insert(FindSlot(priority), entry);
            entry.Registration = cancellation.Register(() => Drop(entry));

            if (!wasBusy)
            {
                _isStarting = true;
                BusyChanged?.Invoke(true);
                StartAsync().Forget();
            }

            return entry.Completion.Task;
        }

        public UniTask EnqueueAsync(Func<CancellationToken, UniTask> play, int priority = 0,
            CancellationToken cancellation = default) =>
            EnqueueAsync(new DelegatePresentation(play), priority, cancellation);

        public UniTask WaitUntilIdleAsync(CancellationToken cancellation = default)
        {
            if (!IsBusy) return UniTask.CompletedTask;

            _idle ??= new UniTaskCompletionSource();
            return _idle.Task.AttachExternalCancellation(cancellation);
        }

        public void Clear()
        {
            // Copied first: a dropped show's waiter may queue another one at once.
            var dropped = _waiting.ToArray();
            _waiting.Clear();

            foreach (var entry in dropped)
            {
                entry.Registration.Dispose();
                entry.Completion.TrySetCanceled();
            }

            _playing?.Stop.Cancel();
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            Clear();
            _lifetime.Cancel();
            _lifetime.Dispose();

            var idle = _idle;
            _idle = null;
            idle?.TrySetResult();
        }

        // The first show waits a frame, so the shows queued in the same frame line up by priority before it plays.
        private async UniTaskVoid StartAsync()
        {
            var isCancelled = await UniTask.Yield(PlayerLoopTiming.Update, _alive).SuppressCancellationThrow();
            _isStarting = false;

            if (isCancelled) return;

            PlayNextAsync().Forget();
        }

        private async UniTaskVoid PlayNextAsync()
        {
            if (_waiting.Count == 0)
            {
                BecomeIdle();
                return;
            }

            var entry = _waiting[0];
            _waiting.RemoveAt(0);
            _playing = entry;

            using (var playing = CancellationTokenSource.CreateLinkedTokenSource(_alive, entry.Cancellation,
                       entry.Stop.Token))
            {
                try
                {
                    await entry.Presentation.PlayAsync(playing.Token);
                    entry.Completion.TrySetResult();
                }
                catch (OperationCanceledException)
                {
                    entry.Completion.TrySetCanceled();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    entry.Completion.TrySetResult();
                }
            }

            entry.Registration.Dispose();
            entry.Stop.Dispose();
            _playing = null;

            if (_isDisposed) return;

            PlayNextAsync().Forget();
        }

        private void Drop(Entry entry)
        {
            if (_waiting.Remove(entry))
            {
                entry.Completion.TrySetCanceled();

                if (!IsBusy)
                {
                    BecomeIdle();
                }

                return;
            }

            if (_playing == entry)
            {
                entry.Stop.Cancel();
            }
        }

        private void BecomeIdle()
        {
            BusyChanged?.Invoke(false);

            // A listener may have queued a new show; whoever waits for an idle screen waits for that one too.
            if (IsBusy) return;

            var idle = _idle;
            _idle = null;
            idle?.TrySetResult();
        }

        private int FindSlot(int priority)
        {
            for (var index = 0; index < _waiting.Count; index++)
            {
                if (_waiting[index].Priority < priority) return index;
            }

            return _waiting.Count;
        }
    }
}
