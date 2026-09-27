using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using LwXLab.Data.EntityFrameworkCore;

namespace LwXLab.Data.AI;

public sealed class LwXDataAgentManifest
{
    public int Version { get; set; } = 1;
    public List<LwXDataAgentDatabase> Databases { get; set; } = new List<LwXDataAgentDatabase>();

    public string ToJson(bool indented = true) => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = indented });
    public void Save(string path) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); File.WriteAllText(path, ToJson(true)); }

    public static LwXDataAgentManifest FromDescriptors(IEnumerable<ILwXDbContextDescriptor> descriptors)
    {
        var manifest = new LwXDataAgentManifest();
        foreach (var d in descriptors)
            manifest.Databases.Add(new LwXDataAgentDatabase { Name = d.Name, Context = d.ContextType.FullName ?? d.ContextType.Name, Provider = d.ProviderName, Schema = d.Schema, MigrationsHistoryTable = d.MigrationsHistoryTable });
        return manifest;
    }
}

public sealed class LwXDataAgentDatabase
{
    public string Name { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string? Schema { get; set; }
    public string MigrationsHistoryTable { get; set; } = string.Empty;
}
