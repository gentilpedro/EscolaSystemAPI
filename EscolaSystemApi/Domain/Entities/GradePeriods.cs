namespace EscolaSystemApi.Domain.Entities;

// Períodos do ano letivo (trimestres), na mesma grafia do front (lib/school.ts → PERIODS)
public static class GradePeriods
{
    public static readonly string[] All =
        ["1º Trimestre", "2º Trimestre", "3º Trimestre", "Recuperação", "Final"];

    // Aceita "1° Trimestre" (símbolo de grau, comum no teclado) como "1º Trimestre"
    public static string Normalize(string? period) =>
        (period ?? string.Empty).Trim().Replace('°', 'º');

    public static bool IsValid(string? period) =>
        All.Contains(Normalize(period), StringComparer.OrdinalIgnoreCase);

    public static bool AreSame(string? a, string? b) =>
        string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);
}
