namespace LangNotebook.Dto;

public readonly record struct SrsOutcome(
    int IntervalDays,
    int Repetitions,
    int Lapses,
    DateTime DueUtc,
    double Stability,
    double Difficulty);