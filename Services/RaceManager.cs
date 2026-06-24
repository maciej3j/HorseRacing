using Microsoft.AspNetCore.SignalR;
using HorseRacing.Hubs;
using HorseRacing.Models;

namespace HorseRacing.Services;

public class RaceManager
{
    private readonly IHubContext<RaceHub> _hubContext;
    private Race _race = new();
    private readonly object _raceLock = new();
    private readonly Random _random = new();
    private int _raceIdCounter;
    private CancellationTokenSource? _raceCts;

    public Race CurrentRace
    {
        get { lock (_raceLock) return _race; }
    }

    public RaceManager(IHubContext<RaceHub> hubContext)
    {
        _hubContext = hubContext;
        InitializeNewRace();
    }

    private void InitializeNewRace()
    {
        _race = new Race
        {
            Id = Interlocked.Increment(ref _raceIdCounter),
            Status = RaceStatus.Waiting,
            Horses = new List<Horse>
            {
                new() { Id = 1, Name = "Błyskawica", Speed = 0, Odds = 2.0m },
                new() { Id = 2, Name = "Złoty Kopyto", Speed = 0, Odds = 3.5m },
                new() { Id = 3, Name = "Leśny Wiatr", Speed = 0, Odds = 4.0m },
                new() { Id = 4, Name = "Czerwona Strzała", Speed = 0, Odds = 5.0m },
                new() { Id = 5, Name = "Nocny Goniec", Speed = 0, Odds = 6.0m },
            }
        };
    }

    public void ResetRace()
    {
        lock (_raceLock)
        {
            _raceCts?.Cancel();
            InitializeNewRace();
        }

        _hubContext.Clients.All.SendAsync("RaceUpdate", ToDto());
    }

    public (bool Success, string Message) PlaceBet(Bet bet)
    {
        lock (_raceLock)
        {
            if (_race.Status != RaceStatus.Waiting)
                return (false, "Wyścig już trwa lub zakończył się. Nie można przyjąć zakładu.");

            var horse = _race.Horses.FirstOrDefault(h => h.Id == bet.HorseId);
            if (horse == null)
                return (false, "Nie znaleziono konia o podanym ID.");

            return (true, "Zakład przyjęty!");
        }
    }

    public void StartRace()
    {
        lock (_raceLock)
        {
            if (_race.Status == RaceStatus.Running) return;
            _race.Status = RaceStatus.Running;
            _race.StartTime = DateTime.UtcNow;
            _race.WinnerHorseId = null;

            foreach (var horse in _race.Horses)
            {
                horse.Position = 0;
                horse.IsWinner = false;
                horse.Speed = _random.Next(1, 6);
            }
        }

        _raceCts = new CancellationTokenSource();
        var token = _raceCts.Token;

        var horseTasks = new List<Task>();
        foreach (var horse in _race.Horses)
        {
            var h = horse;
            horseTasks.Add(Task.Run(() => RunHorseAsync(h, token), token));
        }

        _ = Task.Run(() => BroadcastRaceAsync(token), token);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.WhenAll(horseTasks);
                await FinishRaceAsync();
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    private async Task RunHorseAsync(Horse horse, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(300, token);

            lock (_raceLock)
            {
                if (_race.Status != RaceStatus.Running) return;
                if (horse.IsWinner) return;

                horse.Position += horse.Speed;

                if (horse.Position >= 100)
                {
                    horse.Position = 100;
                    horse.IsWinner = true;

                    if (_race.WinnerHorseId == null)
                        _race.WinnerHorseId = horse.Id;
                }
            }
        }
    }

    private async Task BroadcastRaceAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(300, token);

            try
            {
                await _hubContext.Clients.All.SendAsync("RaceUpdate", ToDto(), token);
            }
            catch (OperationCanceledException) { }
        }
    }

    private async Task FinishRaceAsync()
    {
        lock (_raceLock)
        {
            _race.Status = RaceStatus.Finished;
        }

        await _hubContext.Clients.All.SendAsync("RaceUpdate", ToDto());
        await _hubContext.Clients.All.SendAsync("RaceFinished", ToDto());
    }

    public RaceStateDto ToDto()
    {
        lock (_raceLock)
        {
            return new RaceStateDto
            {
                Status = _race.Status.ToString(),
                WinnerId = _race.WinnerHorseId,
                Horses = _race.Horses.Select(h => new HorseDto
                {
                    Id = h.Id,
                    Name = h.Name,
                    Position = h.Position,
                    Odds = h.Odds
                }).ToList()
            };
        }
    }
}
