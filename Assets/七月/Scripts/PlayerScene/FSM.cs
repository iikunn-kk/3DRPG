using UnityEngine;
public enum StateType
{
    Idle,
    Find_Enemy,
    Attack,
    Die,
    Success
}
public class FSM : MonoBehaviour
{
    [SerializeField] private StateType curState;
    void Start()
    {
        curState = StateType.Idle;
    }

    void Update()
    {
        switch (curState)
        {
            case StateType.Idle:
                OnIdle();
                break;

            case StateType.Find_Enemy:
                OnFindEnemy();
                break;

            case StateType.Attack:
                OnAttack();
                break;

            case StateType.Die:
                OnDie();
                break;

            case StateType.Success:
                OnSuccess();
                break;
        }
    }

    void OnIdle() { }
    void OnFindEnemy() { }
    void OnAttack() { }
    void OnDie() { }
    void OnSuccess() { }
}
