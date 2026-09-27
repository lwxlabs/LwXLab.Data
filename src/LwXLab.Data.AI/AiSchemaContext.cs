using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LwXLab.Data.AI;

public sealed class AiSchemaContext
{
    public string Provider { get; set; } = string.Empty;
    public string? Schema { get; set; }
    public List<AiEntityContext> Entities { get; set; } = new List<AiEntityContext>();
    public string ToJson(bool indented = true) => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = indented });
}

public sealed class AiEntityContext
{
    public string Name { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public List<AiPropertyContext> Properties { get; set; } = new List<AiPropertyContext>();
    public List<string> Relationships { get; set; } = new List<string>();
}

public sealed class AiPropertyContext
{
    public string Name { get; set; } = string.Empty;
    public string Column { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Nullable { get; set; }
    public bool PrimaryKey { get; set; }
}

public static class AiSchemaContextFactory
{
    /// <summary>Creates metadata-only AI context. It never includes connection strings, passwords, or row data.</summary>
    public static AiSchemaContext Create(DbContext context, string provider, string? schema = null)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        var result = new AiSchemaContext { Provider = provider, Schema = schema };
        foreach (var entity in context.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table == null) continue;
            var item = new AiEntityContext { Name = entity.ClrType.Name, Table = table };
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema() ?? schema);
            var primary = entity.FindPrimaryKey();
            foreach (var property in entity.GetProperties())
            {
                item.Properties.Add(new AiPropertyContext
                {
                    Name = property.Name,
                    Column = property.GetColumnName(store) ?? property.Name,
                    Type = property.GetColumnType() ?? property.ClrType.Name,
                    Nullable = property.IsNullable,
                    PrimaryKey = primary != null && primary.Properties.Contains(property)
                });
            }
            foreach (var fk in entity.GetForeignKeys())
                item.Relationships.Add(entity.ClrType.Name + " -> " + fk.PrincipalEntityType.ClrType.Name + " (" + string.Join(",", fk.Properties.Select(x => x.Name)) + ")");
            result.Entities.Add(item);
        }
        return result;
    }
}
