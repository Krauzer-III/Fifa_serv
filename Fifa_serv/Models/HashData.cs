namespace Fifa_serv.Models;

public sealed class HashDataInput
{
    public List<string> HashStrings { get; set; } = [];
}

public sealed class HashDataOutputPlayers
{
    public List<Player> NewPlayers { get; set; } = [];
    public List<string> DeletePlayers { get; set; } = [];
}

public sealed class HashDataOutputMatches
{
    public List<Match> NewMatches { get; set; } = [];
    public List<string> DeleteMatches { get; set; } = [];
}

public sealed class HashDataOutputNews
{
    public List<News> NewNews { get; set; } = [];
    public List<string> DeleteNews { get; set; } = [];
}
