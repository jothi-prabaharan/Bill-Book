using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Messaging;
using Shared.Kernel.Secrets;
using Shared.Kernel.Storage;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// Which implementation each infrastructure interface resolves to.
/// </summary>
public class InfrastructureRegistrationTests
{
    private static IConfiguration Config(params (string Key, string Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();

    private static ServiceCollection Services(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddSingleton(TimeProvider.System);
        services.AddLogging();
        return services;
    }

    // ------------------------------------------------------------ events

    [Fact]
    public void Event_publisher_registers_logging_stand_in()
    {
        IConfiguration config = Config();

        using ServiceProvider provider = Services(config).AddEventPublisher(config).BuildServiceProvider();

        Assert.IsType<LoggingEventPublisher>(provider.GetRequiredService<IEventPublisher>());
    }

    // ------------------------------------------------------------ files

    [Fact]
    public void No_storage_setting_falls_back_to_local_disk()
    {
        IConfiguration config = Config(("FileStorage:LocalRoot", Path.Combine(Path.GetTempPath(), "bb-reg-test")));

        using ServiceProvider provider = Services(config).AddFileStorage(config).BuildServiceProvider();

        Assert.IsType<LocalDiskFileStorage>(provider.GetRequiredService<IFileStorage>());
    }

    [Fact]
    public void An_ftp_host_selects_the_ftp_store()
    {
        IConfiguration config = Config(
            ("Storage:Ftp:Host", "ftp.example.com"),
            ("Storage:Ftp:Username", "billbook"),
            ("Storage:Ftp:Password", "secret"));

        using ServiceProvider provider = Services(config).AddFileStorage(config).BuildServiceProvider();

        Assert.IsType<FtpFileStorage>(provider.GetRequiredService<IFileStorage>());
    }

    [Theory]
    [InlineData("Storage:Ftp:Username")]
    [InlineData("Storage:Ftp:Password")]
    public void An_ftp_host_without_credentials_is_refused_at_startup(string missing)
    {
        var settings = new List<(string, string)>
        {
            ("Storage:Ftp:Host", "ftp.example.com"),
            ("Storage:Ftp:Username", "billbook"),
            ("Storage:Ftp:Password", "secret"),
        };
        settings.RemoveAll(s => s.Item1 == missing);
        IConfiguration config = Config([.. settings]);

        var ex = Assert.Throws<InvalidOperationException>(() => Services(config).AddFileStorage(config));

        Assert.Contains(missing, ex.Message);
    }

    [Theory]
    [InlineData("billbook-files", "0000000042/a/b.pdf", "billbook-files/0000000042/a/b.pdf")]
    [InlineData("/srv/files/", "0000000042/a/b.pdf", "srv/files/0000000042/a/b.pdf")]
    [InlineData("", "0000000042/a/b.pdf", "0000000042/a/b.pdf")]
    public void A_key_lands_under_the_ftp_root(string root, string key, string expected) =>
        Assert.Equal(expected, FtpFileStorage.RemotePath(root, key));

    [Theory]
    [InlineData("../other/x.pdf")]
    [InlineData("a/../../x.pdf")]
    [InlineData("/etc/passwd")]
    [InlineData("a\\..\\x.pdf")]
    [InlineData("")]
    public void A_key_that_could_leave_the_ftp_root_is_refused(string key) =>
        Assert.Throws<InvalidOperationException>(() => FtpFileStorage.RemotePath("billbook-files", key));

    [Fact]
    public void An_sftp_host_selects_the_sftp_store()
    {
        IConfiguration config = Config(
            ("Storage:Sftp:Host", "files-pc"),
            ("Storage:Sftp:Username", "billbook"),
            ("Storage:Sftp:Password", "secret"));

        using ServiceProvider provider = Services(config).AddFileStorage(config).BuildServiceProvider();

        Assert.IsType<SftpFileStorage>(provider.GetRequiredService<IFileStorage>());
    }

    [Theory]
    [InlineData("Storage:Sftp:Username")]
    [InlineData("Storage:Sftp:Password")]
    public void An_sftp_host_without_credentials_is_refused_at_startup(string missing)
    {
        var settings = new List<(string, string)>
        {
            ("Storage:Sftp:Host", "files-pc"),
            ("Storage:Sftp:Username", "billbook"),
            ("Storage:Sftp:Password", "secret"),
        };
        settings.RemoveAll(s => s.Item1 == missing);
        IConfiguration config = Config([.. settings]);

        var ex = Assert.Throws<InvalidOperationException>(() => Services(config).AddFileStorage(config));

        Assert.Contains(missing, ex.Message);
    }

    [Theory]
    [InlineData("billbook-files", "0000000042/a/b.pdf", "billbook-files/0000000042/a/b.pdf")]
    [InlineData("/srv/files/", "0000000042/a/b.pdf", "/srv/files/0000000042/a/b.pdf")]
    [InlineData("", "0000000042/a/b.pdf", "0000000042/a/b.pdf")]
    public void A_key_lands_under_the_sftp_root(string root, string key, string expected) =>
        Assert.Equal(expected, SftpFileStorage.RemotePath(root, key));

    [Theory]
    [InlineData("../other/x.pdf")]
    [InlineData("a/../../x.pdf")]
    [InlineData("/etc/passwd")]
    [InlineData("a\\..\\x.pdf")]
    [InlineData("a//x.pdf")]
    [InlineData("")]
    public void A_key_that_could_leave_the_sftp_root_is_refused(string key) =>
        Assert.Throws<InvalidOperationException>(() => SftpFileStorage.RemotePath("billbook-files", key));

    // ------------------------------------------------------------ secrets

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public void Secret_store_registers_configuration_store_in_production()
    {
        IConfiguration config = Config(("Secrets:TestKey", "secret-val"));

        using ServiceProvider provider = Services(config)
            .AddSecretStore(config, new TestEnvironment(Environments.Production))
            .BuildServiceProvider();

        Assert.IsType<ConfigurationSecretStore>(provider.GetRequiredService<ISecretStore>());
    }

    [Fact]
    public void Secret_store_registers_configuration_store_in_development()
    {
        IConfiguration config = Config();

        using ServiceProvider provider = Services(config)
            .AddSecretStore(config, new TestEnvironment(Environments.Development))
            .BuildServiceProvider();

        Assert.IsType<ConfigurationSecretStore>(provider.GetRequiredService<ISecretStore>());
    }
}
