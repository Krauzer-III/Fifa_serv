using Fifa_serv.Data;
using Fifa_serv.Models;

namespace Fifa_serv.Services;

public sealed class HashCheck
{
    private readonly LiteDbContext _db;

    public HashCheck(LiteDbContext db) => _db = db;

    public HashDataOutputMatches GetActualMatches(IEnumerable<string>? clientHashes) =>
        Diff(_db.Matches.FindAll(), clientHashes, x => x.Hash,
            (items, deleted) => new HashDataOutputMatches { NewMatches = items, DeleteMatches = deleted });

    public HashDataOutputPlayers GetActualPlayers(IEnumerable<string>? clientHashes) =>
        Diff(_db.Players.FindAll(), clientHashes, x => x.Hash,
            (items, deleted) => new HashDataOutputPlayers { NewPlayers = items, DeletePlayers = deleted });

    public HashDataOutputNews GetActualNews(IEnumerable<string>? clientHashes) =>
        Diff(_db.News.FindAll(), clientHashes, x => x.Hash,
            (items, deleted) => new HashDataOutputNews { NewNews = items, DeleteNews = deleted });

    public HashDataOutputTeamRatings GetActualTeamRatings(IEnumerable<string>? clientHashes) =>
        Diff(_db.TeamRatings.FindAll(), clientHashes, x => x.Hash,
            (items, deleted) => new HashDataOutputTeamRatings { NewTeamRatings = items, DeleteTeamRatings = deleted });

    private static TResult Diff<T, TResult>(
        IEnumerable<T> serverItems,
        IEnumerable<string>? clientHashes,
        Func<T, string> hashSelector,
        Func<List<T>, List<string>, TResult> resultFactory)
    {
        var items = serverItems.ToList();
        var client = (clientHashes ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.Ordinal);
        var server = items.Select(hashSelector).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.Ordinal);

        var added = items.Where(x => !client.Contains(hashSelector(x))).ToList();
        var deleted = client.Except(server).ToList();
        return resultFactory(added, deleted);
    }
}
