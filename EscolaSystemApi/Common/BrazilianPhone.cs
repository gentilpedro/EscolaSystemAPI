namespace EscolaSystemApi.Common;

// Telefone brasileiro: DDD (11 a 99) + 8 dígitos (fixo) ou 9 dígitos (celular, começando com 9)
public static class BrazilianPhone
{
    public static string Digits(string? value) => new((value ?? string.Empty).Where(char.IsDigit).ToArray());

    public static bool IsValid(string? value)
    {
        var digits = Digits(value);
        if (digits.Length is not (10 or 11) || digits[0] == '0')
            return false;
        // Celular tem 9 dígitos depois do DDD e começa com 9
        return digits.Length == 10 || digits[2] == '9';
    }

    // Grava sempre no mesmo formato: (51) 3333-1200 ou (51) 99999-1200
    public static string Format(string? value)
    {
        // Fora do padrão (o validator já recusa na API): devolve como veio
        if (!IsValid(value))
            return (value ?? string.Empty).Trim();

        var d = Digits(value);
        return d.Length == 11
            ? $"({d[..2]}) {d[2..7]}-{d[7..]}"
            : $"({d[..2]}) {d[2..6]}-{d[6..]}";
    }
}
