namespace BeNGy.Models;

public class BranchInput
{
    public string Prefix { get; set; } = "feature";
    public string TicketNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string TitleFormat { get; set; } = "kebab-case";
    public string PrefixTicketSeparator { get; set; } = "/";
    public string TicketTitleSeparator { get; set; } = "_";
}
