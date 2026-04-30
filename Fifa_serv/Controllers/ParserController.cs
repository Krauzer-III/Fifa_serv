using Microsoft.AspNetCore.Mvc;
using Fifa_serv.Services;
using Fifa_serv.Data;

namespace Fifa_serv.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ParserController : ControllerBase
{
    private readonly ParserService _parser;
    private readonly LiteDbContext _db;

    public ParserController(ParserService parser, LiteDbContext db)
    {
        _parser = parser;
        _db = db;
    }

    [HttpPost("parse-team")]
    public async Task<IActionResult> ParseTeam()
    {
        try
        {
            await _parser.ParseTeamAsync();
            return Ok(new { message = "Парсинг команды успешно выполнен" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }


    [HttpPost("parse-matches")]
    public async Task<IActionResult> ParseMatchesMain()
    {
        try
        {
            await _parser.ParseMatchesAsync("https://superliga.rfs.ru/tournament/1054805/calendar");
            return Ok(new { message = "Парсинг матчей успешно выполнен" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("parse-matches-playoff")]
    public async Task<IActionResult> ParseMatchesPlayOff()
    {
        try
        {
            await _parser.ParseMatchesAsync("https://superliga.rfs.ru/tournament/1054805/calendar?round_id=1122977", "Плей-Офф");
            return Ok(new { message = "Парсинг матчей успешно выполнен" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("test")]
    public IActionResult Test()
    {
        return Ok(new { message = "Парсер готов к работе" });
    }

    [HttpPost("clear-database")]
    public IActionResult ClearDatabase()
    {
        try
        {
            // Получаем количество записей до очистки
            var playersCount = _db.Players.Count();
            var matchesCount = _db.Matches.Count();
            var newsCount = _db.News.Count();
            var clubInfoCount = _db.ClubInfo.Count();

            // Очищаем все коллекции
            _db.Players.DeleteAll();
            _db.Matches.DeleteAll();
            _db.News.DeleteAll();
            _db.ClubInfo.DeleteAll();

            return Ok(new
            {
                message = "База данных очищена",
                deleted = new
                {
                    players = playersCount,
                    matches = matchesCount,
                    news = newsCount,
                    clubInfo = clubInfoCount
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("clear-matches")]
    public IActionResult ClearMatches()
    {
        try
        {
            var count = _db.Matches.Count();
            _db.Matches.DeleteAll();
            return Ok(new { message = $"Удалено матчей: {count}" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("clear-players")]
    public IActionResult ClearPlayers()
    {
        try
        {
            var count = _db.Players.Count();
            _db.Players.DeleteAll();
            return Ok(new { message = $"Удалено игроков: {count}" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}