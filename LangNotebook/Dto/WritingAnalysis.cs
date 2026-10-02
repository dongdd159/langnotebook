namespace LangNotebook.Dto;

public class WritingAnalysis
{
    public int WordCount { get; init; }
    public int CharacterCount { get; init; }
    public int SentenceCount { get; init; }
    public int ParagraphCount { get; init; }
    public double AverageSentenceLength { get; init; }
    public int LongestSentenceLength { get; init; }
    public double LexicalDiversity { get; init; }
    public List<string> OverusedWords { get; init; } = new();
    public List<string> UsedTargetTerms { get; init; } = new();
    public List<string> MissingTargetTerms { get; init; } = new();
    public List<WritingFeedback> Feedback { get; init; } = new();
    public int ScoreLength { get; init; }
    public int ScoreVariety { get; init; }
    public int ScoreDiversity { get; init; }
    public int ScoreTargetUsage { get; init; }
    public int TotalScore => ScoreLength + ScoreVariety + ScoreDiversity + ScoreTargetUsage;
    public bool IsCjk { get; init; }
}