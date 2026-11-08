using LangNotebook.Dto;
using LangNotebook.Helper;
using LangNotebook.Service;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace LangNotebook.Pages.Writing;

public partial class WritingAiReviewPanel : ComponentBase
{
    private static readonly (string Key, string Label)[] Categories =
    [
        ("grammar", "Grammar"),
        ("vocabulary", "Vocabulary"),
        ("phrasing", "Phrasing")
    ];

    [Parameter] public WritingAiReview? Review { get; set; }
    [Parameter] public bool IsBusy { get; set; }
    [Parameter] public AiReviewException? Error { get; set; }
    [Parameter] public bool IsStale { get; set; }
    [Parameter] public bool CanEvaluate { get; set; }
    [Parameter] public EventCallback OnEvaluate { get; set; }
    [Parameter] public EventCallback<string> OnApply { get; set; }
    [Parameter] public EventCallback OnDecisionChanged { get; set; }

    private bool _showFinal;
    private string? _filter;
    private int? _selected;
    private bool _wasBusy;

    private int AcceptedCount => Review?.Changes.Count(x => x.Decision == AiChangeDecision.Accepted) ?? 0;

    private bool HasApplicableChanges => Review?.Changes.Any(x => x.Decision != AiChangeDecision.Skipped) ?? false;

    private string FinalText => Review is null
        ? string.Empty
        : WritingAiReviewMapper.ApplyChanges(Review, x => x.Decision != AiChangeDecision.Skipped);

    private string ScoreTone => Review?.TotalScore switch
    {
        >= 85 => "nt-tag-4",
        >= 70 => "nt-tag-5",
        >= 50 => "nt-tag-2",
        _ => "nt-tag-8"
    };

    private string FooterNote
    {
        get
        {
            if (IsStale)
                return "The article has changed since this review. Re-evaluate to score the latest version and apply suggestions.";
            var note = $"{AcceptedCount} of {Review?.Changes.Count} accepted. Applying replaces the text in the editor.";
            return Review?.UnmatchedChanges > 0
                ? $"{note} {Review.UnmatchedChanges} suggestion(s) were left out because they couldn't be matched to your text."
                : note;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (IsBusy && !_wasBusy)
            await JS.InvokeVoidAsync("langDb.scrollIntoView", "ai-review", "start");
        _wasBusy = IsBusy;
    }

    private static string TabClass(bool active) =>
        "h-6 cursor-pointer rounded-nt border-0 px-2.5 text-xs font-medium " +
        (active ? "bg-nt-bg text-ink shadow-card" : "bg-transparent text-mute");

    private string CardClass(int index, WritingAiChange change)
    {
        if (_selected == index)
            return "cursor-pointer bg-nt-accent-soft [box-shadow:inset_0_0_0_2px_var(--nt-accent)]";
        return change.Decision == AiChangeDecision.Pending
            ? "cursor-pointer border border-line bg-nt-bg"
            : "cursor-pointer border border-line bg-nt-side";
    }

    private string BadgeClass(int index) =>
        "inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-full text-xs font-medium " +
        (_selected == index ? "bg-nt-accent text-white" : "bg-nt-accent-soft text-[color:var(--nt-accent-hover)]");

    private static string CategoryLabel(string category) =>
        Categories.FirstOrDefault(x => x.Key == category).Label ?? category;

    private static string CategoryTone(string category) => category switch
    {
        "grammar" => "nt-tag-8",
        "vocabulary" => "nt-tag-6",
        "phrasing" => "nt-tag-3",
        _ => "nt-tag-0"
    };

    private async Task Decide(WritingAiChange change, AiChangeDecision decision)
    {
        change.Decision = decision;
        await OnDecisionChanged.InvokeAsync();
    }

    private async Task SelectFromTextAsync(int index)
    {
        _selected = index;
        _filter = null;
        StateHasChanged();
        await Task.Yield();
        await JS.InvokeVoidAsync("langDb.scrollIntoView", $"ai-change-{index}");
    }

    private async Task ApplyAsync(Func<WritingAiChange, bool> include)
    {
        if (Review is null)
            return;
        await OnApply.InvokeAsync(WritingAiReviewMapper.ApplyChanges(Review, include));
    }

    private async Task CopyAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("langDb.copyText", FinalText);
            State.ShowSuccess("Copied the improved article.");
        }
        catch (JSException)
        {
            State.ShowError("Couldn't copy to the clipboard.");
        }
    }

    // Built in code because the paragraph uses pre-wrap: whitespace between markup tags would show up in the article.
    private void RenderDiff(RenderTreeBuilder builder)
    {
        if (Review is null)
            return;
        var source = Review.SourceText;
        var cursor = 0;
        for (var i = 0; i < Review.Changes.Count; i++)
        {
            var index = i;
            var change = Review.Changes[i];
            builder.AddContent(0, source[cursor..change.Start]);
            if (change.Decision == AiChangeDecision.Skipped)
                builder.AddContent(1, change.Original);
            else
            {
                builder.OpenElement(2, "del");
                builder.AddAttribute(3, "class", "text-[color:var(--diff-removed)] line-through");
                builder.AddContent(4, change.Original);
                builder.CloseElement();
                builder.AddContent(5, " ");
                builder.OpenElement(6, "ins");
                builder.AddAttribute(7, "class", "font-medium text-[color:var(--diff-added)] underline");
                builder.AddContent(8, change.Improved);
                builder.CloseElement();
            }
            builder.OpenElement(9, "button");
            builder.AddAttribute(10, "type", "button");
            builder.AddAttribute(11, "class",
                "ml-1 cursor-pointer border-0 bg-transparent p-0 align-baseline text-xs font-semibold text-[color:var(--nt-accent-hover)] hover:underline");
            builder.AddAttribute(12, "aria-label", $"Show explanation for change {index + 1}");
            builder.AddAttribute(13, "onclick", EventCallback.Factory.Create(this, () => SelectFromTextAsync(index)));
            builder.AddContent(14, $"[{index + 1}]");
            builder.CloseElement();
            cursor = change.Start + change.Original.Length;
        }
        builder.AddContent(15, source[cursor..]);
    }
}
