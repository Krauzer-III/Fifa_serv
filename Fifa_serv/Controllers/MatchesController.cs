using Microsoft.AspNetCore.Mvc;
using Fifa_serv.Data;
using Fifa_serv.Services;
using System.Globalization;
using Fifa_serv.Models;

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
    [HttpGet]
    public IActionResult GetAll()
    {
        var matches = _db.Matches
            .FindAll()
            .ToList();

        var orderedMatches = matches
            .OrderByDescending(GetMatchDateTime)
            .ThenByDescending(x => x.Id)
            .ToList();

        var hash = _hash.ComputeHashFromList(orderedMatches);

        var result = new
        {
            home = orderedMatches.Where(m => m.IsHome),
            away = orderedMatches.Where(m => !m.IsHome),
            hash = hash
        };

        Response.Headers.Append(
            "X-Content-Hash",
            hash
        );

        return Ok(result);
    }

    private static DateTime GetMatchDateTime(Match match)
    {
        var culture = CultureInfo.GetCultureInfo("ru-RU");

        var value = $"{match.Date} {match.Time}".Trim();

        var formats = new[]
        {
        "dd.MM.yyyy HH:mm",
        "d.MM.yyyy HH:mm",

        "dd.MM.yyyy H:mm",
        "d.MM.yyyy H:mm",

        "yyyy-MM-dd HH:mm",

        "d MMMM yyyy HH:mm",
        "dd MMMM yyyy HH:mm",

        "d MMM yyyy HH:mm",
        "dd MMM yyyy HH:mm",

        "dd.MM.yyyy",
        "d.MM.yyyy",
        "yyyy-MM-dd",

        "d MMMM yyyy",
        "dd MMMM yyyy"
    };

        if (DateTime.TryParseExact(
            value,
            formats,
            culture,
            DateTimeStyles.AllowWhiteSpaces,
            out var exact))
        {
            return exact;
        }

        if (DateTime.TryParse(
            value,
            culture,
            DateTimeStyles.AllowWhiteSpaces,
            out var parsed))
        {
            return parsed;
        }

        // Нераспознанные даты уходят в самый конец.
        return DateTime.MinValue;
    }
}