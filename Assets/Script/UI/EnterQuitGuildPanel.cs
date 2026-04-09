using System;
using TMPro;
using UnityEngine;

public class EnterQuitGuildPanel : UIPopPanelBase
{
   [SerializeField] private TMP_Text guildNameText;
   private Action onQuitGuildAction;
   public void Init(string guildName, Action quitGuildAction)
   {
       guildNameText.text ="您确认退出公会"+ guildName+"吗?";
       this.onQuitGuildAction = quitGuildAction;
       Show();
   }
   public void OnEnterButtonClick()
   {
       onQuitGuildAction.Invoke();
       Hide(false);
   }
   public void OnQuitButtonClick()
   {
       Hide(false);
   }
}
