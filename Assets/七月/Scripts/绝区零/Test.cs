using UnityEngine;

public class Test : MonoBehaviour
{
    [SerializeField] private VoidEventSO voidEventSO;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }
    void OnEnable()
    {
        voidEventSO.onEventRaised += HandleEvent;
    }
    void OnDisable()
    {
        voidEventSO.onEventRaised -= HandleEvent;
    }
    public void HandleEvent()
    {
        Debug.Log("HandleEvent");
    }

    // Update is called once per frame
    void Update()
    {

    }
}
