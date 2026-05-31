using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 数据后端工厂：探测 MongoDB 可用性，自动选择后端。
/// </summary>
public static class DataStoreFactory
{
    /// <summary>
    /// 创建合适的数据后端。2秒超时探测 MongoDB，
    /// 成功返回 MongoDataStore，失败返回 LocalJsonDataStore。
    /// </summary>
    public static async Task<IDataStore> CreateAsync()
    {
        var mongoStore = new MongoDataStore();
        if (await mongoStore.TryConnectAsync())
        {
            Debug.Log("[DataStore] MongoDB 已连接，使用云端存储");
            return mongoStore;
        }

        Debug.Log("[DataStore] MongoDB 不可用，降级为本地文件存储");
        return new LocalJsonDataStore();
    }
}
