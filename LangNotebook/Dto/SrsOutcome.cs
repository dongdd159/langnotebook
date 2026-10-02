namespace LangNotebook.Dto;

public readonly record struct SrsOutcome(
    double EaseFactor,
    int IntervalDays,
    int Repetitions,
    int Lapses,
    DateTime DueUtc);