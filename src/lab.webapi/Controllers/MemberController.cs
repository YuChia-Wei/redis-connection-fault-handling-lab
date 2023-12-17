using lab.service.Dtos;
using lab.service.Interfaces;
using lab.webapi.Controllers.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace lab.webapi.Controllers;

/// <summary>
/// 成員狀態 API
/// </summary>
/// <seealso cref="ControllerBase" />
[Route("v{version:apiVersion}/[controller]")]
[ApiController]
public class MemberController : ControllerBase
{
    /// <summary>
    /// The state service
    /// </summary>
    private readonly IMemberService _memberService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemberController" /> class.
    /// </summary>
    /// <param name="memberService">The state service.</param>
    public MemberController(IMemberService memberService)
    {
        this._memberService = memberService;
    }

    /// <summary>
    /// 建立成員
    /// </summary>
    /// <param name="member">Member</param>
    /// <returns></returns>
    [HttpPost]
    [Produces("application/json")]
    [ProducesResponseType(typeof(MemberViewModel), 200)]
    public async Task<MemberViewModel> CreateAsync([FromBody] MemberDto member)
    {
        //這邊很單純懶得再開一個輸入用物件，所以直接使用 Member Dto 給外部
        var memberDto = await this._memberService.SetAsync(member.Id, member.Name);

        return new MemberViewModel
        {
            Id = memberDto.Id,
            Name = memberDto.Name
        };
    }

    /// <summary>
    /// 取得成員
    /// </summary>
    /// <param name="id">Member id</param>
    /// <returns></returns>
    [HttpGet]
    [Route("{id}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(MemberViewModel), 200)]
    public async Task<MemberViewModel> GetAsync(string id)
    {
        var memberDto = await this._memberService.GetAsync(id);

        return new MemberViewModel
        {
            Id = memberDto.Id,
            Name = memberDto.Name
        };
    }
}