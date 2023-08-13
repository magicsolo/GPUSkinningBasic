using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class TestScene : MonoBehaviour
{
    private Transform parentt;
    [SerializeField]
    private GameObject sample;
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine("Generate");
    }

    private void OnGUI()
    {
        GUIStyle guiStyle = new GUIStyle();
        guiStyle.fontSize = 100;
        GUILayout.Label($"TotalTedyNum {parentt.childCount}",guiStyle);
    }
    
    // Update is called once per frame
    IEnumerator Generate()
    {
        parentt = (new GameObject("Tedys")).transform;
        for (int i = 0; i < 100000; i++)
        {
            GameObject obj = GameObject.Instantiate(sample);
            obj.transform.position = transform.position + new Vector3((Random.value - 0.5f) * 50, 0, (Random.value - 0.5f) * 100);
            obj.transform.SetParent(parentt,true);
                
            yield return new WaitForSeconds(0.04f);
        }
        yield break;
    }
}
