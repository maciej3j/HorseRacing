using HorseRacing.Models;

namespace HorseRacing.Services;

public class BetService
{
    private readonly RaceManager _raceManager;
    private readonly List<Bet> _bets = new();
    private readonly object _betLock = new();
    private int _betIdCounter;

    public BetService(RaceManager raceManager)
    {
        _raceManager = raceManager;
    }

    public (bool Success, string Message, Bet? Bet) PlaceBet(string userName, int horseId, decimal amount)
    {
        if (amount <= 0)
            return (false, "Kwota musi być większa od 0.", null);

        var race = _raceManager.CurrentRace;
        var horse = race.Horses.FirstOrDefault(h => h.Id == horseId);
        if (horse == null)
            return (false, "Nie znaleziono konia.", null);

        var (success, message) = _raceManager.PlaceBet(new Bet { HorseId = horseId });
        if (!success)
            return (false, message, null);

        lock (_betLock)
        {
            var bet = new Bet
            {
                Id = Interlocked.Increment(ref _betIdCounter),
                UserName = userName,
                HorseId = horseId,
                Amount = amount
            };
            _bets.Add(bet);
            return (true, "Zakład przyjęty!", bet);
        }
    }

    public List<Bet> GetUserBets(string userName)
    {
        lock (_betLock)
        {
            return _bets.Where(b => b.UserName == userName).ToList();
        }
    }

    public void SettleBets(int winnerHorseId)
    {
        lock (_betLock)
        {
            var race = _raceManager.CurrentRace;
            var winnerHorse = race.Horses.FirstOrDefault(h => h.Id == winnerHorseId);

            foreach (var bet in _bets.Where(b => !b.IsSettled))
            {
                if (bet.HorseId == winnerHorseId)
                {
                    bet.IsWon = true;
                    bet.Payout = bet.Amount * (winnerHorse?.Odds ?? 1.0m);
                }
                bet.IsSettled = true;
            }
        }
    }

    public List<Bet> GetAllBets()
    {
        lock (_betLock)
        {
            return _bets.ToList();
        }
    }
}
