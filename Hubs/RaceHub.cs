using Microsoft.AspNetCore.SignalR;
using HorseRacing.Services;

namespace HorseRacing.Hubs;

public class RaceHub : Hub
{
    private readonly RaceManager _raceManager;

    public RaceHub(RaceManager raceManager)
    {
        _raceManager = raceManager;
    }

    public async Task JoinRace()
    {
        var dto = _raceManager.ToDto();
        await Clients.Caller.SendAsync("RaceUpdate", dto);
    }

    public async Task StartRace()
    {
        _raceManager.StartRace();
        await Task.CompletedTask;
    }

    public async Task ResetRace()
    {
        _raceManager.ResetRace();
        await Task.CompletedTask;
    }
}
