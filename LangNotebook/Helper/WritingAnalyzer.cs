using System.Globalization;
using System.Text.RegularExpressions;
using LangNotebook.Dto;

namespace LangNotebook.Service;

public class WritingAnalyzer
{
    private static readonly Regex WordRegex = new(@"[\p{L}\p{N}']+", RegexOptions.Compiled);
    private static readonly Regex SentenceRegex = new(@"[^.!?。！？\n]+[.!?。！？]?", RegexOptions.Compiled);

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "that", "this", "with", "have", "has", "for", "are", "was", "were", "you", "your", "但是", "です",
        "ます", "から", "こと", "する", "した", "một", "những", "được", "trong", "cũng", "rằng", "của"
    };

    public static WritingAnalysis Analyze(string content, int targetWordCount, IEnumerable<string>? targetTerms = null)
    {
        content ??= string.Empty;
        var trimmed = content.Trim();
        var terms = (targetTerms ?? Enumerable.Empty<string>()).Select(x => x.Trim()).Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (trimmed.Length == 0)
            return new WritingAnalysis
            {
                MissingTargetTerms = terms, Feedback = new List<WritingFeedback>
                {
                    new() { Kind = "info", Message = "Start writing to receive automatic feedback." }
                }
            };
        var isCjk = DetectCjk(trimmed);
        var words = WordRegex.Matches(trimmed).Select(m => m.Value).ToList();
        var wordCount = isCjk ? trimmed.Count(ch => !char.IsWhiteSpace(ch)) : words.Count;
        var sentences = SentenceRegex.Matches(trimmed).Select(m => m.Value.Trim()).Where(s => s.Length > 0).ToList();
        var sentenceLengths = sentences
            .Select(s => isCjk ? s.Count(ch => !char.IsWhiteSpace(ch)) : WordRegex.Matches(s).Count)
            .Where(len => len > 0).ToList();
        var paragraphs = trimmed.Split('\n', StringSplitOptions.RemoveEmptyEntries).Count(p => p.Trim().Length > 0);
        var avgSentence = sentenceLengths.Count == 0 ? 0 : sentenceLengths.Average();
        var longest = sentenceLengths.Count == 0 ? 0 : sentenceLengths.Max();
        var normalized = words.Select(w => w.ToLowerInvariant()).ToList();
        var diversity = normalized.Count == 0 ? 0 : (double)normalized.Distinct().Count() / normalized.Count;
        var overused = normalized.Where(w => w.Length > 3 && !StopWords.Contains(w)).GroupBy(w => w)
            .Where(g => g.Count() >= 4).OrderByDescending(g => g.Count()).Take(5).Select(g => $"{g.Key} ({g.Count()}×)")
            .ToList();
        var used = terms.Where(t => trimmed.Contains(t, StringComparison.OrdinalIgnoreCase)).ToList();
        var missing = terms.Except(used, StringComparer.OrdinalIgnoreCase).ToList();
        var target = targetWordCount <= 0 ? 150 : targetWordCount;
        var ratio = (double)wordCount / target;
        var scoreLength = ratio switch
        {
            >= 0.9 and <= 1.25 => 30,
            >= 0.75 and < 0.9 => 24,
            > 1.25 and <= 1.5 => 24,
            >= 0.5 and < 0.75 => 16,
            > 1.5 => 16,
            _ => 8
        };
        var variety = ComputeVarietyScore(sentenceLengths, isCjk);
        var scoreDiversity = diversity switch
        {
            >= 0.62 => 25,
            >= 0.52 => 20,
            >= 0.42 => 14,
            >= 0.32 => 8,
            _ => 4
        };
        var scoreTarget = terms.Count == 0 ? 25 : (int)Math.Round(25.0 * used.Count / terms.Count);
        var feedback = new List<WritingFeedback>();
        if (ratio < 0.85)
            feedback.Add(new WritingFeedback
            {
                Kind = "warn",
                Message =
                    $"The article is still short compared to your target ({wordCount}/{target}). Try adding specific examples or reasons."
            });
        else if (ratio > 1.4)
            feedback.Add(new WritingFeedback
            {
                Kind = "warn",
                Message =
                    $"The article is much longer than your target ({wordCount}/{target}). Try trimming redundant points."
            });
        else
            feedback.Add(new WritingFeedback
            {
                Kind = "good",
                Message = $"Good length: {wordCount}/{target} words."
            });

        if (longest > (isCjk ? 90 : 35))
            feedback.Add(new WritingFeedback
            {
                Kind = "warn",
                Message =
                    $"You have a sentence with up to {longest} words. Overly long sentences can disrupt the flow – consider splitting it into two."
            });

        if (sentenceLengths.Count >= 3 && StandardDeviation(sentenceLengths) < 2.5)
            feedback.Add(new WritingFeedback
            {
                Kind = "warn",
                Message =
                    "Your sentences are nearly uniform in length. Alternate between short and long sentences for a more natural style."
            });

        if (overused.Count > 0)
            feedback.Add(new WritingFeedback
            {
                Kind = "warn",
                Message = $"Frequently repeated words: {string.Join(", ", overused)}. Try replacing them with synonyms."
            });

        if (paragraphs <= 1 && wordCount > 120)
            feedback.Add(new WritingFeedback
            {
                Kind = "warn",
                Message =
                    "The article is currently a single block of text. Breaking it into paragraphs will make it easier to follow."
            });

        if (terms.Count > 0)
            feedback.Add(missing.Count == 0
                ? new WritingFeedback { Kind = "good", Message = "Used all target vocabulary. Great job!" }
                : new WritingFeedback
                {
                    Kind = "info",
                    Message = $"Not used yet: {string.Join(", ", missing)}."
                });

        if (diversity >= 0.6 && wordCount > 60)
            feedback.Add(new WritingFeedback
                { Kind = "good", Message = "Rich and varied vocabulary with minimal repetition." });
        return new WritingAnalysis
        {
            WordCount = wordCount, CharacterCount = trimmed.Length, SentenceCount = sentences.Count,
            ParagraphCount = paragraphs, AverageSentenceLength = Math.Round(avgSentence, 1),
            LongestSentenceLength = longest, LexicalDiversity = Math.Round(diversity, 3), OverusedWords = overused,
            UsedTargetTerms = used, MissingTargetTerms = missing, Feedback = feedback, ScoreLength = scoreLength,
            ScoreVariety = variety, ScoreDiversity = scoreDiversity, ScoreTargetUsage = scoreTarget, IsCjk = isCjk
        };
    }

    public static int CountWords(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return 0;
        return DetectCjk(content) ? content.Count(ch => !char.IsWhiteSpace(ch)) : WordRegex.Matches(content).Count;
    }

    public static string ScoreBand(int totalScore) => totalScore switch
    {
        >= 85 => "Excellent",
        >= 70 => "Good",
        >= 55 => "Average",
        >= 40 => "Needs Improvement",
        _ => "Beginner"
    };

    private static int ComputeVarietyScore(List<int> sentenceLengths, bool isCjk)
    {
        if (sentenceLengths.Count < 2)
            return 8;
        var sd = StandardDeviation(sentenceLengths);
        var threshold = isCjk ? 8.0 : 4.0;
        if (sd >= threshold * 1.5) return 20;
        if (sd >= threshold) return 17;
        if (sd >= threshold * 0.6) return 13;
        return 8;
    }

    private static double StandardDeviation(List<int> values)
    {
        if (values.Count < 2)
            return 0;
        var mean = values.Average();
        var variance = values.Sum(v => (v - mean) * (v - mean)) / values.Count;
        return Math.Sqrt(variance);
    }

    private static bool DetectCjk(string text)
    {
        var cjk = text.Count(ch =>
            (ch >= 0x3040 && ch <= 0x30FF) || (ch >= 0x4E00 && ch <= 0x9FFF) || (ch >= 0xAC00 && ch <= 0xD7AF));
        var letters = text.Count(char.IsLetter);
        return letters > 0 && (double)cjk / letters > 0.3;
    }

    public static string FormatPercent(double value) =>
        (value * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
}