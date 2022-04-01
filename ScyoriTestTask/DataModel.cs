namespace ScyoriTestTask;

internal class DataModel
{
    public string? DatabaseConnectionString { get; set; }
    public string? WebsiteConnectionString { get; set; }
    public string[]? EmailAdresses { get; set; }
    public string? ConnectionResultsPath { get; set; }
    public string? EmailLogin { get; set; }
    public string? EmailPassword { get; set; }
    public string? SmtpServer { get; set; }
    public int SmtpPort { get; set; }
}
