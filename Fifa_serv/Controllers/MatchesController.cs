using Microsoft.AspNetCore.Mvc;
using Fifa_serv.Data;
using Fifa_serv.Services;

namespace Fifa_serv.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class MatchesController : ControllerBase
{
    private readonly LiteDbContext _db;
    private readonly HashService _hash;

    public MatchesController(LiteDbContext db, HashService hash)
    {
        _db = db;
        _hash = hash;
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        var matches = _db.Matches.FindAll().ToList();
        var hash = _hash.ComputeHashFromList(matches);

        var result = new
        {
            home = matches.Where(m => m.IsHome).OrderBy(m => m.Date),
            away = matches.Where(m => !m.IsHome).OrderBy(m => m.Date),
            hash = hash
        };

        Response.Headers.Append("X-Content-Hash", hash);
        return Ok(result);
    }
}