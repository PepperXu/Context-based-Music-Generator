using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GazeTracker : MonoBehaviour
{
    public struct Gaze{
        public Vector2 center;
        public float radius;
        public float duration;
        public float startTime;
    }

    public struct SamplePoint{
        public Quaternion rotation;
        public Vector2 screenPoint;
    }

    const float angleThreshold = 5f;
    const float durationThreshold = 0.7f;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
