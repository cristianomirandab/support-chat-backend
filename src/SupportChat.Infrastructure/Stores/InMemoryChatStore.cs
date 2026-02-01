using System.Collections.Concurrent;
using SupportChat.Domain.Models;

namespace SupportChat.Infrastructure.Stores;

public sealed class InMemoryChatStore
{
    private readonly ConcurrentDictionary<Guid, ChatSession> _chats = new();

    public ChatSession Add(ChatSession chat)
    {
        _chats[chat.Id] = chat;
        return chat;
    }

    public bool TryGet(Guid id, out ChatSession? chat) => _chats.TryGetValue(id, out chat);

    public IEnumerable<ChatSession> All() => _chats.Values;

    public void Update(ChatSession chat) => _chats[chat.Id] = chat;
}
