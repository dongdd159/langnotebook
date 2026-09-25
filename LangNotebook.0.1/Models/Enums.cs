namespace LangNotebook.Models;

public enum StudyItemKind
{
    Vocabulary = 0,
    Grammar = 1
}
public enum ReviewGrade
{
    Again = 0,
    Hard = 1,
    Good = 2,
    Easy = 3
}
public enum LearningStage
{
    New = 0,
    Learning = 1,
    Young = 2,
    Mature = 3
}
public enum ReviewDirection
{
    TermToMeaning = 0,
    MeaningToTerm = 1,
    Mixed = 2
}
public enum ReviewScope
{
    All = 0,
    VocabularyOnly = 1,
    GrammarOnly = 2
}
public enum WritingStatus
{
    Draft = 0,
    Finished = 1
}
public enum ProficiencyLevel
{
    Beginner = 0,
    Elementary = 1,
    Intermediate = 2,
    UpperIntermediate = 3,
    Advanced = 4
}
