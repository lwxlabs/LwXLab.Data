using System;
using LwXLab.Data.CockroachDb;
using LwXLab.Data.MariaDb;
using LwXLab.Data.MySql;
using LwXLab.Data.Oracle;
using LwXLab.Data.PostgreSql;
using LwXLab.Data.Sqlite;
using LwXLab.Data.SqlServer;
using LwXLab.Data.Supabase;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using Xunit;

namespace LwXLab.Data.Tests;

public sealed class ProviderTests
{
    [Fact]
    public void PostgreSql_EnforcesSslWithoutLeakingPasswordFromSafeDescription()
    {
        var options = new PostgreSqlDatabaseOptions("Host=localhost;Database=test;Username=user;Password=super-secret;SSL Mode=Disable");
        var normalized = PostgreSqlConnectionString.Create(options);
        var builder = new NpgsqlConnectionStringBuilder(normalized);

        Assert.Equal(SslMode.Require, builder.SslMode);
        Assert.DoesNotContain("super-secret", PostgreSqlConnectionString.DescribeSafely(options));
    }

    [Fact]
    public void SqlServer_DefaultsToEncryptionAndNoTrustedCertificateBypass()
    {
        var options = new SqlServerDatabaseOptions("Server=localhost;Database=test;User Id=user;Password=super-secret;Encrypt=False;TrustServerCertificate=True");
        var normalized = SqlServerConnectionString.Create(options);
        var builder = new SqlConnectionStringBuilder(normalized);

        var encrypt = builder["Encrypt"].ToString();
        Assert.True(
            string.Equals(encrypt, "True", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(encrypt, "Mandatory", StringComparison.OrdinalIgnoreCase));
        Assert.False(builder.TrustServerCertificate);
        Assert.DoesNotContain("super-secret", SqlServerConnectionString.DescribeSafely(options));
    }

    [Fact]
    public void Supabase_DetectsTransactionPooler_AndDisablesAutoPrepare()
    {
        var options = new SupabaseDatabaseOptions(
            "Host=aws-0-eu-central-1.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.project;Password=secret;SSL Mode=Require;Max Auto Prepare=10");

        var normalized = SupabaseConnectionString.Create(options);
        var builder = new NpgsqlConnectionStringBuilder(normalized);

        Assert.Equal(SupabaseConnectionMode.TransactionPooler, SupabaseConnectionString.Detect(normalized));
        Assert.Equal(0, builder.MaxAutoPrepare);
    }

    [Fact]
    public void Sqlite_ProvidesPasswordSafeDescription()
    {
        var options = new SqliteDatabaseOptions("Data Source=test.db;Password=super-secret");
        var normalized = SqliteConnectionString.Create(options);
        var builder = new SqliteConnectionStringBuilder(normalized);

        Assert.Equal("test.db", builder.DataSource);
        Assert.DoesNotContain("super-secret", SqliteConnectionString.DescribeSafely(options));
    }

    [Fact]
    public void MySql_EnforcesRequiredTlsAndHidesPassword()
    {
        var options = new MySqlDatabaseOptions(
            "Server=localhost;Database=test;User Id=user;Password=super-secret;SslMode=Disabled");

        var normalized = MySqlConnectionString.Create(options);
        var builder = new global::MySql.Data.MySqlClient.MySqlConnectionStringBuilder(normalized);

        Assert.Equal(global::MySql.Data.MySqlClient.MySqlSslMode.Required, builder.SslMode);
        Assert.DoesNotContain("super-secret", MySqlConnectionString.DescribeSafely(options));
    }

    [Fact]
    public void MariaDb_EnforcesRequiredTlsAndHidesPassword()
    {
        var options = new MariaDbDatabaseOptions(
            "Server=localhost;Database=test;User Id=user;Password=super-secret;SslMode=None");

        var normalized = MariaDbConnectionString.Create(options);
        var builder = new MySqlConnector.MySqlConnectionStringBuilder(normalized);

        Assert.Equal(MySqlConnector.MySqlSslMode.Required, builder.SslMode);
        Assert.DoesNotContain("super-secret", MariaDbConnectionString.DescribeSafely(options));
    }

    [Fact]
    public void CockroachDb_UsesPostgreSqlSecurityDefaults()
    {
        var options = new CockroachDbDatabaseOptions(
            "Host=localhost;Database=defaultdb;Username=root;Password=super-secret;SSL Mode=Disable");

        var normalized = CockroachDbConnectionString.Create(options);
        var builder = new NpgsqlConnectionStringBuilder(normalized);

        Assert.Equal(SslMode.Require, builder.SslMode);
        Assert.DoesNotContain("super-secret", CockroachDbConnectionString.DescribeSafely(options));
    }

    [Fact]
    public void Oracle_HidesPasswordAndAppliesPoolOptions()
    {
        var options = new OracleDatabaseOptions(
            "User Id=user;Password=super-secret;Data Source=localhost/XEPDB1")
        {
            MinimumPoolSize = 1,
            MaximumPoolSize = 8
        };

        var normalized = OracleConnectionString.Create(options);
        var builder = new OracleConnectionStringBuilder(normalized);

        Assert.Equal(1, builder.MinPoolSize);
        Assert.Equal(8, builder.MaxPoolSize);
        Assert.DoesNotContain("super-secret", OracleConnectionString.DescribeSafely(options));
    }
}
