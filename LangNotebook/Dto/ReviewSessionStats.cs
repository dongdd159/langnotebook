namespace LangNotebook.Dto;

public sealed class ReviewSessionStats
{
    public int Total { get; set; }
    public int Done { get; set; }
    public int Again { get; set; }
    public int Hard { get; set; }
    public int Good { get; set; }
    public int Easy { get; set; }
    public int Remaining => Math.Max(0, Total - Done);
    public double Accuracy => Done == 0 ? 0 : (double)(Done - Again) / Done;
}