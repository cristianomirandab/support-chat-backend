using System.Threading.Channels;

namespace SupportChat.Infrastructure.Queues;

public sealed class ChatQueue : IMainChatQueue, IOverflowChatQueue
{
    private readonly Channel<Guid> _channel;

    public ChatQueue(int capacity)
    {
        _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(capacity)
        {            
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = false,
            SingleWriter = false
        });
    }

    public bool TryEnqueue(Guid chatId) => _channel.Writer.TryWrite(chatId);

    public ValueTask<Guid> DequeueAsync(CancellationToken ct) => _channel.Reader.ReadAsync(ct);
}