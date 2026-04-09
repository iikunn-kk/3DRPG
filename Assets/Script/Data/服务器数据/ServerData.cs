using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ServerData 
{
   public int serverId;
   public string serverName;
   [SerializeField]
   private string state;
   public ServerState serverState {
       get {
           ServerState result;
           if (Enum.TryParse<ServerState>(state, out result)) {
               return result;
           }
           return ServerState.爆满; // 默认值
       }
   }
}

[System.Serializable]
public class ServerCategoryData 
{
   public int categoryId;
   public string categoryName;
   public ServerData[] servers;
   
   public ServerCategoryData()
   {
       servers = new ServerData[0];
   }
}