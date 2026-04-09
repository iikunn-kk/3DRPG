using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerStateMachine : StateMachine
{
    [SerializeField] PlayerState[] states;
    Animator animator;
    PlayerController player;
    PlayerInput input;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        //Do player states initialization here
        input = GetComponent<PlayerInput>();
        player = GetComponent<PlayerController>();
        stateTable = new Dictionary<System.Type, IState>(states.Length);
        foreach (PlayerState state in states)
        {
            state.Initialize(animator, player, input, this);
            stateTable.Add(state.GetType(), state);
        }
    }

    void Start()
    {
        // SwtichOn(stateTable[typeof(PlayerState_Idle)]);
    }
}
