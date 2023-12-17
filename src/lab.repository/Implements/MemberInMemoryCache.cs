using System.Threading.Tasks;
using lab.repository.Entities;
using lab.repository.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace lab.repository.Implements;

/// <summary>
/// 存記憶體的成員Repository
/// </summary>
public class MemberInMemoryCache : ICache<Member>
{
    /// <summary>
    /// 被裝飾者
    /// </summary>
    private readonly ICache<Member> _cache;

    /// <summary>
    /// memory cache
    /// </summary>
    private readonly IMemoryCache _memoryCache;

    /// <summary>
    /// 成員資料的記憶體來原
    /// </summary>
    /// <param name="cache"></param>
    /// <param name="memoryCache"></param>
    public MemberInMemoryCache(ICache<Member> cache, IMemoryCache memoryCache)
    {
        this._cache = cache;
        this._memoryCache = memoryCache;
    }

    /// <summary>
    /// 取得成員
    /// </summary>
    /// <param name="stateKey">狀態 Key</param>
    /// <returns></returns>
    public async Task<Member> GetAsync(string stateKey)
    {
        var memberState = await this._cache.GetAsync(stateKey);

        if (memberState != null)
        {
            // 如果記憶體快取沒資料，寫入備份
            if (!this._memoryCache.TryGetValue(stateKey, out _))
            {
                this._memoryCache.Set(stateKey, memberState);
            }

            return memberState;
        }

        // 如果原本的資料來源沒有資料，但是記憶體快取有，就嘗試把記憶體內的資料寫回原本的資料來源
        var memberStateInMemory = this._memoryCache.Get<Member>(stateKey);
        if (memberStateInMemory != null)
        {
            // 此處因為是裝飾 Redis Repository，如果因為 Redis 掛掉而沒有資料，此處會導致寫入延遲，因為要等待 Redis 寫入成功
            await this._cache.SetAsync(stateKey, memberStateInMemory);
        }

        return memberStateInMemory;
    }

    /// <summary>
    /// 設定成員
    /// </summary>
    /// <param name="stateKey"></param>
    /// <param name="state"></param>
    public async Task SetAsync(string stateKey, Member state)
    {
        await this._cache.SetAsync(stateKey, state);

        // 寫入記憶體備份
        this._memoryCache.Set(stateKey, state);
    }
}