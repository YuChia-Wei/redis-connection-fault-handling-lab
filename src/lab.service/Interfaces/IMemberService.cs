using System.Threading.Tasks;
using lab.service.Dtos;

namespace lab.service.Interfaces;

/// <summary>
/// 成員狀態服務介面
/// </summary>
public interface IMemberService
{
    Task<MemberDto?> GetAsync(string id);

    /// <summary>
    /// 建立成員
    /// </summary>
    /// <returns></returns>
    Task<MemberDto> SetAsync(string id, string name);
}