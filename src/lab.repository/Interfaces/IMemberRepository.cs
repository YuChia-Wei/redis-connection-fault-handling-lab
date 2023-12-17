using System.Threading.Tasks;
using lab.repository.Entities;

namespace lab.repository.Interfaces;

public interface IMemberRepository
{
    /// <summary>
    /// 取得成員
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    Task<Member> GetAsync(string id);

    /// <summary>
    /// 設定成員資料
    /// </summary>
    /// <param name="member"></param>
    Task SetAsync(Member member);
}