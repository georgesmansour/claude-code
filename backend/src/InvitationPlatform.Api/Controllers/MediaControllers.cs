using InvitationPlatform.Api.Services.Media;
using InvitationPlatform.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvitationPlatform.Api.Controllers;

/// <summary>
/// User-media management for the invitation owner (couple/host). Scoped to the caller's own
/// invitation via the JWT — a client can never touch another invitation's media.
/// </summary>
[ApiController]
[Route("api/client/media")]
[Authorize(Roles = "Client")]
public class ClientMediaController(MediaService media) : ControllerBase
{
    private Guid InvitationId => Guid.Parse(User.FindFirst("invitation_id")!.Value);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await media.ListAsync(InvitationId, ct));

    [HttpPost]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct)
        => await MediaEndpoints.Upload(this, media, InvitationId, file, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await media.DeleteAsync(InvitationId, id, ct)
            ? Ok(new { ok = true })
            : NotFound(new { error = "Media not found" });
}

/// <summary>
/// User-media management for the Super Admin editing ANY invitation. The invitation is named
/// explicitly in the route (admins are not tied to one invitation like clients are).
/// </summary>
[ApiController]
[Route("api/admin/invitations/{invitationId:guid}/media")]
[Authorize(Roles = "SuperAdmin")]
public class AdminMediaController(MediaService media, AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid invitationId, CancellationToken ct)
    {
        if (!await db.Invitations.AnyAsync(i => i.Id == invitationId, ct))
            return NotFound(new { error = "Invitation not found" });
        return Ok(await media.ListAsync(invitationId, ct));
    }

    [HttpPost]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Upload(Guid invitationId, IFormFile? file, CancellationToken ct)
    {
        if (!await db.Invitations.AnyAsync(i => i.Id == invitationId, ct))
            return NotFound(new { error = "Invitation not found" });
        return await MediaEndpoints.Upload(this, media, invitationId, file, ct);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid invitationId, Guid id, CancellationToken ct)
        => await media.DeleteAsync(invitationId, id, ct)
            ? Ok(new { ok = true })
            : NotFound(new { error = "Media not found" });
}

/// <summary>Shared upload handler so the client and admin controllers stay in lock-step.</summary>
internal static class MediaEndpoints
{
    public static async Task<IActionResult> Upload(
        ControllerBase c, MediaService media, Guid invitationId, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return c.BadRequest(new { error = "No file was uploaded." });
        try
        {
            var dto = await media.UploadAsync(
                invitationId, file.OpenReadStream(), file.FileName, file.ContentType, file.Length, ct);
            return c.Ok(dto);
        }
        catch (MediaException ex)
        {
            return c.BadRequest(new { error = ex.Message });
        }
    }
}
