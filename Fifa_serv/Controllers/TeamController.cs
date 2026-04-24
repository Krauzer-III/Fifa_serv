using Microsoft.AspNetCore.Mvc;
using Fifa_serv.Models;
using Fifa_serv.Data;
using Fifa_serv.Services;

namespace Fifa_serv.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class TeamController : ControllerBase
{
    private readonly LiteDbContext _db;
    private readonly HashService _hash;

    public TeamController(LiteDbContext db, HashService hash)
    {
        _db = db;
        _hash = hash;
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        var players = _db.Players.FindAll().ToList();
        var hash = _hash.ComputeHashFromList(players);
        Response.Headers.Append("X-Content-Hash", hash);
        return Ok(players);
    }

    [HttpGet("{number}")]
    public IActionResult GetByNumber(int number)
    {
        var player = _db.Players.FindOne(x => x.Number == number);
        if (player == null) return NotFound();
        return Ok(player);
    }

    [HttpPost]
    public IActionResult Create(Player player)
    {
        player.Hash = _hash.ComputeHash(player);
        _db.Players.Insert(player);
        return CreatedAtAction(nameof(GetByNumber), new { number = player.Number }, player);
    }
}