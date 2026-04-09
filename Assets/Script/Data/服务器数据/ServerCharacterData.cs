using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 服务器角色数据管理类
/// 用于处理每个服务器中独立的角色数据
/// </summary>
[Serializable]
public class ServerCharacterManager
{
    // 服务器ID到角色列表的映射
    public Dictionary<int, List<CharacterData>> serverCharacters;
    
    public ServerCharacterManager()
    {
        serverCharacters = new Dictionary<int, List<CharacterData>>();
    }
    
    /// <summary>
    /// 为指定服务器添加角色
    /// </summary>
    /// <param name="serverId">服务器ID</param>
    /// <param name="character">角色数据</param>
    public void AddCharacterToServer(int serverId, CharacterData character)
    {
        if (!serverCharacters.ContainsKey(serverId))
        {
            serverCharacters[serverId] = new List<CharacterData>();
        }
        
        serverCharacters[serverId].Add(character);
    }
    
    /// <summary>
    /// 获取指定服务器的角色列表
    /// </summary>
    /// <param name="serverId">服务器ID</param>
    /// <returns>角色列表</returns>
    public List<CharacterData> GetCharactersForServer(int serverId)
    {
        if (serverCharacters.ContainsKey(serverId))
        {
            return serverCharacters[serverId];
        }
        
        return new List<CharacterData>();
    }
    
    /// <summary>
    /// 检查指定服务器是否存在特定角色
    /// </summary>
    /// <param name="serverId">服务器ID</param>
    /// <param name="characterId">角色ID</param>
    /// <returns>是否存在</returns>
    public bool CharacterExistsInServer(int serverId, string characterId)
    {
        if (serverCharacters.ContainsKey(serverId))
        {
            return serverCharacters[serverId].Exists(c => c.Id == characterId);
        }
        
        return false;
    }
    
    /// <summary>
    /// 从指定服务器移除角色
    /// </summary>
    /// <param name="serverId">服务器ID</param>
    /// <param name="characterId">角色ID</param>
    public void RemoveCharacterFromServer(int serverId, string characterId)
    {
        if (serverCharacters.ContainsKey(serverId))
        {
            serverCharacters[serverId].RemoveAll(c => c.Id == characterId);
        }
    }
}