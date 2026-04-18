namespace LocalMovieVault.Web.Contracts;

public class ImportSummary
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public List<string> Messages { get; set; } = [];
}
