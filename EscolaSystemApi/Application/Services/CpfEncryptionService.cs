using System.Security.Cryptography;
using System.Text;
using EscolaSystemApi.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace EscolaSystemApi.Application.Services;

public class CpfEncryptionService(IConfiguration configuration) : ICpfEncryptionService
{
    private readonly byte[] _key = DeriveKey(
        configuration["Cpf:EncryptionKey"] ?? throw new InvalidOperationException("Cpf:EncryptionKey not configured"));

    public string Encrypt(string cpf)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var cipherBytes = encryptor.TransformFinalBlock(Encoding.UTF8.GetBytes(cpf), 0, cpf.Length);

        var result = new byte[aes.IV.Length + cipherBytes.Length];
        aes.IV.CopyTo(result, 0);
        cipherBytes.CopyTo(result, aes.IV.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string encrypted)
    {
        var data = Convert.FromBase64String(encrypted);
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = data[..16];

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(data, 16, data.Length - 16);
        return Encoding.UTF8.GetString(plainBytes);
    }

    public string Hash(string cpf)
    {
        using var hmac = new HMACSHA256(_key);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(cpf))).ToLowerInvariant();
    }

    private static byte[] DeriveKey(string configKey)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(configKey));
    }
}
