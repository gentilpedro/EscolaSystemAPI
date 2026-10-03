using EscolaSystemApi.Application.Services;
using Microsoft.Extensions.Configuration;

namespace EscolaSystemApi.Tests.Helpers;

public static class CpfEncryptionHelper
{
    public static CpfEncryptionService Create()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cpf:EncryptionKey"] = "chave-de-teste-apenas-para-testes-automatizados"
            })
            .Build();

        return new CpfEncryptionService(configuration);
    }
}
