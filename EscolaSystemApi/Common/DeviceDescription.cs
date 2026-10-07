namespace EscolaSystemApi.Common;

// Resume o User-Agent em "Chrome no Windows", para a pessoa reconhecer o aparelho na lista de sessões
public static class DeviceDescription
{
    public const int MaxUserAgentLength = 512;

    public static string? Trim(string? userAgent) =>
        string.IsNullOrWhiteSpace(userAgent) ? null
        : userAgent.Length > MaxUserAgentLength ? userAgent[..MaxUserAgentLength] : userAgent;

    public static string From(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return "Aparelho desconhecido";

        var browser = Browser(userAgent);
        var system = System(userAgent);
        return (browser, system) switch
        {
            (null, null) => "Aparelho desconhecido",
            (null, _) => system!,
            (_, null) => browser,
            _ => $"{browser} no {system}"
        };
    }

    // A ordem importa: Edge e Opera também dizem "Chrome", e o Chrome também diz "Safari"
    private static string? Browser(string ua) =>
        ua.Contains("Edg/") || ua.Contains("EdgA/") || ua.Contains("EdgiOS/") ? "Edge"
        : ua.Contains("OPR/") ? "Opera"
        : ua.Contains("SamsungBrowser/") ? "Samsung Internet"
        : ua.Contains("Chrome/") || ua.Contains("CriOS/") ? "Chrome"
        : ua.Contains("Firefox/") || ua.Contains("FxiOS/") ? "Firefox"
        : ua.Contains("Safari/") ? "Safari"
        : null;

    private static string? System(string ua) =>
        ua.Contains("Windows") ? "Windows"
        : ua.Contains("Android") ? "Android"
        : ua.Contains("iPhone") ? "iPhone"
        : ua.Contains("iPad") ? "iPad"
        : ua.Contains("Mac OS X") ? "Mac"
        : ua.Contains("CrOS") ? "Chromebook"
        : ua.Contains("Linux") ? "Linux"
        : null;
}
