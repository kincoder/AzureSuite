using AzureSuite.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web.Resource;

namespace AzureSuite.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequiredScope("Messages.ReadWrite")]
public class MessagesController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var messages = await dbContext.Pacs008Messages
            .Select(m => new { m.Id, m.MessageId, m.EndToEndId, m.Amount, m.Currency, m.Status })
            .ToListAsync();

        return Ok(messages);
    }
}
