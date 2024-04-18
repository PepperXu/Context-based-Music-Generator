using System.Collections;
using System.Collections.Generic;
using ABCUnity;
using UnityEngine;

public class MusicPlayer : MonoBehaviour
{
    //public bool selected = false;
    public static MusicPlayer Instance;
    private pxStrax[] synths;
    public int current = 0;
    public int note = 32;
    //private float[] minorScale = new float[] { 0, 2f, 3f, 5f, 7f, 8f, 10f, 12f };
    //private float[] majorScle = new float[]  { 0, 2f, 4f, 5f, 7f, 9f, 11f, 12f };

    public int currentTempo;
    private float beatDuration;
    private float timer;

    public PhotoManager.NoteInfo[] currentMelody;

    public bool playing = false;

    //public bool minor;
    // Use this for initialization
    void Start () {
        if(!Instance){
            Instance = this;
        } else {
            Destroy(gameObject);
        }
        synths = GetComponentsInChildren<pxStrax>();
	}
	
	// Update is called once per frame
	void Update () {
        if(Input.GetKeyDown(KeyCode.P)) {
            if(!playing) StartPlay(); else StopPlay();
        }
    }

    public void StartPlay(){
        beatDuration = 60f/currentTempo;
        timer = 0f;
        StartCoroutine(PlayMelody());
    }

    public void StopPlay(){
        foreach(var synth in synths) {
            synth.KeyOff();
        }
        StopAllCoroutines();
    }


    IEnumerator PlayMelody(){
        int noteIdx = 0;
        playing = true;
        float timer_end = -1f;
        while(true){
            if(noteIdx >= currentMelody.Length){
                if(timer_end < 0f)
                    timer_end = timer + (float)currentMelody[noteIdx-1].duration / 0.25f * beatDuration;
                else if(timer >= timer_end){
                    noteIdx = 0;
                    timer = 0f;
                }
            } else {
                PhotoManager.NoteInfo note = currentMelody[noteIdx];
                if(timer >= (note.time*beatDuration)){
                    StartCoroutine(PlayNote(note.pitch, note.duration));
                    noteIdx++;
                }
            }
            timer += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
        
    }

    IEnumerator PlayNote(int pitch, double duration){
        current = (current + 1) % 10;
        synths[current].KeyOn(pitch);
        yield return new WaitForSecondsRealtime((float)duration / 0.25f * beatDuration / 4f);
        synths[current].KeyOff();
    }
}
