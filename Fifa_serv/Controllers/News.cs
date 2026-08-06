using Fifa_serv.Data;
using Fifa_serv.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fifa_serv.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class NewsController : ControllerBase
{
    private readonly LiteDbContext _db;

    public NewsController(LiteDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public ActionResult<List<News>> GetAll()
    {
        var news = _db.News
            .Query()
            .OrderByDescending(x => x.Id)
            .ToList();

        return Ok(news);
    }

    [HttpGet("{id:int}")]
    public ActionResult<News> GetById(int id)
    {
        var news = _db.News.FindById(id);

        if (news == null)
        {
            return NotFound(new
            {
                error = $"Новость с ID {id} не найдена"
            });
        }

        return Ok(news);
    }
}