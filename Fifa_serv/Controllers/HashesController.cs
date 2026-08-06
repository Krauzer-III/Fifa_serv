using Fifa_serv.Models;
using Fifa_serv.Services;
using Microsoft.AspNetCore.Mvc;

namespace Fifa_serv.Controllers;

[ApiController]
[Route("api/hashes")]
public sealed class HashesController : ControllerBase
{
    private readonly CommonHashService _commonHash;
    private readonly EntityHashDiffService _hashDiff;

    public HashesController(CommonHashService commonHash, EntityHashDiffService hashDiff)
    {
        _commonHash = commonHash;
        _hashDiff = hashDiff;
    }

    [HttpPost("check")]
    public ActionResult<CommonHashResponse> Check([FromBody] CommonHashRequest request)
    {
        var serverHash = _commonHash.Calculate();
        return Ok(new CommonHashResponse
        {
            IsActual = string.Equals(request.CommonHash, serverHash, StringComparison.OrdinalIgnoreCase),
            ServerCommonHash = serverHash
        });
    }

    [HttpPost("sync")]
    public ActionResult<EntitySyncResponse> Sync([FromBody] EntityHashesRequest request) => Ok(new EntitySyncResponse
    {
        CommonHash = _commonHash.Calculate(),
        Matches = _hashDiff.GetActualMatches(request.MatchHashes),
        Players = _hashDiff.GetActualPlayers(request.PlayerHashes),
        News = _hashDiff.GetActualNews(request.NewsHashes),
        TeamRatings = _hashDiff.GetActualTeamRatings(request.TeamRatingHashes)
    });
}
