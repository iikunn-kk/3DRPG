using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UI3DStudioOfInventory : MonoBehaviour
{
   [Header("所有的数据SO")]
   [SerializeField] private CharacterSelectDataSO characterSelectDataSO;
   [Header("角色动作摄影棚")] [SerializeField] private CharacterActionMod characterAction;
   public void Init( CharacterProfession profession)
   {
       ChangeGameObject(profession);
       characterAction.SetRotation(0);
   }
   /// <summary>
   /// 获取展示动作角色动作摄影棚的RenderTexture
   /// </summary>
   /// <returns></returns>
   public RenderTexture GetActionRenderTexture()
   {
      return characterAction.RenderTexture;
   }
   private void ChangeGameObject(CharacterProfession profession)
   {
      characterAction.ChangeGameObject(characterSelectDataSO.data.First(x=>x.job==profession).showObj,profession);
   }
   
}