using VContainer;
using VContainer.Unity;

namespace Presentations.Samples
{
    public sealed class ExampleLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterPresentationQueue();
            builder.RegisterEntryPoint<GreetingExample>();
        }
    }
}
