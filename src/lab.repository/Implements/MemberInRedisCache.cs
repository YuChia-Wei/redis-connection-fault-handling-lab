using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using lab.common.HealthChecker;
using lab.repository.Entities;
using lab.repository.Interfaces;
using MessagePack;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Fallback;
using Polly.Retry;
using StackExchange.Redis;

namespace lab.repository.Implements;

/// <summary>
/// 成員狀態快取 Repository
/// </summary>
/// <seealso cref="ICache{T}" />
public class MemberInRedisCache : ICache<Member>
{
    /// <summary>
    /// The cache
    /// </summary>
    private readonly IDistributedCache _cache;

    /// <summary>
    /// Logger
    /// </summary>
    private readonly ILogger<MemberInRedisCache> _logger;

    /// <summary>
    /// Redis 健康監測
    /// </summary>
    private readonly IRedisHealthStatusProvider _redisHealthStatusProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemberInRedisCache" /> class.
    /// </summary>
    /// <param name="cache">The cache.</param>
    /// <param name="logger"></param>
    /// <param name="redisHealthStatusProvider"></param>
    public MemberInRedisCache(IDistributedCache cache,
                              ILogger<MemberInRedisCache> logger,
                              IRedisHealthStatusProvider redisHealthStatusProvider)
    {
        this._cache = cache;
        this._redisHealthStatusProvider = redisHealthStatusProvider;
        this._logger = logger;
    }

    /// <summary>
    /// 取得快取
    /// </summary>
    /// <param name="key">快取 Key</param>
    /// <returns></returns>
    public async Task<Member?> GetAsync(string key)
    {
        if (!this.IsRedisHealthy())
        {
            return null;
        }

        var fallbackPolicy = this.GetFunctionFallbackPolicy();

        // Get 方法沒有建立重試策略，因此不需要 WrapAsync
        // var policy = Policy.WrapAsync<Member>(fallbackPolicy);

        return await fallbackPolicy.ExecuteAsync(this.GetAction(key));
    }

    /// <summary>
    /// 建立快取
    /// </summary>
    /// <param name="key">快取 Key</param>
    /// <param name="value">快取值</param>
    public async Task SetAsync(string key, Member value)
    {
        if (!this.IsRedisHealthy())
        {
            return;
        }

        // 策略組合 - 組合重試策略與熔斷策略
        var policy = Policy.WrapAsync(this.SetFunctionFallbackPolicy(), this.SetFunctionRetryPolicy());

        await policy.ExecuteAsync(async () =>
        {
            // 序列化
            var bytes = MessagePackSerializer.Serialize(value);

            var options = new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromDays(3) };

            await this._cache.SetAsync(key, bytes, options);
        });
    }

    /// <summary>
    /// 取得成員資料的 Func
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    private Func<Task<Member?>> GetAction(string key)
    {
        return async () =>
        {
            if (await this._cache.GetAsync(key) is { } bytes)
            {
                return MessagePackSerializer.Deserialize<Member>(bytes);
            }

            return default;
        };
    }

    /// <summary>
    /// GetAsync 的熔斷策略
    /// </summary>
    /// <returns></returns>
    private AsyncFallbackPolicy GetFunctionFallbackPolicy()
    {
        return Policy.Handle<RedisTimeoutException>()
                     .FallbackAsync(onFallbackAsync: async exception =>
                                    {
                                        var message =
                                            $"{nameof(this.GetType)}.{nameof(this.SetAsync)} - onFallback";

                                        this._logger.LogError(
                                            exception, message, $"{this.GetType().Name}",
                                            $"{nameof(this.GetAsync)}");

                                        try
                                        {
                                            //如果發生 Fallback 的話，就要主動去把 redis health checker 內的狀態更新，避免下次調用 API 又進入等待
                                            this._redisHealthStatusProvider.SetUnhealthy();
                                        }
                                        catch (RedisTimeoutException)
                                        {
                                            this._logger.LogError(exception, "Redis is unhealthy!");
                                        }
                                    },
                                    fallbackAction: cancellationToken => Task.CompletedTask);
    }

    private bool IsRedisHealthy()
    {
        return this._redisHealthStatusProvider.CheckResult() == HealthStatus.Healthy;
    }

    /// <summary>
    /// SetAsync 的熔斷策略
    /// </summary>
    /// <returns></returns>
    private AsyncFallbackPolicy SetFunctionFallbackPolicy()
    {
        return Policy.Handle<Exception>()
                     .FallbackAsync(onFallbackAsync: async exception =>
                                    {
                                        var message =
                                            $"{nameof(this.GetType)}.{nameof(this.SetAsync)} - onFallback";

                                        this._logger.LogError(exception, message, $"{this.GetType().Name}",
                                                              $"{nameof(this.GetAsync)}");
                                    },
                                    fallbackAction: cancellationToken => Task.CompletedTask);
    }

    /// <summary>
    /// SetAsync 的重試策略
    /// </summary>
    /// <returns></returns>
    private AsyncRetryPolicy SetFunctionRetryPolicy()
    {
        // 重試策略 - 當功能執行發生 Exception 就進入重試，重試五次，重試等待時間為 2 的次數次方
        return Policy.Handle<Exception>()
                     .WaitAndRetryAsync(
                         2,
                         retryAttempt =>
                         {
                             var timeToWait = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                             var retryMessage =
                                 $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Retry Times: {retryAttempt}, Waiting {timeToWait.TotalSeconds} seconds";
                             this._logger.Log(LogLevel.Warning, retryMessage);

                             return timeToWait;
                         },
                         (exception, timeSpan) =>
                         {
                             this._logger.Log(LogLevel.Warning,
                                              $"on retry timeSpan:{timeSpan}, exception:{exception.Message}");
                         }
                     );
    }

    /// <summary>
    /// 壓縮資料
    /// </summary>
    /// <param name="redisKey"></param>
    /// <param name="bytes"></param>
    /// <returns></returns>
    private static async Task<MemoryStream> ZipDataMemoryStreamAsync(string redisKey, byte[] bytes)
    {
        await using var zipStream = new MemoryStream();
        using var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Update);
        var entry = zipArchive.CreateEntry(redisKey);
        await using var entryStream = entry.Open();
        // await entryStream.WriteAsync(bytes, 0, bytes.Length);
        await entryStream.WriteAsync(bytes);
        return zipStream;
    }
}