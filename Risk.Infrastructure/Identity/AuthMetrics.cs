using System.Diagnostics.Metrics;

namespace Risk.Infrastructure.Identity;

/// <summary>
/// Tæller loginforsøg fordelt på resultat, så fejlede forsøg kan ses i Grafana og udløse en alarm (OWASP A09).
/// Tællerne har kun resultatet som tag, aldrig email eller bruger-id.
/// </summary>
public sealed class AuthMetrics
{
    public const string MeterName = "Risk.Auth";

    private readonly Counter<long> _loginAttempts;

    public AuthMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _loginAttempts = meter.CreateCounter<long>(
            "risk.auth.login.attempts", unit: "{attempt}", description: "Loginforsøg fordelt på resultat.");
    }

    public void LoginAttempt(string result) =>
        _loginAttempts.Add(1, new KeyValuePair<string, object?>("result", result));
}