using System.Collections.Generic;
using System.Text;

namespace LwXLab.Data.Tooling;

public sealed class DoctorReport
{
    public string Provider { get; set; } = string.Empty;
    public string SafeConnectionString { get; set; } = string.Empty;
    public List<string> Lines { get; } = new List<string>();
    public string ToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("LwXLab.Data Doctor");
        sb.AppendLine("Provider: " + Provider);
        sb.AppendLine("Connection: " + SafeConnectionString);
        foreach (var line in Lines) sb.AppendLine(line);
        return sb.ToString();
    }
}
