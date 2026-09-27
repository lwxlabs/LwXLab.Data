namespace MultiDatabase.Api.Models;

public sealed class AuditEntry
{
    public long Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public System.DateTimeOffset CreatedAt { get; set; }
}
