using BlazorClaw.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlazorClaw.Server.Controllers;

[ApiController]
[AllowAnonymous]
public class MediaController(PathHelper pathHelper) : ControllerBase
{
    [HttpGet("/uploads/{fileName}")]
    public async Task<ActionResult> GetMediaFile(string fileName)
    {
        var t = await pathHelper.GetMediaFileAsync(fileName);
        if (t == null) return NotFound();
        return File(t.Item1, t.Item2);
    }
}