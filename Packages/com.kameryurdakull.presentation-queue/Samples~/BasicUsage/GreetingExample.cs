using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace Presentations.Samples
{
    /// <summary>Queues two shows in the same frame; the higher priority one plays first, then the screen clears.</summary>
    public sealed class GreetingExample : IStartable
    {
        private readonly IPresentationQueue _queue;

        public GreetingExample(IPresentationQueue queue) => _queue = queue;

        public void Start()
        {
            _queue.EnqueueAsync(cancellation => SayAsync("Second: the night begins", cancellation)).Forget();
            _queue.EnqueueAsync(cancellation => SayAsync("First: welcome", cancellation), priority: 1).Forget();
            WaitForQuietAsync().Forget();
        }

        private async UniTaskVoid WaitForQuietAsync()
        {
            await _queue.WaitUntilIdleAsync();
            Debug.Log("Every show has played; the game may start.");
        }

        private static async UniTask SayAsync(string line, CancellationToken cancellation)
        {
            Debug.Log(line);
            await UniTask.Delay(1000, cancellationToken: cancellation);
        }
    }
}
