using Microsoft.AspNetCore.Mvc;
using SupportChat.Domain.Models;
using SupportChat.Api.Services;
using SupportChat.Infrastructure.Stores;
using SupportChat.Infrastructure.Time;

namespace SupportChat.Api.Controllers;

[ApiController]
[Route("chats")]
public sealed class ChatsController : ControllerBase
{
    private readonly InMemoryChatStore _chats;
    private readonly IClock _clock;
    private readonly ChatAdmissionService _admission;

    public ChatsController(
        InMemoryChatStore chats,
        IClock clock,
        ChatAdmissionService admission)
    {
        _chats = chats;
        _clock = clock;
        _admission = admission;
    }

    [HttpPost]
    public IActionResult Create()
    {
        var result = _admission.CreateChat();

        if (!result.Accepted)
            return StatusCode(429, new { chatId = result.ChatId, status = result.Status });

        return Ok(new { chatId = result.ChatId, status = result.Status, team = result.Team!.ToString() });
    }

    [HttpGet("{id:guid}")]
    public IActionResult Get(Guid id)
    {
        if (!_chats.TryGet(id, out var chat) || chat is null)
            return NotFound();

        return Ok(new
        {
            chat.Id,
            Status = chat.Status.ToString(),
            chat.AssignedAgentId,
            AssignedTeam = chat.AssignedTeam.ToString(),
            chat.LastPollAt,
            chat.CreatedAt
        });
    }

    [HttpGet("{id:guid}/poll")]
    public IActionResult Poll(Guid id)
    {
        if (!_chats.TryGet(id, out var chat) || chat is null)
            return NotFound();

        chat.LastPollAt = _clock.UtcNow;

        // First client poll after being assigned marks the chat as Active.
        if (chat.Status == ChatStatus.Assigned)
            chat.Status = ChatStatus.Active;

        _chats.Update(chat);

        return Ok(new
        {
            chat.Id,
            Status = chat.Status.ToString(),
            chat.AssignedAgentId,
            AssignedTeam = chat.AssignedTeam.ToString()
        });
    }
}
