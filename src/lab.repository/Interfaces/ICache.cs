using System.Threading.Tasks;
using lab.repository.Entities;

namespace lab.repository.Interfaces;

/// <summary>
/// 快取 Repository 介面
/// </summary>
/// <typeparam name="T">泛型類別</typeparam>
public interface ICache<T>
{
    /// <summary>
    /// 取得快取
    /// </summary>
    /// <param name="key">快取 Key</param>
    /// <returns></returns>
    Task<T?> GetAsync(string key);

    /// <summary>
    /// 建立快取
    /// </summary>
    /// <param name="key">快取 Key</param>
    /// <param name="value">快取值</param>
    /// <returns></returns>
    Task SetAsync(string key, T value);
}