using System.ComponentModel.DataAnnotations;

namespace ChessMultitool.Models;

class Game
{
    [Key] // Clé primaire
    public int Id { get; set; }

    public string? Event { get; set; }

    public string? Site { get; set; }

    public DateTime Date { get; set; }

    public string? Round { get; set; }

    public string? White { get; set; }

    public string? Black { get; set; }

    public string? Result { get; set; }

    public int? WhiteElo { get; set; }

    public int? BlackElo { get; set; }

    public int? PlayerElo { get; set; }

    public string? TimeControl { get; set; }

    public string? Category { get; set; }

    public TimeSpan EndTime { get; set; }

    public string? Termination { get; set; }

    public string? Moves { get; set; }

    public string? PlayerUsername { get; set; }

    public string? ResultForPlayer { get; set; }

    public string? EndOfGameBy { get; set; }

    public double? Accuracy { get; set; }

    public string? Opening { get; set; }

    public string? Eco { get; set; }

    public DateTime DateAndEndTime { get; set; }

    public string CastlingSameOrOppositeSide { get; set; }

}
