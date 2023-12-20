using System.Threading.Tasks;
using lab.repository.Entities;
using lab.repository.Interfaces;

namespace lab.repository.Implements;

/// <summary>
/// Member Repository
/// </summary>
public class MemberRepository : IMemberRepository
{
    /// <summary>
    /// The Member cache repository
    /// </summary>
    private readonly ICache<Member?> _cache;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="cache"></param>
    public MemberRepository(ICache<Member?> cache)
    {
        this._cache = cache;
    }

    /// <summary>
    /// 取得成員
    /// </summary>
    /// <param name="id">成員 Id</param>
    /// <returns></returns>
    public async Task<Member?> GetAsync(string id)
    {
        return await this._cache.GetAsync(id);
    }

    /// <summary>
    /// 設定成員資料
    /// </summary>
    /// <param name="member"></param>
    public async Task SetAsync(Member? member)
    {
        await this._cache.SetAsync(member.Id, member);
    }
}