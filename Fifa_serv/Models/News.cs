using LiteDB;

namespace Fifa_serv.Models;

public class News
{
    [BsonId]
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
}