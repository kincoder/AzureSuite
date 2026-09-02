using AzureSuite.Application.Messages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;

namespace AzureSuite.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequiredScope("Messages.ReadWrite")]
public class MessagesController(IPacs008MessageService messageService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Pacs008MessageSummaryDto>>> GetAll(CancellationToken cancellationToken)
    {
        var messages = await messageService.GetAllAsync(cancellationToken);
        return Ok(messages);
    }

    [HttpPost]
    public async Task<ActionResult<Pacs008MessageSummaryDto>> Create(
        [FromBody] CreatePacs008MessageRequest request,
        CancellationToken cancellationToken)
    {
        var created = await messageService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { }, created);
    }
}
