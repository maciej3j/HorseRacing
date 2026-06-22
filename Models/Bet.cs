namespace HorseRacing.Models;

public class Bet
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int HorseId { get; set; }
    public decimal Amount { get; set; }
    public bool IsWon { get; set; }
    public decimal Payout { get; set; }
    public bool IsSettled { get; set; }
}
