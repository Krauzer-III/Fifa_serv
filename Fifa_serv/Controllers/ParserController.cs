using Microsoft.AspNetCore.Mvc;
using Fifa_serv.Services;

namespace Fifa_serv.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ParserController : ControllerBase
{
    private readonly ParserService _parser;

    public ParserController(ParserService parser)
    {
        _parser = parser;
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

    [HttpGet("test")]
    public IActionResult Test()
    {
        return Ok(new { message = "Парсер готов к работе" });
    }
}