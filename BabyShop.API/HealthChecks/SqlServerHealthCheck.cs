using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BabyShop.API.HealthChecks;

/// <summary>
/// سلامت اتصال به دیتابیس اصلی را بررسی می‌کند تا /health/ready وضعیت واقعی سرویس را بگوید.
/// </summary>
public sealed class SqlServerHealthCheck : IHealthCheck
{
    private const int TimeoutSeconds = 5;

    private readonly string? _connectionString;

    public SqlServerHealthCheck(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            return HealthCheckResult.Unhealthy("Connection string 'DefaultConnection' is not configured.");

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

            await connection.OpenAsync(timeout.Token);
            return HealthCheckResult.Healthy("SQL Server is reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("SQL Server is unreachable.", exception);
        }
    }
}