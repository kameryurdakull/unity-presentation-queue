using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace Presentations.Tests
{
    public sealed class PresentationQueueTests
    {
        /// <summary>A show that writes when it starts and ends, and plays until it is let go.</summary>
        private sealed class HeldShow : IPresentation
        {
            private readonly string _name;
            private readonly List<string> _log;
            private readonly UniTaskCompletionSource _release = new();

            public HeldShow(string name, List<string> log)
            {
                _name = name;
                _log = log;
            }

            public bool HasStarted { get; private set; }

            public void Release() => _release.TrySetResult();

            public async UniTask PlayAsync(CancellationToken cancellation)
            {
                HasStarted = true;
                _log.Add(_name + " start");
                await _release.Task.AttachExternalCancellation(cancellation);
                _log.Add(_name + " end");
            }
        }

        private readonly List<string> _log = new();
        private PresentationQueue _queue;

        [SetUp]
        public void SetUp()
        {
            _log.Clear();
            _queue = new PresentationQueue();
        }

        [TearDown]
        public void TearDown() => _queue.Dispose();

        [Test]
        public async Task Shows_PlayOneAfterAnother()
        {
            var first = new HeldShow("first", _log);
            var second = new HeldShow("second", _log);

            _queue.EnqueueAsync(first).Forget();
            var secondDone = _queue.EnqueueAsync(second);
            await UniTask.WaitUntil(() => first.HasStarted);
            await UniTask.Yield();

            Assert.That(second.HasStarted, Is.False);

            first.Release();
            second.Release();
            await secondDone;

            Assert.That(_log, Is.EqualTo(new[] { "first start", "first end", "second start", "second end" }));
        }

        [Test]
        public async Task ShowsQueuedInTheSameFrame_PlayByPriority()
        {
            var low = new HeldShow("low", _log);
            var high = new HeldShow("high", _log);
            low.Release();
            high.Release();

            _queue.EnqueueAsync(low).Forget();
            await _queue.EnqueueAsync(high, 1);

            Assert.That(_log[0], Is.EqualTo("high start"));
        }

        [Test]
        public async Task AHigherPriority_NeverCutsIntoThePlayingShow()
        {
            var playing = new HeldShow("playing", _log);
            var urgent = new HeldShow("urgent", _log);

            _queue.EnqueueAsync(playing).Forget();
            await UniTask.WaitUntil(() => playing.HasStarted);
            var urgentDone = _queue.EnqueueAsync(urgent, 10);
            await UniTask.Yield();

            Assert.That(urgent.HasStarted, Is.False);

            playing.Release();
            urgent.Release();
            await urgentDone;

            Assert.That(_log, Is.EqualTo(new[] { "playing start", "playing end", "urgent start", "urgent end" }));
        }

        [Test]
        public async Task Idle_IsReportedOnceTheLastShowEnds()
        {
            var changes = new List<bool>();
            _queue.BusyChanged += changes.Add;
            var show = new HeldShow("show", _log);

            _queue.EnqueueAsync(show).Forget();
            var idle = _queue.WaitUntilIdleAsync();

            Assert.That(_queue.IsBusy, Is.True);

            show.Release();
            await idle;

            Assert.That(_queue.IsBusy, Is.False);
            Assert.That(changes, Is.EqualTo(new[] { true, false }));
            Assert.That(_queue.WaitUntilIdleAsync().Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public async Task CancellingAWaitingShow_DropsItAndTheLineMovesOn()
        {
            var first = new HeldShow("first", _log);
            var dropped = new HeldShow("dropped", _log);
            var last = new HeldShow("last", _log);
            var cancellation = new CancellationTokenSource();

            _queue.EnqueueAsync(first).Forget();
            var droppedDone = _queue.EnqueueAsync(dropped, 0, cancellation.Token);
            var lastDone = _queue.EnqueueAsync(last);
            cancellation.Cancel();
            first.Release();
            last.Release();
            await lastDone;

            Assert.That(droppedDone.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(dropped.HasStarted, Is.False);
            Assert.That(_log, Is.EqualTo(new[] { "first start", "first end", "last start", "last end" }));
        }

        [Test]
        public async Task Clear_StopsThePlayingShowAndDropsTheRest()
        {
            var playing = new HeldShow("playing", _log);
            var waiting = new HeldShow("waiting", _log);

            var playingDone = _queue.EnqueueAsync(playing);
            var waitingDone = _queue.EnqueueAsync(waiting);
            await UniTask.WaitUntil(() => playing.HasStarted);
            _queue.Clear();
            await _queue.WaitUntilIdleAsync();

            Assert.That(playingDone.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(waitingDone.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(waiting.HasStarted, Is.False);
        }

        [Test]
        public async Task AShowThatThrows_DoesNotHoldUpTheLine()
        {
            var after = new HeldShow("after", _log);
            after.Release();
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: broken show");

            _queue.EnqueueAsync(_ => throw new InvalidOperationException("broken show")).Forget();
            await _queue.EnqueueAsync(after);

            Assert.That(_log, Is.EqualTo(new[] { "after start", "after end" }));
        }

        [Test]
        public void RegisterPresentationQueue_ResolvesSingleton()
        {
            var builder = new ContainerBuilder();
            builder.RegisterPresentationQueue();

            using (var container = builder.Build())
            {
                var first = container.Resolve<IPresentationQueue>();
                var second = container.Resolve<IPresentationQueue>();

                Assert.That(first, Is.SameAs(second));
            }
        }
    }
}
