namespace SupportChat.Infrastructure.Queues;

public interface IOverflowChatQueue
{
    bool TryEnqueue(Guid chatId);
    ValueTask<Guid> DequeueAsync(CancellationToken ct);
}
