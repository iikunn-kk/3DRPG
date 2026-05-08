// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// // 在Unity创建菜单中添加创建该状态的选项
// [CreateAssetMenu(menuName = "Data/StateMachine/PlayerState/Defeated", fileName = "PlayerState_Defeated")]
// public class PlayerState_Defeated : PlayerState
// {
//     [SerializeField] ParticleSystem vfx; // 死亡时播放的粒子特效
//     [SerializeField] AudioClip[] voice;  // 死亡音效数组，可配置多个音效随机播放

//     // 当进入死亡状态时调用
//     public override void Enter()
//     {
//         base.Enter(); // 调用父类的Enter方法

//         // 在玩家当前位置生成死亡粒子特效
//         Instantiate(vfx, player.transform.position, Quaternion.identity);

//         // 从音效数组中随机选择一个音效播放
//         AudioClip deathVoice = voice[Random.Range(0, voice.Length)];
//         player.VoicePlayer.PlayOneShot(deathVoice);
//     }

//     // 每帧更新逻辑
//     public override void LogicUpdate()
//     {
//         // 检查死亡动画是否播放完毕
//         if (IsAnimationFinished)
//         {
//             // 动画结束后切换到漂浮状态

//             // stateMachine.SwitchState(typeof(PlayerState_Float));
//         }
//     }
// }
