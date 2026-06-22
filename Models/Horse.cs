namespace HorseRacing.Models;

public class Horse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Position { get; set; }
    public double Speed { get; set; }
    public bool IsWinner { get; set; }
    public decimal Odds { get; set; }
}
