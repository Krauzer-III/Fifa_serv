using Fifa_serv.Data;
using Fifa_serv.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fifa_serv.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class RatingController : ControllerBase
{
    private readonly LiteDbContext _db;

    public RatingController(LiteDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(IReadOnlyCollection<TeamRating>),
        StatusCodes.Status200OK
    )]
    public ActionResult<IReadOnlyCollection<TeamRating>> GetAll()
    {
        var rating = _db.TeamRatings
            .Query()
            .OrderBy(x => x.Position)
            .ToList();

        return Ok(rating);
    }

    [HttpGet("{teamId:int}")]
    [ProducesResponseType(
        typeof(TeamRating),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<TeamRating> GetByTeamId(int teamId)
    {
        var team = _db.TeamRatings.FindOne(
            x => x.TeamId == teamId
        );

        if (team == null)
        {
            return NotFound(new
            {
                error = $"Команда с ID {teamId} не найдена"
            });
        }

        return Ok(team);
    }

    [HttpDelete]
    public IActionResult Clear()
    {
        var deleted = _db.TeamRatings.DeleteAll();

        return Ok(new
        {
            message = "Рейтинг очищен",
            deleted
        });
    }
}