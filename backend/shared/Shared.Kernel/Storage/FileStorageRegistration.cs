using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Shared.Kernel.Storage;

/// <summary>
/// Chooses the file store implementation (FTP, SFTP, or LocalDisk).
/// </summary>
public static class FileStorageRegistration
{
    /// <summary>
    /// FTP when <c>Storage:Ftp:Host</c> is configured,
    /// SFTP when <c>Storage:Sftp:Host</c> is configured,
    /// Local disk (<see cref="LocalDiskFileStorage"/>) otherwise.
    /// </summary>
    public static IServiceCollection AddFileStorage(
        this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration["Storage:Ftp:Host"] is { Length: > 0 } ftpHost)
        {
            FtpStorageOptions options = FtpOptions(configuration, ftpHost);

            services.AddSingleton<IFileStorage>(provider => new FtpFileStorage(
                options, provider.GetRequiredService<ILogger<FtpFileStorage>>()));

            return services;
        }

        if (configuration["Storage:Sftp:Host"] is { Length: > 0 } sftpHost)
        {
            SftpStorageOptions options = SftpOptions(configuration, sftpHost);

            services.AddSingleton<IFileStorage>(provider => new SftpFileStorage(
                options, provider.GetRequiredService<ILogger<SftpFileStorage>>()));

            return services;
        }

        services.AddSingleton<IFileStorage, LocalDiskFileStorage>();

        return services;
    }

    private static FtpStorageOptions FtpOptions(IConfiguration configuration, string host)
    {
        string Required(string key) =>
            configuration[key] is { Length: > 0 } value
                ? value
                : throw new InvalidOperationException(
                    $"Storage:Ftp:Host is set, so file storage is FTP, but {key} is not configured.");

        bool enableSsl = bool.TryParse(configuration["Storage:Ftp:EnableSsl"], out bool parsedSsl) && parsedSsl;

        int port = configuration["Storage:Ftp:Port"] is { Length: > 0 } text
            ? int.TryParse(text, out int parsed) && parsed is > 0 and <= 65535
                ? parsed
                : throw new InvalidOperationException($"Storage:Ftp:Port '{text}' is not a valid port number.")
            : (enableSsl ? 990 : 21);

        bool usePassive = !bool.TryParse(configuration["Storage:Ftp:UsePassive"], out bool parsedPassive) || parsedPassive;

        return new FtpStorageOptions(
            host,
            port,
            Required("Storage:Ftp:Username"),
            Required("Storage:Ftp:Password"),
            configuration["Storage:Ftp:Root"] is { Length: > 0 } root ? root : "billbook-files",
            enableSsl,
            usePassive);
    }

    private static SftpStorageOptions SftpOptions(IConfiguration configuration, string host)
    {
        string Required(string key) =>
            configuration[key] is { Length: > 0 } value
                ? value
                : throw new InvalidOperationException(
                    $"Storage:Sftp:Host is set, so file storage is SFTP, but {key} is not configured.");

        int port = configuration["Storage:Sftp:Port"] is { Length: > 0 } text
            ? int.TryParse(text, out int parsed) && parsed is > 0 and <= 65535
                ? parsed
                : throw new InvalidOperationException($"Storage:Sftp:Port '{text}' is not a port number.")
            : 22;

        return new SftpStorageOptions(
            host,
            port,
            Required("Storage:Sftp:Username"),
            Required("Storage:Sftp:Password"),
            configuration["Storage:Sftp:Root"] is { Length: > 0 } root ? root : "billbook-files",
            configuration["Storage:Sftp:HostKeySha256"]);
    }
}
