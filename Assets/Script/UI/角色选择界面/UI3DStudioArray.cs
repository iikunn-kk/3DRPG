using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UI3DStudioArray : MonoBehaviour
{
   [Header("单个的影棚")]
   [SerializeField] private GameObject ui3DStudioModObj;
   [Header("所有的数据SO")]
   [SerializeField] private CharacterSelectDataSO characterSelectDataSO;
   [Header("每个摄影棚的位置")]
   [SerializeField] private List<Transform> positions;
   private Dictionary<CharacterProfession,UI3DStudioMod> ui3DStudioMods=new();
   [Header("角色动作摄影棚")] [SerializeField] private CharacterActionMod characterAction;
   private void Start()
   {
      Init();
   }
   private void Init()
   {
      for (int i = 0; i < characterSelectDataSO.data.Count; i++)
      {
         var obj = Instantiate(ui3DStudioModObj, positions[i]);
         var ui3DStudioMod = obj.GetComponent<UI3DStudioMod>();
         ui3DStudioMod.Init(characterSelectDataSO.data[i]);
         ui3DStudioMods.Add(characterSelectDataSO.data[i].job,ui3DStudioMod);
      }

   }
   /// <summary>
   /// 获取指定待机角色动作摄影棚的RenderTexture
   /// </summary>
   /// <param name="profession"></param>
   /// <returns></returns>
   public RenderTexture GetRenderTexture(CharacterProfession profession)
   {
      return ui3DStudioMods[profession].renderTexture;
   }
   /// <summary>
   /// 获取展示动作角色动作摄影棚的RenderTexture
   /// </summary>
   /// <returns></returns>
   public RenderTexture GetActionRenderTexture()
   {
      return characterAction.RenderTexture;
   }
   /// <summary>
   /// 修改展示中的角色的动作
   /// </summary>
   /// <param name="action"></param>
   public void SetAnimation(CharacterActionEnum action)
   {
      characterAction.SetAction(action);
   }
   public void ChangeGameObject(CharacterProfession profession)
   {
      characterAction.ChangeGameObject(characterSelectDataSO.data.First(x=>x.job==profession).showObj,profession);
   }
   
   // 添加控制旋转的方法
   public void AddRotation(float deltaRotation)
   {
       characterAction.AddRotation(deltaRotation);
   }
   
   // 设置旋转角度
   public void SetRotation(float rotation)
   {
       characterAction.SetRotation(rotation);
   }
   
   // 获取当前旋转角度
   public float GetRotation()
   {
       return characterAction.GetRotation();
   }
   
   // 重置旋转角度
   public void ResetRotation()
   {
       characterAction.ResetRotation();
   }
}