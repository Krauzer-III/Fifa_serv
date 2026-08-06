namespace Fifa_serv.Models;

public sealed class CommonHashRequest
{
    public string CommonHash { get; set; } = string.Empty;
}

public sealed class CommonHashResponse
{
    public bool IsActual { get; set; }
    public string ServerCommonHash { get; set; } = string.Empty;
    public bool SendEntityHashes => !IsActual;
}

public sealed class EntityHashesRequest
{
    public List<string> MatchHashes { get; set; } = [];
    public List<string> PlayerHashes { get; set; } = [];
    public List<string> NewsHashes { get; set; } = [];
    public List<string> TeamRatingHashes { get; set; } = [];
}

public sealed class EntitySyncResponse
{
    public string CommonHash { get; set; } = string.Empty;
    public HashDataOutputMatches Matches { get; set; } = new();
    public HashDataOutputPlayers Players { get; set; } = new();
    public HashDataOutputNews News { get; set; } = new();
    public HashDataOutputTeamRatings TeamRatings { get; set; } = new();
}

public sealed class HashDataOutputTeamRatings
{
    public List<TeamRating> NewTeamRatings { get; set; } = [];
    public List<string> DeleteTeamRatings { get; set; } = [];
}
