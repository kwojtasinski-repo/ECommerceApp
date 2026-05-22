using System.Threading.Channels;

namespace RagTools.Core;

/// <summary>
/// Singleton <see cref="Channel{T}"/> wrapper for <see cref="IngestJob"/> items.
///
/// The HTTP controller writes to the channel; <see cref="IngestWorker"/> reads from it.
/// Bounded capacity (default: 1000) provides back-pressure — the controller returns 503
/// when the channel is full.
///
/// Registered as a singleton in DI:
///   services.AddSingleton&lt;IngestChannel&gt;();
///   services.AddSingleton(sp =&gt; sp.GetRequiredService&lt;IngestChannel&gt;().Reader);
///   services.AddSingleton(sp =&gt; sp.GetRequiredService&lt;IngestChannel&gt;().Writer);
/// </summary>
public sealed class IngestChannel
{
    private readonly Channel<IngestJob> _channel;
    private readonly int _capacity;

    public IngestChannel(int capacity = 1000)
    {
        _capacity = capacity;
        _channel = Channel.CreateBounded<IngestJob>(new BoundedChannelOptions(capacity)
        {
            FullMode     = BoundedChannelFullMode.Wait,   // back-pressure (controller awaits)
            SingleReader = true,                          // only IngestWorker reads
            SingleWriter = false,                         // multiple HTTP requests can write
        });
    }

    public ChannelReader<IngestJob> Reader => _channel.Reader;
    public ChannelWriter<IngestJob> Writer => _channel.Writer;

    /// <summary>
    /// Try to enqueue without blocking. Returns false when the channel is full.
    /// Use from controllers that prefer 503 over blocking.
    /// </summary>
    public bool TryWrite(IngestJob job) => _channel.Writer.TryWrite(job);

    /// <summary>Enqueue, waiting if the channel is at capacity.</summary>
    public ValueTask WriteAsync(IngestJob job, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(job, ct);

    /// <summary>Current number of pending items (approximate).</summary>
    public int PendingCount => _channel.Reader.Count;

    /// <summary>Maximum number of items the channel can hold (the capacity it was created with).</summary>
    public int Capacity => _capacity;
}
