using System;
using System.Threading.Tasks;
using HealthChecks.Redis;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Registry;

namespace lab.common.HealthChecker;

/// <summary>
/// Redis Health Status
/// </summary>
public class RedisHealthStatusProvider : IRedisHealthStatusProvider
{
    /// <summary>
    /// 熔斷時間 - 120 秒
    /// </summary>
    private static readonly TimeSpan _durationOfBreak = TimeSpan.FromSeconds(120);

    private static HealthStatus _checkResult = HealthStatus.Unhealthy;

    private readonly RedisHealthCheck _healthCheckService;
    private readonly ILogger<RedisHealthStatusProvider> _logger;
    private readonly ResiliencePipeline<HealthStatus> _pipeline;

    public RedisHealthStatusProvider(ResiliencePipelineProvider<string> pipelineProvider,
                                     RedisHealthCheck healthCheckService,
                                     ILogger<RedisHealthStatusProvider> logger)
    {
        this._healthCheckService = healthCheckService;
        this._logger = logger;
        this._pipeline = pipelineProvider.GetPipeline<HealthStatus>("redis-health-check-retry-pipeline");
    }

    /// <summary>
    /// 執行 Redis 健康狀態檢查
    /// </summary>
    public async Task CheckHealthAsync()
    {
        _checkResult = await this._pipeline.ExecuteAsync<HealthStatus>(async token =>
        {
            this._logger.LogInformation("on CheckHealthAsync");
            var healthCheckResult = await this._healthCheckService.CheckHealthAsync(new HealthCheckContext(), token);
            this._logger.LogInformation("redis is {healthCheckResult}", healthCheckResult);
            return healthCheckResult.Status;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Health CheckResult
    /// </summary>
    public HealthStatus CheckResult()
    {
        this._logger.LogInformation("get check result");
        return _checkResult;
    }
}