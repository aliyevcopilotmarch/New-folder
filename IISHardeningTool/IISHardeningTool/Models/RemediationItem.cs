namespace IISHardeningTool.Models;

public enum ComplianceStatus
{
    Unknown,
    Compliant,
    NonCompliant,
    Error,
    Fixed
}

public class RemediationItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public string CisBenchmark { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ComplianceStatus Status { get; set; } = ComplianceStatus.Unknown;
    public string StatusMessage { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}
