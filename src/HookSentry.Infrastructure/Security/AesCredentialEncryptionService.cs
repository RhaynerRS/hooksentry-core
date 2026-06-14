using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

using HookSentry.Domain.Security;

namespace HookSentry.Infrastructure.Security;

public sealed class AesCredentialEncryptionService : ICredentialEncryptionService
{
    private readonly byte[] _key;

    public AesCredentialEncryptionService(IOptions<CredentialEncryptionSettings> options)
    {
        _key = Convert.FromBase64String(options.Value.Key);

        if (_key.Length != 32)
            throw new InvalidOperationException(
                "CredentialEncryption:Key deve ser uma chave AES-256 de exatamente 32 bytes (base64).");
    }

    public string Encrypt(string plaintext)
    {
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        RandomNumberGenerator.Fill(nonce);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];

        using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
        nonce.CopyTo(result, 0);
        tag.CopyTo(result, nonce.Length);
        ciphertext.CopyTo(result, nonce.Length + tag.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string encryptedBase64)
    {
        var data = Convert.FromBase64String(encryptedBase64);

        var nonce = data[..12];
        var tag = data[12..28];
        var ciphertext = data[28..];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
