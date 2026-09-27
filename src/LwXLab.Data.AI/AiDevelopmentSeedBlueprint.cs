using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace LwXLab.Data.AI;

public sealed class AiSeedEntityBlueprint
{
    public string Entity { get; set; } = string.Empty;
    public List<string> RequiredProperties { get; set; } = new List<string>();
}

public static class AiDevelopmentSeedBlueprint
{
    /// <summary>Builds metadata for generating synthetic development seeds. It does not read production rows and does not insert data.</summary>
    public static IReadOnlyList<AiSeedEntityBlueprint> Create(DbContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        var result = new List<AiSeedEntityBlueprint>();
        foreach (var entity in context.Model.GetEntityTypes())
        {
            var item = new AiSeedEntityBlueprint { Entity = entity.ClrType.Name };
            foreach (var property in entity.GetProperties())
                if (!property.IsNullable && property.ValueGenerated == Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never)
                    item.RequiredProperties.Add(property.Name + ":" + property.ClrType.Name);
            result.Add(item);
        }
        return result;
    }
}
