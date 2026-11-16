using System;
using VContainer;

namespace Presentations
{
    public static class VContainerPresentationQueueExtensions
    {
        /// <summary>Registers one queue for the scope; the container disposes it, stopping whatever still plays.</summary>
        public static void RegisterPresentationQueue(this IContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.Register<IPresentationQueue, PresentationQueue>(Lifetime.Singleton);
        }
    }
}
