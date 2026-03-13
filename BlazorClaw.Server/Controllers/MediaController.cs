using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BlazorClaw.Server.Controllers;

[ApiController]
[Route("/api/")]
[AllowAnonymous]
public class MediaController : ControllerBase
{
    [HttpGet("media/{fileName}")]
    public async Task<ActionResult> GetMediaFile(string fileName)
    {
        if (!Guid.TryParse(Path.GetFileNameWithoutExtension(fileName), out _)) return NotFound(); 
        var file = Path.Combine("mediafiles", fileName);
        if (!System.IO.File.Exists(file)) return NotFound();
        return File(System.IO.File.OpenRead(file), GetContentType(file));
    }

    private string GetContentType(string filename)
    {
        var rext = Path.GetExtension(filename).ToLowerInvariant();
        if (rext == ".png") return "image/png";
        if (rext == ".jpg" || rext == ".jpeg") return "image/jpeg";
        if (rext == ".txt") return "text/plain";
        return "application/octet-stream";
    }
}