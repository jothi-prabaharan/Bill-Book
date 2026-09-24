using Master.Api.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// The application creates databases in Development only (D-02, TK-27). Every
/// other environment finds them made by infrastructure, and a missing one
/// stops startup with the fix in the message.
/// </summary>
public sealed class DatabaseCreationPolicyTests
{
    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Master.Api";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    [InlineData("SelfHosted", false)]
    [InlineData("Staging", false)]
    public void Only_development_may_create_a_database(string environment, bool mayCreate)
    {
        Assert.Equal(mayCreate, DatabaseMigrationService.MayCreateDatabases(new Environment(environment)));
    }

    [Fact]
    public void A_missing_database_names_itself_its_server_and_where_it_is_created()
    {
        string message = DatabaseMigrationService.MissingDatabaseMessage(
            "Host=pg.example.net;Port=5432;Database=IN000001;Username=app;Password=secret", "Production");

        Assert.Contains("\"IN000001\"", message);
        Assert.Contains("pg.example.net", message);
        Assert.Contains("deploy/azure/main.bicep", message);
        Assert.Contains("deploy/local/db/init", message);
        Assert.DoesNotContain("secret", message);
    }

    [Fact]
    public void The_single_pc_install_creates_both_databases_itself()
    {
        string root = RepositoryRoot();
        string script = File.ReadAllText(Path.Combine(root, "deploy", "local", "db", "init", "01-create-databases.sql"));
        string compose = File.ReadAllText(Path.Combine(root, "deploy", "local", "docker-compose.yml"));

        Assert.Contains("CREATE DATABASE \"EP_Admin\"", script);
        Assert.Contains("CREATE DATABASE \"IN000001\"", script);
        Assert.Contains("./db/init:/docker-entrypoint-initdb.d", compose);
    }

    [Fact]
    public void Azure_declares_both_databases()
    {
        string bicep = File.ReadAllText(Path.Combine(RepositoryRoot(), "deploy", "azure", "main.bicep"));

        Assert.Contains("name: 'EP_Admin'", bicep);
        Assert.Contains("name: 'IN000001'", bicep);
    }

    private static string RepositoryRoot()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "deploy")) && Directory.Exists(Path.Combine(dir.FullName, "backend")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("The repository root was not found above the test output.");
    }
}
