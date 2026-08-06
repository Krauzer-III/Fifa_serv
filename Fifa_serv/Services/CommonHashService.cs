using System.Security.Cryptography;
using System.Text;
using Fifa_serv.Data;

namespace Fifa_serv.Services;

public sealed class CommonHashService
{
    private readonly LiteDbContext _db;

    public CommonHashService(LiteDbContext db) => _db = db;

    public string Calculate()
    {
        var values = _db.Matches.FindAll().Select(x => $"m:{x.Hash}")
            .Concat(_db.Players.FindAll().Select(x => $"p:{x.Hash}"))
            .Concat(_db.News.FindAll().Select(x => $"n:{x.Hash}"))
            .Concat(_db.TeamRatings.FindAll().Select(x => $"r:{x.Hash}"))
            .OrderBy(x => x, StringComparer.Ordinal);

        var payload = string.Join('|', values);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}
