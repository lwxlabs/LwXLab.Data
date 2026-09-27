using System;

namespace LwXLab.Data.EntityFrameworkCore;

public interface ILwXDbContextDescriptor
{
    Type ContextType { get; }
    string Name { get; }
    string ProviderName { get; }
    string? Schema { get; }
    bool HasSchema { get; }
    string MigrationsHistoryTable { get; }
}
