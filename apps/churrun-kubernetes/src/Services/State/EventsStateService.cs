using System.Runtime.CompilerServices;
using System.Threading.Tasks.Dataflow;

namespace ChurrunKubernetes.Services.State
{
    public class EventsStateService<T>
    {
        public readonly BufferBlock<T> _eventsBuffer = new();

        public void AddEvent(T kubernetesEvent)
        {
            _eventsBuffer.Post(kubernetesEvent);
        }

        public async IAsyncEnumerable<T> GetEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                T kubernetesEvent;
                try
                {
                    kubernetesEvent = await _eventsBuffer.ReceiveAsync(TimeSpan.FromSeconds(20), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }
                catch (TimeoutException)
                {
                    yield break;
                }
                yield return kubernetesEvent;
            }
        }
    }
}
