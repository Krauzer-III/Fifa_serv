using Fifa_serv.Data;
using Fifa_serv.Models;
using Fifa_serv.Services;
using Microsoft.AspNetCore.Mvc;

namespace Fifa_serv.Controllers;

[ApiController]
[Route("api/team-ratings")]
public sealed class TeamRatingsController : ControllerBase
{
    private readonly LiteDbContext _db;
    private readonly DataRefreshService _refresh;

    public TeamRatingsController(LiteDbContext db, DataRefreshService refresh)
    {
        _db = db;
        _refresh = refresh;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<TeamRating>> Get() => Ok(_db.TeamRatings.Query().OrderBy(x => x.Position).ToList());

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        await _refresh.RefreshAsync(cancellationToken);
        return NoContent();
    }
}
