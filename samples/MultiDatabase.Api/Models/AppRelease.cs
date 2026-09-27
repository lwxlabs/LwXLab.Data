namespace MultiDatabase.Api.Models;

public sealed class AppRelease
{
    public long Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public System.DateTimeOffset PublishedAt { get; set; }
}
