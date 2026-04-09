// using UnityEngine;

// [CreateAssetMenu(menuName = "Data/StateMachine/PlayerState/Idle", fileName = "PlayerState_Idle")]
// public class PlayerState_Idle : PlayerState
// {
//     [SerializeField] float deceleration = 5f;
//     public override void Enter()
//     {
//         base.Enter();
//         currentSpeed = player.MoveSpeed;
//         // player.SetVelocityX(0f);
//     }

//     public override void LogicUpdate()
//     {
//         if (input.Move)
//         {
//             stateMachine.SwitchState(typeof(PlayerState_Run));
//         }

//         if (input.Jump)
//         {
//             stateMachine.SwitchState(typeof(PlayerState_JumpUp));
//         }
//         if (!player.IsGrounded)
//         {
//             stateMachine.SwitchState(typeof(PlayerState_Fall));
//         }
//         currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, deceleration * Time.deltaTime);
//     }

//     public override void PhysicUpdate()
//     {
//         player.SetVelocityX(currentSpeed * player.transform.localScale.x);
//     }
// }
// using UnityEngine;

// public class PlayerState_Idle : MonoBehaviour
// {
//     // Start is called once before the first execution of Update after the MonoBehaviour is created
//     void Start()
//     {

//     }

//     // Update is called once per frame
//     void Update()
//     {

//     }
// }
