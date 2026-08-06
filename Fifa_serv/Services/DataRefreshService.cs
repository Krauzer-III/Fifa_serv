using Fifa_serv.Data;
using Fifa_serv.Models;

namespace Fifa_serv.Services;

public sealed class DataRefreshService
{
    private readonly LiteDbContext _db;
    private readonly TeamRatingParser _ratings;
    private readonly NewsParser _news;
    private readonly ILogger<DataRefreshService> _logger;

    public DataRefreshService(LiteDbContext db, TeamRatingParser ratings, NewsParser news, ILogger<DataRefreshService> logger)
    {
        _db = db;
        _ratings = ratings;
        _news = news;
        _logger = logger;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var ratings = await _ratings.ParseAsync(cancellationToken);
        Replace(_db.TeamRatings, ratings);

        var news = await _news.ParseAsync(cancellationToken);
        Replace(_db.News, news);

        _logger.LogInformation("Daily refresh completed: {Ratings} ratings, {News} news", ratings.Count, news.Count);
    }

    private static void Replace<T>(LiteDB.ILiteCollection<T> collection, IEnumerable<T> values)
    {
        collection.DeleteAll();
        collection.InsertBulk(values);
    }
}
