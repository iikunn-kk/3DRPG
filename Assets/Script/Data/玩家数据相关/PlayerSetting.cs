using System;
using UnityEngine;
[Serializable]
public class PlayerSetting
{
   public bool RememberPassword;
   public string Username;
   public string Password;
   public bool openBgm=true;
   public bool openSound=true;
   public float bgmVolume=1;
   public float soundVolume=1;
   public FullScreenMode fullScreenMode= FullScreenMode.ExclusiveFullScreen;
   public int resolutionWidth;
   public int resolutionHeight;
}