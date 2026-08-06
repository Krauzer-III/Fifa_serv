using LiteDB;

namespace Fifa_serv.Models;

public class TeamRating
{
    [BsonId]
    public int Id { get; set; }

    public int Position { get; set; }
    public int? TeamId { get; set; }

    public string TeamName { get; set; } = string.Empty;
    public string TeamUrl { get; set; } = string.Empty;
    public string LogoBase64 { get; set; } = string.Empty;

    public int Games { get; set; }
    public int Wins { get; set; }
    public int WinsAfterPenalties { get; set; }
    public int LossesAfterPenalties { get; set; }
    public int Losses { get; set; }

    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int Points { get; set; }

    public string RatingType { get; set; } = "Основной";
    public string Hash { get; set; } = string.Empty;
}