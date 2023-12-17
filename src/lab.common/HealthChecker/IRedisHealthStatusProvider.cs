using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace lab.common.HealthChecker;

/// <summary>
/// Redis Health Status
/// </summary>
public interface IRedisHealthStatusProvider
{
    /// <summary>
    /// 檢查 Redis 狀態
    /// </summary>
    public Task CheckHealthAsync();

    /// <summary>
    /// Health CheckResult
    /// </summary>
    public HealthStatus CheckResult();
}