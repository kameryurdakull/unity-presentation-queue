using System.Threading;
using Cysharp.Threading.Tasks;

namespace Presentations
{
    /// <summary>A show that takes the screen for a while: a banner, a reveal, an intro card.</summary>
    public interface IPresentation
    {
        /// <summary>
        /// Plays the show to its end. The token is cancelled when the show is dropped, the queue is cleared or the
        /// queue is disposed; the show should then stop where it is.
        /// </summary>
        UniTask PlayAsync(CancellationToken cancellation);
    }
}
