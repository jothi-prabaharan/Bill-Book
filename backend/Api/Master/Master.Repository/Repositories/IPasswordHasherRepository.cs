using Master.Repository.Clients;

namespace Master.Repository.Repositories;

public interface IPasswordHasherRepository
{
    string Hash(string password);

    bool Verify(string password, string hash);
}

/// <summary>BCrypt at work factor 12, per the auth rules.</summary>
public sealed class BcryptPasswordHasher : IPasswordHasherRepository
{
    private const int WorkFactor = 12;

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(hash))
        {
            return false;
        }

        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
