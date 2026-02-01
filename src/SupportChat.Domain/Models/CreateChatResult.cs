using SupportChat.Domain.Models;

namespace SupportChat.Domain.Services;

public sealed record CreateChatResult(
    bool Accepted,
    Guid ChatId,
    string Status,
    Teams? Team = null
);
