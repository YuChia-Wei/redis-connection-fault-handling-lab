using lab.common.HealthChecker;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace lab.webapi.Infrastructure.BackgroundServices;

/// <summary>
/// Redis 服務健康檢查背景服務
/// </summary>
public partial class RedisHealthCheckBackgroundService : BackgroundService
{
    /// <summary>
    /// 預設 10 分鐘檢查一次 Redis Service 狀態
    /// </summary>
    private static readonly TimeSpan _intervalTime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Logger
    /// </summary>
    private readonly ILogger<RedisHealthCheckBackgroundService> _logger;

    /// <summary>
    /// Redis Health Status Object (Use Singleton in this project)
    /// </summary>
    private readonly IRedisHealthStatusProvider _redisHealthStatusProvider;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="redisHealthStatusProvider"></param>
    public RedisHealthCheckBackgroundService(ILogger<RedisHealthCheckBackgroundService> logger,
                                             IRedisHealthStatusProvider redisHealthStatusProvider)
    {
        this._logger = logger;
        this._redisHealthStatusProvider = redisHealthStatusProvider;
    }

    /// <summary>
    /// This method is called when the <see cref="T:Microsoft.Extensions.Hosting.IHostedService" /> starts. The implementation
    /// should return a task that represents
    /// the lifetime of the long running operation(s) being performed.
    /// </summary>
    /// <param name="stoppingToken">
    /// Triggered when
    /// <see cref="M:Microsoft.Extensions.Hosting.IHostedService.StopAsync(System.Threading.CancellationToken)" /> is called.
    /// </param>
    /// <returns>A <see cref="T:System.Threading.Tasks.Task" /> that represents the long running operations.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var machineName = Environment.MachineName;

        this._logger.LogInformation(
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{machineName}] {this.GetType().Name} is starting.");

        stoppingToken.Register(
            () => this._logger.LogInformation(
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{machineName}] {this.GetType()} is stopping."));

        while (stoppingToken.IsCancellationRequested.Equals(false))
        {
            try
            {
                await this._redisHealthStatusProvider.CheckHealthAsync();
            }
            catch (Exception e)
            {
                var checkResult = this._redisHealthStatusProvider.CheckResult();
                if (checkResult != HealthStatus.Healthy)
                {
                    LogRedisIsNotHealthy(this._logger, checkResult);
                }
            }

            // 於指定的間隔時間 (IntervalTime) 後再啟動
            await Task.Delay(_intervalTime, stoppingToken);
        }
    }

    [LoggerMessage(LogLevel.Error, "Redis Checking - Still Break, Redis Connection Health Status: {checkResult}")]
    private static partial void LogRedisIsNotHealthy(ILogger<RedisHealthCheckBackgroundService> logger, HealthStatus checkResult);
}