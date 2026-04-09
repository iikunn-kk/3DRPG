using System;
using TMPro;
using UnityEngine;

public class ConfirmJoinGuildPanel : UIPopPanelBase
{
   [SerializeField] private TMP_Text guildNameText;
   [SerializeField] private TMP_Text guildMasterNameText;
   private Action _onConfirmButtonClick;

   public void Init(GuildData data, Action onConfirmButtonClick)
   {
       this._onConfirmButtonClick = onConfirmButtonClick;
       guildNameText.text = data.guildName;
       guildMasterNameText.text = data.leaderCharacterName;
       Show();
   }
   
   public void OnConfirmButtonClick()
   {
       _onConfirmButtonClick?.Invoke();
       Hide(false);
   }
   
   public void OnCancelButtonClick()
   {
       Hide(false);
   }
}