namespace HorseRacing.Models;

public enum RaceStatus
{
    Waiting,
    Running,
    Finished
}

public class Race
{
    public int Id { get; set; }
    public RaceStatus Status { get; set; } = RaceStatus.Waiting;
    public List<Horse> Horses { get; set; } = new();
    public DateTime? StartTime { get; set; }
    public int? WinnerHorseId { get; set; }
}
