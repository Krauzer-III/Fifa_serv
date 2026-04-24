using LiteDB;
using Fifa_serv.Models;

namespace Fifa_serv.Data;

public class LiteDbContext
{
    public LiteDatabase Database { get; }

    public LiteDbContext()
    {
        var connectionString = "Filename=Data/mfk_gazprom.db;connection=shared";
        Database = new LiteDatabase(connectionString);

        // Создаём индексы для быстрого поиска
        Database.GetCollection<Player>("players").EnsureIndex(x => x.Number);
        Database.GetCollection<Match>("matches").EnsureIndex(x => x.Date);
        Database.GetCollection<News>("news").EnsureIndex(x => x.Date);
    }

    public ILiteCollection<Player> Players => Database.GetCollection<Player>("players");
    public ILiteCollection<Match> Matches => Database.GetCollection<Match>("matches");
    public ILiteCollection<News> News => Database.GetCollection<News>("news");
    public ILiteCollection<ClubInfo> ClubInfo => Database.GetCollection<ClubInfo>("clubinfo");
}