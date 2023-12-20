using System;
using System.Threading.Tasks;
using HealthChecks.Redis;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace lab.common.HealthChecker;

/// <summary>
/// Redis Health Status
/// </summary>
public partial class RedisHealthStatusProvider : IRedisHealthStatusProvider
{
    /// <summary>
    /// 熔斷時間 - 120 秒
    /// </summary>
    private static readonly TimeSpan _durationOfBreak = TimeSpan.FromSeconds(120);

    private static HealthStatus _checkResult = HealthStatus.Unhealthy;

    private readonly RedisHealthCheck _healthCheckService;
    private readonly ILogger<RedisHealthStatusProvider> _logger;
    private readonly ResiliencePipeline _pipeline;

    public RedisHealthStatusProvider(RedisHealthCheck healthCheckService,
                                     ILogger<RedisHealthStatusProvider> logger)
    {
        this._healthCheckService = healthCheckService;
        this._logger = logger;
        this._pipeline = new ResiliencePipelineBuilder()
                         .AddRetry(new RetryStrategyOptions
                         {
                             //最高重式次數
                             MaxRetryAttempts = 2,
                             //重試時的等待時間以等比級數增加
                             BackoffType = DelayBackoffType.Exponential,
                             UseJitter = false,
                             //預設延遲 2 秒，配合 DelayBackoffType.Exponential + MaxRetryAttempts 設定，最高等待 8 秒
                             Delay = TimeSpan.FromSeconds(2),
                             ShouldHandle = arguments => arguments.Outcome switch
                             {
                                 { Exception: Exception } => PredicateResult.True(),
                                 _ => PredicateResult.False()
                             },
                             OnRetry = arguments =>
                             {
                                 this._logger.LogInformation("{datetime} : retry!", DateTime.Now);
                                 return ValueTask.CompletedTask;
                             }
                         })
                         .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                         {
                             ShouldHandle = arguments => arguments.Outcome switch
                             {
                                 { Exception: Exception } => PredicateResult.True(),
                                 _ => PredicateResult.False()
                             },
                             //熔斷後停止多久，為了測試，使用 30 秒鐘
                             BreakDuration = TimeSpan.FromSeconds(30),
                             OnClosed = arguments =>
                             {
                                 this._logger.LogInformation("{datetime} : on close!", DateTime.Now);
                                 _checkResult = HealthStatus.Unhealthy;
                                 return ValueTask.CompletedTask;
                             },
                             OnOpened = arguments =>
                             {
                                 this._logger.LogInformation("{datetime} : on opened!", DateTime.Now);
                                 _checkResult = HealthStatus.Healthy;
                                 return ValueTask.CompletedTask;
                             }
                         }).Build();
    }

    /// <summary>
    /// 執行 Redis 健康狀態檢查
    /// </summary>
    public async Task CheckHealthAsync()
    {
        await this._pipeline.ExecuteAsync(async token =>
        {
            var healthCheckResult = await this._healthCheckService.CheckHealthAsync(new HealthCheckContext(), token);
            var healthStatus = healthCheckResult.Status;
            LogRedisHealthStatus(this._logger, DateTime.Now, healthStatus);
            _checkResult = healthStatus;
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

    /// <summary>
    /// set to unhealthy
    /// </summary>
    public void SetUnhealthy()
    {
        this._logger.LogInformation("{datetime} : set unhealthy", DateTime.Now);
        _checkResult = HealthStatus.Unhealthy;
    }

    [LoggerMessage(LogLevel.Error, "{dateTime}: Redis Current Status is {healthStatus}")]
    private static partial void LogRedisHealthStatus(ILogger<RedisHealthStatusProvider> logger, DateTime dateTime, HealthStatus healthStatus);
}