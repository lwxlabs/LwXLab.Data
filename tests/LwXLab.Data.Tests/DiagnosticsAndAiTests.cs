using LwXLab.Data;
using LwXLab.Data.AI;
using LwXLab.Data.Diagnostics;
using Xunit;

namespace LwXLab.Data.Tests;

public sealed class DiagnosticsAndAiTests
{
    [Fact]
    public void Redactor_RemovesPasswords()
    {
        var value = ConnectionStringRedactor.Redact("Host=localhost;Database=x;Username=u;Password=secret");
        Assert.DoesNotContain("secret", value);
        Assert.Contains("********", value);
    }

    [Fact]
    public void Diagnostics_Defaults_AreNonBlocking()
    {
        var options = new LwXDataDiagnosticsOptions();
        Assert.False(options.FailStartupOnDiagnosticError);
        Assert.False(options.EnableEnvironmentProtection);
        Assert.False(options.EnableMigrationSafety);
        Assert.False(options.EnableQueryBudget);
        Assert.False(options.ThrowWhenQueryBudgetExceeded);
    }

    [Fact]
    public void ConnectionIntelligence_DetectsSupabaseTransactionPooler()
    {
        var advice = ConnectionIntelligence.Analyze("Supabase", "Host=aws-0-eu.pooler.supabase.com;Port=6543;Database=postgres;Username=u;Password=p;SSL Mode=Require");
        Assert.Contains(advice, x => x.Code == "supabase.pooler.transaction");
    }
}
