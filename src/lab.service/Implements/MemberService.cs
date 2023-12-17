using System.Threading.Tasks;
using lab.repository.Entities;
using lab.repository.Interfaces;
using lab.service.Dtos;
using lab.service.Interfaces;

namespace lab.service.Implements;

/// <summary>
/// 成員服務
/// </summary>
/// <seealso cref="IMemberService" />
public class MemberService : IMemberService
{
    private readonly IMemberRepository _memberRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemberService" /> class.
    /// </summary>
    /// <param name="memberRepository"></param>
    public MemberService(
        IMemberRepository memberRepository)
    {
        this._memberRepository = memberRepository;
    }

    /// <summary>
    /// 取得成員
    /// </summary>
    /// <returns></returns>
    public async Task<MemberDto> GetAsync(string id)
    {
        var member = await this._memberRepository.GetAsync(id);

        return new MemberDto {Id = member.Id, Name = member.Name};
    }

    /// <summary>
    /// 建立成員
    /// </summary>
    /// <returns></returns>
    public async Task<MemberDto> SetAsync(string id, string name)
    {
        var member = new Member() {Id = id, Name = name};

        await this._memberRepository.SetAsync(member);

        return new MemberDto {Id = member.Id, Name = member.Name};
    }
}