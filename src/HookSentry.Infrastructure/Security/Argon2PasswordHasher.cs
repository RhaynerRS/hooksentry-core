using Konscious.Security.Cryptography;
using System.Security.Cryptography;
using System.Text;

using HookSentry.Domain.Security;

namespace HookSentry.Infrastructure.Security;

public class Argon2PasswordHasher : IPasswordHasher
{
    private const int SALT_SIZE = 16;
    private const int HASH_SIZE = 32;
    private const int ITERATIONS = 5;
    private const int MEMORY_SIZE = 65536; // 64 MB
    private const int PARALLELISM = 8;

    public string Hash(string plainTextPassword)
    {
        var salt = RandomNumberGenerator.GetBytes(SALT_SIZE);
        using var hasher = new Argon2id(Encoding.UTF8.GetBytes(plainTextPassword));
        hasher.Salt = salt;
        hasher.DegreeOfParallelism = PARALLELISM;
        hasher.MemorySize = MEMORY_SIZE;
        hasher.Iterations = ITERATIONS;
        var hash = hasher.GetBytes(HASH_SIZE);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string plainTextPassword, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;

        byte[] salt, expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[0]);
            expectedHash = Convert.FromBase64String(parts[1]);
        }
        catch { return false; }

        using var hasher = new Argon2id(Encoding.UTF8.GetBytes(plainTextPassword));
        hasher.Salt = salt;
        hasher.DegreeOfParallelism = PARALLELISM;
        hasher.MemorySize = MEMORY_SIZE;
        hasher.Iterations = ITERATIONS;
        var newHash = hasher.GetBytes(HASH_SIZE);
        return newHash.SequenceEqual(expectedHash);
    }
}
