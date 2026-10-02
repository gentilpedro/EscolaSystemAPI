namespace EscolaSystemApi.Application.Interfaces;

public interface ICpfEncryptionService
{
    string Encrypt(string cpf);
    string Decrypt(string encrypted);
    string Hash(string cpf);
}
