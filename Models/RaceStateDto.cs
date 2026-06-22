namespace HorseRacing.Models;

public class RaceStateDto
{
    public string Status { get; set; } = string.Empty;
    public List<HorseDto> Horses { get; set; } = new();
    public int? WinnerId { get; set; }
}

public class HorseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Position { get; set; }
    public decimal Odds { get; set; }
}
