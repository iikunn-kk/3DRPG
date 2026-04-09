using UnityEngine;
using System;

public class TestDateTimeSerialization : MonoBehaviour
{
    [Serializable]
    public class TestData
    {
        public DateTime createTime;
        public string name;
        
        public TestData()
        {
            createTime = DateTime.Now;
            name = "Test";
        }
    }
    
    void Start()
    {
        TestData data = new TestData();
        Debug.Log("Original DateTime: " + data.createTime);
        Debug.Log("Original DateTime Ticks: " + data.createTime.Ticks);
        
        // 序列化
        string json = JsonUtility.ToJson(data);
        Debug.Log("Serialized JSON: " + json);
        
        // 反序列化
        TestData deserializedData = JsonUtility.FromJson<TestData>(json);
        Debug.Log("Deserialized DateTime: " + deserializedData.createTime);
        Debug.Log("Deserialized DateTime Ticks: " + deserializedData.createTime.Ticks);
        
        // 检查是否相等
        Debug.Log("DateTime equal: " + (data.createTime.Equals(deserializedData.createTime)));
    }
}