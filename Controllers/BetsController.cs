using Microsoft.AspNetCore.Mvc;
using HorseRacing.Services;

namespace HorseRacing.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BetsController : ControllerBase
{
    private readonly BetService _betService;
    private readonly RaceManager _raceManager;

    public BetsController(BetService betService, RaceManager raceManager)
    {
        _betService = betService;
        _raceManager = raceManager;
    }

    [HttpGet("race")]
    public IActionResult GetRaceState()
    {
        var dto = _raceManager.ToDto();
        return Ok(dto);
    }

    [HttpPost]
    public IActionResult PlaceBet([FromBody] PlaceBetRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName))
            return BadRequest(new { message = "Nazwa użytkownika jest wymagana." });

        var (success, message, bet) = _betService.PlaceBet(request.UserName, request.HorseId, request.Amount);
        if (!success)
            return BadRequest(new { message });

        return Ok(new { message, bet });
    }

    [HttpGet("{userName}")]
    public IActionResult GetUserBets(string userName)
    {
        var bets = _betService.GetUserBets(userName);
        return Ok(bets);
    }
}

public class PlaceBetRequest
{
    public string UserName { get; set; } = string.Empty;
    public int HorseId { get; set; }
    public decimal Amount { get; set; }
}
