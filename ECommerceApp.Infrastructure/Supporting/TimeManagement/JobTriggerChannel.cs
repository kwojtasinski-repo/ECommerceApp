using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Supporting.TimeManagement
{
    internal sealed class JobTriggerChannel
    {
        private readonly Channel<JobTriggerRequest> _channel =
            Channel.CreateUnbounded<JobTriggerRequest>(new UnboundedChannelOptions { SingleReader = true });

        public ChannelWriter<JobTriggerRequest> Writer => _channel.Writer;
        public ChannelReader<JobTriggerRequest> Reader => _channel.Reader;

        public ValueTask WriteAsync(JobTriggerRequest request, CancellationToken ct = default)
            => _channel.Writer.WriteAsync(request, ct);
    }
}
