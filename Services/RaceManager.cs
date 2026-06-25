using Microsoft.AspNetCore.SignalR;
using HorseRacing.Hubs;
using HorseRacing.Models;

namespace HorseRacing.Services;

public class RaceManager
{
    private readonly IHubContext<RaceHub> _hubContext;
    private readonly IServiceProvider _serviceProvider;
    private Race _race = new();
    private readonly object _raceLock = new();
    private readonly Random _random = new();
    private int _raceIdCounter;
    private CancellationTokenSource? _raceCts;

    public Race CurrentRace
    {
        get { lock (_raceLock) return _race; }
    }

    public RaceManager(IHubContext<RaceHub> hubContext, IServiceProvider serviceProvider)
    {
        ThreadPool.SetMinThreads(8, 4);
        _hubContext = hubContext;
        _serviceProvider = serviceProvider;
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
        Race currentRace;
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
                horse.Speed = _random.Next(2, 7);
            }

            currentRace = _race;
        }

        _raceCts = new CancellationTokenSource();
        var token = _raceCts.Token;

        int horseCount = currentRace.Horses.Count;
        var startBarrier = new Barrier(horseCount);
        var finishCountdown = new CountdownEvent(horseCount);

        foreach (var horse in currentRace.Horses)
        {
            var h = horse;
            ThreadPool.QueueUserWorkItem(_ => RunHorse(h, currentRace, startBarrier, finishCountdown, token));
        }

        ThreadPool.QueueUserWorkItem(async _ =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(300, token);
                    await _hubContext.Clients.All.SendAsync("RaceUpdate", ToDto(), token);
                }
                catch (OperationCanceledException) { }
            }
        });

        ThreadPool.QueueUserWorkItem(async _ =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (finishCountdown.Wait(100))
                        break;
                }

                if (!token.IsCancellationRequested)
                {
                    await FinishRaceAsync();
                }
            }
            finally
            {
                startBarrier.Dispose();
                finishCountdown.Dispose();
            }
        });
    }

    private void RunHorse(Horse horse, Race race, Barrier startBarrier, CountdownEvent finishCountdown, CancellationToken token)
    {
        try
        {
            startBarrier.SignalAndWait(token);

            while (!token.IsCancellationRequested)
            {
                Thread.Sleep(300);

                lock (_raceLock)
                {
                    if (race.Status != RaceStatus.Running) return;
                    if (horse.IsWinner) return;

                    horse.Position += horse.Speed;

                    if (horse.Position >= 100)
                    {
                        horse.Position = 100;
                        horse.IsWinner = true;

                        if (race.WinnerHorseId == null)
                            race.WinnerHorseId = horse.Id;

                        return;
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (BarrierPostPhaseException) { }
        finally
        {
            finishCountdown.Signal();
        }
    }

    private async Task FinishRaceAsync()
    {
        int? winnerId;
        lock (_raceLock)
        {
            _race.Status = RaceStatus.Finished;
            winnerId = _race.WinnerHorseId;
        }

        if (winnerId.HasValue)
        {
            var betService = _serviceProvider.GetRequiredService<BetService>();
            betService.SettleBets(winnerId.Value);
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
