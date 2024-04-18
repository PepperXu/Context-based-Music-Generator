using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DebugText : MonoBehaviour
{
    private static TextMeshPro textComponent;
    private static float fadeTimer;
    
    
    // Start is called before the first frame update
    void OnEnable()
    {
        textComponent = GetComponent<TextMeshPro>();
    }

    void Update(){
        if(fadeTimer > 0f){
            fadeTimer -= Time.deltaTime;
            Color c = textComponent.color;
            c.a = fadeTimer/3f;
            textComponent.color = c;
        }
    }

    public static void SetText(string text)
    {
        if(textComponent){
            textComponent.text = text;
            fadeTimer = 3f;
        }
    }
}
