using System.Collections;
using System.Collections.Generic;
using Microsoft.MixedReality.GraphicsTools;
using TMPro;
using UnityEngine;
using UnityEngine.Events;


public class GazeableMelody : MonoBehaviour
{
    public static GazeableMelody currentActive;
    public PhotoManager.NoteInfo[] melody;

    public int bpm;
    public string description;

    private UnityEvent changeActiveMelody = new UnityEvent();
    public MaterialInstance matInstance;

    public GameObject descriptionObject;
    public TextMeshPro descriptionText;

    public Color activeColor;

    // Start is called before the first frame update
    void Start()
    {
        changeActiveMelody.AddListener(ChangeActiveMelody);
        descriptionText.text = description;
        //matInstance = GetComponent<MaterialInstance>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    public void Hovering(){
        ActivateMelody();
        descriptionObject.SetActive(true);
    }

    public void Unhover(){
        StopMelody();
        descriptionObject.SetActive(false);
    }
    public void ActivateMelody(){
        
        if(MusicPlayer.Instance.playing)
            MusicPlayer.Instance.StopPlay();

        MusicPlayer.Instance.currentMelody = melody;
        MusicPlayer.Instance.StartPlay();
        currentActive = this;
        changeActiveMelody.Invoke();
        StartCoroutine(BeatAnimation());
    }
    public void StopMelody(){
        if(MusicPlayer.Instance.playing)
            MusicPlayer.Instance.StopPlay();
        currentActive = null;
        changeActiveMelody.Invoke();
        StopAllCoroutines();
    }

    void ChangeActiveMelody(){
        if(currentActive != this){
            matInstance.Material.SetColor("_RimColor", Color.white);
        } else {
            matInstance.Material.SetColor("_RimColor", activeColor);
        }
    }

    IEnumerator BeatAnimation(){
        float timer = 0f;
        while(true){
            if(timer <= 0f){
                
                timer = 60f/bpm;
            }
            matInstance.transform.localScale = Vector3.one * (1f + 0.2f * timer/(60f/bpm));
            timer -= Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
    }
}
