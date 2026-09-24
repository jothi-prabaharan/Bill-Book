using System.Text.Json;
using Azure.Identity;
using Azure.Storage.Blobs;
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
/// Which implementation each infrastructure interface resolves to, and the shape
/// of what the event publisher puts on the wire.
///
/// <b>The selection is the whole deployment contract.</b> A deployment does not
/// choose an implementation; it sets a setting, and these registrations turn
/// that into a class. Getting one wrong fails at the first request that needs it
/// — a secret read, an upload, an event — which is after the deploy reported
/// success. So the mapping from setting to class is asserted here, where it
/// fails at build time instead.
///
/// Nothing here touches a network. Every client below is constructed and never
/// used, which Azure's SDKs allow: they connect lazily.
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
    public async Task A_service_bus_namespace_selects_the_service_bus_publisher()
    {
        IConfiguration config = Config(("ServiceBus:Namespace", "bb-test.servicebus.windows.net"));

        // await using, not using: the publisher is IAsyncDisposable only, and a
        // container disposed synchronously throws on it. The web host disposes
        // asynchronously, so this matters to tests and not to production — but
        // it is the kind of thing that turns a green suite red for no reason.
        await using ServiceProvider provider = Services(config).AddEventPublisher(config).BuildServiceProvider();

        Assert.IsType<ServiceBusEventPublisher>(provider.GetRequiredService<IEventPublisher>());
    }

    [Fact]
    public async Task No_namespace_falls_back_to_the_logging_stand_in()
    {
        IConfiguration config = Config();

        await using ServiceProvider provider = Services(config).AddEventPublisher(config).BuildServiceProvider();

        Assert.IsType<LoggingEventPublisher>(provider.GetRequiredService<IEventPublisher>());
    }

    [Fact]
    public void A_topic_is_named_after_the_event_type_and_prefixed_only_when_asked()
    {
        Assert.Equal("CustomerProvisioned", ServiceBusEventPublisher.TopicFor("CustomerProvisioned", null));
        Assert.Equal("CustomerProvisioned", ServiceBusEventPublisher.TopicFor("CustomerProvisioned", ""));
        Assert.Equal("staging-CustomerProvisioned", ServiceBusEventPublisher.TopicFor("CustomerProvisioned", "staging"));
    }

    private sealed record SampleEvent(Guid CustomerId, string CustomerCode);

    [Fact]
    public void Every_message_carries_a_fresh_id_for_consumers_to_dedupe_on()
    {
        var @event = new SampleEvent(Guid.NewGuid(), "000042");

        var first = ServiceBusEventPublisher.BuildMessage(@event);
        var second = ServiceBusEventPublisher.BuildMessage(@event);

        // The same event published twice is two messages, not one: the id marks
        // a send, and the broker's duplicate detection must only collapse a
        // retry of the same send.
        Assert.Matches("^[0-9a-f]{32}$", first.MessageId);
        Assert.NotEqual(first.MessageId, second.MessageId);
    }

    [Fact]
    public void A_message_says_what_it_is_without_being_opened()
    {
        var message = ServiceBusEventPublisher.BuildMessage(new SampleEvent(Guid.NewGuid(), "000042"));

        Assert.Equal("application/json", message.ContentType);
        Assert.Equal(nameof(SampleEvent), message.Subject);
        Assert.Equal(nameof(SampleEvent), message.ApplicationProperties["eventType"]);
        Assert.True(DateTimeOffset.TryParse((string)message.ApplicationProperties["publishedAt"], out _));
    }

    [Fact]
    public void The_body_is_camel_case_json_that_round_trips()
    {
        var customerId = Guid.NewGuid();
        var message = ServiceBusEventPublisher.BuildMessage(new SampleEvent(customerId, "000042"));

        using JsonDocument body = JsonDocument.Parse(message.Body.ToString());

        // camelCase is the wire convention everywhere else in this system; a
        // consumer reading PascalCase here would be reading the only exception.
        Assert.Equal(customerId, body.RootElement.GetProperty("customerId").GetGuid());
        Assert.Equal("000042", body.RootElement.GetProperty("customerCode").GetString());
    }

    // ------------------------------------------------------------ files

    [Fact]
    public void An_account_url_selects_blob_storage_by_managed_identity()
    {
        IConfiguration config = Config(("Storage:AccountUrl", "https://bbtest.blob.core.windows.net"));

        using ServiceProvider provider = Services(config).AddFileStorage(config).BuildServiceProvider();

        Assert.IsType<AzureBlobFileStorage>(provider.GetRequiredService<IFileStorage>());
    }

    [Fact]
    public void No_storage_setting_falls_back_to_local_disk()
    {
        IConfiguration config = Config(("FileStorage:LocalRoot", Path.Combine(Path.GetTempPath(), "bb-reg-test")));

        using ServiceProvider provider = Services(config).AddFileStorage(config).BuildServiceProvider();

        Assert.IsType<LocalDiskFileStorage>(provider.GetRequiredService<IFileStorage>());
    }

    // SFTP connects per operation, so resolving it opens no connection and
    // these need no server. What they pin is which settings choose it and that
    // an incomplete configuration fails at startup rather than at first upload.
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

    [Fact]
    public void Blob_storage_wins_over_sftp_when_both_are_set()
    {
        IConfiguration config = Config(
            ("Storage:AccountUrl", "https://example.blob.core.windows.net"),
            ("Storage:Sftp:Host", "files-pc"));

        using ServiceProvider provider = Services(config).AddFileStorage(config).BuildServiceProvider();

        Assert.IsType<AzureBlobFileStorage>(provider.GetRequiredService<IFileStorage>());
    }

    [Theory]
    [InlineData("billbook-files", "0000000042/a/b.pdf", "billbook-files/0000000042/a/b.pdf")]
    [InlineData("/srv/files/", "0000000042/a/b.pdf", "/srv/files/0000000042/a/b.pdf")]
    [InlineData("", "0000000042/a/b.pdf", "0000000042/a/b.pdf")]
    public void A_key_lands_under_the_sftp_root(string root, string key, string expected) =>
        Assert.Equal(expected, SftpFileStorage.RemotePath(root, key));

    // The server may hold more than this product's files, so a key that could
    // climb out of the root is refused even though StorageKey never makes one.
    [Theory]
    [InlineData("../other/x.pdf")]
    [InlineData("a/../../x.pdf")]
    [InlineData("/etc/passwd")]
    [InlineData("a\\..\\x.pdf")]
    [InlineData("a//x.pdf")]
    [InlineData("")]
    public void A_key_that_could_leave_the_sftp_root_is_refused(string key) =>
        Assert.Throws<InvalidOperationException>(() => SftpFileStorage.RemotePath("billbook-files", key));

    [Theory]
    [InlineData("ohD8VZEXGWo6Ez8GSEJQ9WpafgLFsOfLOtGGQCQo6Og", "SHA256:ohD8VZEXGWo6Ez8GSEJQ9WpafgLFsOfLOtGGQCQo6Og", true)]
    [InlineData("ohD8VZEXGWo6Ez8GSEJQ9WpafgLFsOfLOtGGQCQo6Og", "ohD8VZEXGWo6Ez8GSEJQ9WpafgLFsOfLOtGGQCQo6Og=", true)]
    [InlineData("ohD8VZEXGWo6Ez8GSEJQ9WpafgLFsOfLOtGGQCQo6Og", "SHA256:ohD8VZEXGWo6Ez8GSEJQ9WpafgLFsOfLOtGGQCQo6Oh", false)]
    [InlineData("ohD8VZEXGWo6Ez8GSEJQ9WpafgLFsOfLOtGGQCQo6Og", "", false)]
    public void A_pinned_host_key_must_match(string presented, string configured, bool matches) =>
        Assert.Equal(matches, SftpFileStorage.FingerprintMatches(presented, configured));

    [Fact]
    public async Task A_key_signed_download_url_is_read_only_and_expires()
    {
        // The development storage account's well-known key: the client can sign
        // locally without contacting anything, which is exactly the path under
        // test. No request is made.
        var container = new BlobContainerClient("UseDevelopmentStorage=true", "documents");
        var storage = new AzureBlobFileStorage(container);

        Uri? url = await storage.GetDownloadUrlAsync("org/area/1/file.pdf", TimeSpan.FromMinutes(5));

        Assert.NotNull(url);
        string query = url!.Query;

        // Read only. A link that could write would let whoever holds it replace
        // the document it was issued for.
        Assert.Contains("sp=r&", query + "&");
        Assert.Contains("se=", query);
        Assert.Contains("sig=", query);
    }

    [Fact]
    public async Task Without_a_key_or_an_account_client_there_is_no_url_and_no_failure()
    {
        // A container built from a token credential cannot sign with a key, and
        // with no service client there is no delegation to fall back on. The
        // interface says null means "stream it through the API instead".
        var container = new BlobContainerClient(
            new Uri("https://bbtest.blob.core.windows.net/documents"), new DefaultAzureCredential());

        Uri? url = await new AzureBlobFileStorage(container).GetDownloadUrlAsync("k", TimeSpan.FromMinutes(5));

        Assert.Null(url);
    }

    // ------------------------------------------------------------ secrets

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public void Production_without_a_vault_refuses_to_start()
    {
        IConfiguration config = Config();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            Services(config).AddSecretStore(config, new TestEnvironment(Environments.Production)));

        // The message names the setting to fix. It is what an operator reads in
        // a container log at two in the morning.
        Assert.Contains("KeyVault:Uri", ex.Message);
    }

    [Fact]
    public void A_vault_uri_selects_key_vault_in_any_environment()
    {
        IConfiguration config = Config(("KeyVault:Uri", "https://bb-test.vault.azure.net/"));

        using ServiceProvider provider = Services(config)
            .AddSecretStore(config, new TestEnvironment(Environments.Production))
            .BuildServiceProvider();

        Assert.IsType<KeyVaultSecretStore>(provider.GetRequiredService<ISecretStore>());
    }

    [Fact]
    public void Development_without_a_vault_reads_configuration()
    {
        IConfiguration config = Config();

        using ServiceProvider provider = Services(config)
            .AddSecretStore(config, new TestEnvironment(Environments.Development))
            .BuildServiceProvider();

        Assert.IsType<ConfigurationSecretStore>(provider.GetRequiredService<ISecretStore>());
    }
}
