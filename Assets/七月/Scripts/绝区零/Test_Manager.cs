using UnityEngine;

public class Test_Manager : MonoBehaviour
{
    [SerializeField] private VoidEventSO voidEventSO;
    [Header("延迟触发事件的时间")]
    [SerializeField] private float delayTime = 3f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        TimerManager.Instance.Delay(delayTime, () =>
        {
            voidEventSO.Raise(this);
        });

    }

    // Update is called once per frame
    void Update()
    {

    }
}
