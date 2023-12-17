using MessagePack;

namespace lab.repository.Entities;

/// <summary>
/// 成員狀態
/// </summary>
[MessagePackObject(true)]
public class Member
{
    /// <summary>
    /// Id
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// 名稱
    /// </summary>
    public string Name { get; set; }
}