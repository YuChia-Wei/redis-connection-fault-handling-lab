using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using lab.common;
using lab.common.HealthChecker;
using lab.repository.Entities;
using lab.repository.Interfaces;
using MessagePack;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Registry;

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

    private readonly IRedisHealthStatusProvider _redisHealthStatusProvider;

    private readonly ResiliencePipeline<Member?> _redisRetryPipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemberInRedisCache" /> class.
    /// </summary>
    /// <param name="cache">The cache.</param>
    /// <param name="logger"></param>
    /// <param name="redisHealthStatusProvider"></param>
    /// <param name="resiliencePipelineProvider"></param>
    public MemberInRedisCache(IDistributedCache cache,
                              ILogger<MemberInRedisCache> logger,
                              IRedisHealthStatusProvider redisHealthStatusProvider,
                              ResiliencePipelineProvider<string> resiliencePipelineProvider)
    {
        this._cache = cache;
        this._redisRetryPipeline = resiliencePipelineProvider.GetPipeline<Member?>(PollyKeys.RedisRetryPipeline);
        this._logger = logger;
        this._redisHealthStatusProvider = redisHealthStatusProvider;
    }

    /// <summary>
    /// 取得快取
    /// </summary>
    /// <param name="key">快取 Key</param>
    /// <returns></returns>
    public async Task<Member?> GetAsync(string key)
    {
        //TODO: research and fix: Closure can be eliminated: method has overload to avoid closure creation
        var member = await this._redisRetryPipeline.ExecuteAsync<Member?>(async token =>
                         {
                             if (await this._cache.GetAsync(key, token) is { } bytes)
                             {
                                 return MessagePackSerializer.Deserialize<Member>(bytes, cancellationToken: token);
                             }

                             return default;
                         }
                     );
        return member;
    }

    /// <summary>
    /// 建立快取，塞入成功就回傳塞入的值，否則回傳 null
    /// </summary>
    /// <param name="key">快取 Key</param>
    /// <param name="value">快取值</param>
    public async Task<Member?> SetAsync(string key, Member value)
    {
        if (!this.IsRedisHealthy())
        {
            return null;
        }

        var member = await this._redisRetryPipeline.ExecuteAsync<Member?>(async token =>
        {
            // 序列化
            var bytes = MessagePackSerializer.Serialize(value);

            var options = new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromDays(3) };

            await this._cache.SetAsync(key, bytes, options, token);

            return value;
        });

        return member;
    }

    private bool IsRedisHealthy()
    {
        return this._redisHealthStatusProvider.CheckResult() == HealthStatus.Healthy;
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