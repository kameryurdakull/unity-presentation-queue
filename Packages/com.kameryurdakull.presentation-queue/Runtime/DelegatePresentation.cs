using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Presentations
{
    /// <summary>A show given as a method, for views that would rather not implement <see cref="IPresentation"/>.</summary>
    public sealed class DelegatePresentation : IPresentation
    {
        private readonly Func<CancellationToken, UniTask> _play;

        public DelegatePresentation(Func<CancellationToken, UniTask> play) =>
            _play = play ?? throw new ArgumentNullException(nameof(play));

        public UniTask PlayAsync(CancellationToken cancellation) => _play(cancellation);
    }
}
