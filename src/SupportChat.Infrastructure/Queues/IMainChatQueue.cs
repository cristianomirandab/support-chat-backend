namespace SupportChat.Infrastructure.Queues;

public interface IMainChatQueue
{
    bool TryEnqueue(Guid chatId);
    ValueTask<Guid> DequeueAsync(CancellationToken ct);
}
