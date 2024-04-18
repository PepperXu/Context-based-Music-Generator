using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR;
using UnityEngine;
using MixedReality.Toolkit.Subsystems;
using MixedReality.Toolkit;
using UnityEngine.Windows.WebCam;
using System.Linq;
using System;
using TMPro;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using MixedReality.Toolkit.UX;



public class PhotoManager : MonoBehaviour
{
    PhotoCapture photoCapture = null;

    
    Resolution cameraResolution;
    Texture2D targetTexture = null;

    private bool photoModeActivated = false;
    private bool photoTaken = false;
    private bool preparingPhotoShoot = false;
    private GameObject photoQuad;
    private string filename, filePath;

    [SerializeField] private Material photoMaterial;

    // Get a reference to the aggregator.
    HandsAggregatorSubsystem aggregator;

    bool subsystemEnabled = false;

    bool leftPinching = false, rightPinching = false;
    float leftPinchTimer = 0f, rightPinchTimer = 0f;

    const float tapThreshold = 0.5f;


    public GPTCommunication gptComm;
    //public OpenAIAPIManager openAIAPIManager;

    string promptMessage = "given the attached image, please specify a key, a tempo, a scale and a time signature for composing music. output in the following JSON format without markdown:{ \"key\" : String, \"tempo\":Integer, \"scale\":String, \"time_sig\": String} No prose.";

    string promptMessage2 = "given the attached image, please specify at least three key coordinates (in viewport space from bottom left (0,0) to top right (1,1)) that attract people's attention. output in the following JSON format without markdown:{\"key_coords\":[\"coord\": Vector2, \"description\": String]} No prose. If no attention point was found, output the center of the image and a description of the whole image.";

    string systemPrompt = "You are MusicGPT, a music creation and completion chat bot that. When a user gives you a prompt, " +
        " you return them a song showing the notes, durations, and times that they occur. Respond with just the music." + 
        "\n\nNotation looks like this:\n(Note-duration-time in beats)\nC4-1/4-0, Eb4-1/8-2.5, D4-1/4-3, F4-1/4-3 etc. No prose." ;

    public TextMeshPro gptResponseContainer;

    public struct Composition{
        public string key;
        public int tempo;
        public string scale;
        public string time_sig;
    }

    public struct Coord{
        public float x;
        public float y;
    }
    public struct KeyCoord{
        public Coord coord;
        public string description;
    }

    public struct Attends{
        public List<KeyCoord> key_coords;
    }

    public struct NoteInfo{
        public int pitch;
        public double duration;
        public float time;
    }

    private Composition composition;
    private Attends attends;
    private List<NoteInfo> noteInfos = new List<NoteInfo>();

    public struct LocationBasedMelody{
        public int tempo;
        public List<NoteInfo> melody;
        public Vector3 position;
        public GameObject locator;
    }

    public List<LocationBasedMelody> locationBasedMelody = new List<LocationBasedMelody>();

    private LocationBasedMelody currentLocationBasedMeloody;

    private int currentLocationBasedMeloodyIndex = -1;

    private string currentImageString;

    private bool callback1Finished = false, callback2Finished = false;

    //private Vector3 photoPos;
    //private Quaternion photoRot;

    private List<Vector3> gazebleLocations = new List<Vector3>();
    private List<GameObject> gazebleObjects = new List<GameObject>();

    public GameObject completePhotoButton;
    public MusicPlayer musicPlayer;

    public GameObject Gazeble;
    public GameObject locator;

    private Transform photoTakenPoint;

    private bool showPhoto;

    //private float progress;

    public GameObject progressBar;
    public Slider slider;
    public TextMeshPro message;

    private int numOfMelodyGenerated;

    string[,] notes = {{"C", ""},{"C#", "Db"},{"D", ""},{"D#", "Eb"},{"E", ""},{"F", ""},{"F#", "Gb"},{"G",""},{"G#", "Ab"},{"A",""},{"A#", "Bb"},{"B",""}};

    // Wait until an aggregator is available.
    IEnumerator EnableWhenSubsystemAvailable()
    {
        yield return new WaitUntil(() => XRSubsystemHelpers.GetFirstRunningSubsystem<HandsAggregatorSubsystem>() != null);
        
        aggregator = XRSubsystemHelpers.GetFirstRunningSubsystem<HandsAggregatorSubsystem>();
        subsystemEnabled = true;
    }

    void Start(){
        StartCoroutine(EnableWhenSubsystemAvailable());
        cameraResolution = PhotoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();
        targetTexture = new Texture2D(cameraResolution.width, cameraResolution.height, TextureFormat.BGRA32, false);
    }

    void Update(){
        if(!subsystemEnabled) return;
        
        //ProcessLeftHand();
        ProcessRightHand();
        if(Input.GetKeyDown(KeyCode.Space)){
            if(!photoModeActivated){
                PreparePhotoShot();
                GenerateTestCompletion();
            } else {
                if(!preparingPhotoShoot){
                    if(!photoTaken){
                        PhotoShoot();
                    } else {
                        CompletePhotoShoot();
                    }
                }
            }
        }
        if(leftPinching){
            leftPinchTimer += Time.deltaTime;
        }
        if(rightPinching){
            rightPinchTimer += Time.deltaTime;
        }

        if(callback1Finished && callback2Finished){
            callback1Finished = false;
            callback2Finished = false;
            slider.Value = 0.6f;
            message.text = "Generating Melodies...";
            numOfMelodyGenerated = 0;
            for(int i = 0; i < attends.key_coords.Count; i++){
                GenerateMusic(i);
            }
        }

        if(!photoModeActivated && !preparingPhotoShoot && locationBasedMelody.Count > 0){
            float shortestDistance = float.MaxValue;
            for(int i = 0; i < locationBasedMelody.Count; i++){
                float distToAnchor = Vector3.Distance(locationBasedMelody[i].position, Camera.main.transform.position);
                if(distToAnchor < 2f && distToAnchor < shortestDistance){
                    shortestDistance = distToAnchor;
                    if(currentLocationBasedMeloodyIndex != i){
                        currentLocationBasedMeloodyIndex = i;
                        MusicPlayer.Instance.currentTempo = locationBasedMelody[i].tempo;
                        MusicPlayer.Instance.currentMelody = locationBasedMelody[i].melody.ToArray();
                        if(MusicPlayer.Instance.playing)
                            MusicPlayer.Instance.StopPlay();
                        MusicPlayer.Instance.StartPlay();
                    }
                }
            }            
        }
    }

    void ProcessLeftHand(){
        bool leftPinchingNow;
        bool leftHandIsValid = aggregator.TryGetPinchProgress(XRNode.LeftHand, out bool isLeftReadyToPinch, out leftPinchingNow, out float leftPinchAmount);
        if(leftHandIsValid){
            if(!leftPinching){
                leftPinchTimer = 0f;
            } else {
                if(!leftPinchingNow){
                    if(leftPinchTimer < tapThreshold){
                        AirTap();
                    }
                }
            }
            leftPinching = leftPinchingNow;
        }else {
            leftPinching = false;
        }
    }

    void ProcessRightHand(){
        bool rightPinchingNow;
        bool rightHandIsValid = aggregator.TryGetPinchProgress(XRNode.RightHand, out bool isRightReadyToPinch, out rightPinchingNow, out float rightPinchAmount);
        if(rightHandIsValid){
            if(!rightPinching){
                rightPinchTimer = 0f;
            } else {
                if(!rightPinchingNow){
                    if(rightPinchTimer < tapThreshold){
                        AirTap();
                    }
                }
            }
            rightPinching = rightPinchingNow;
        } else {
            rightPinching = false;
        }
    }

    void AirTap(){
        DebugText.SetText("Air Tap Detected");
        if(photoModeActivated && !photoTaken){
            PhotoShoot();
        }
    }

    
    public void PreparePhotoShot()
    {
        if(preparingPhotoShoot)
            return;
        if(photoModeActivated) return;
        preparingPhotoShoot = true;
        musicPlayer.StopPlay();
        PhotoCapture.CreateAsync(false, delegate (PhotoCapture captureObject) {
            photoCapture = captureObject;
            CameraParameters cameraParameters = new CameraParameters(WebCamMode.PhotoMode);
            cameraParameters.hologramOpacity = 0.0f;
            cameraParameters.cameraResolutionWidth = cameraResolution.width;
            cameraParameters.cameraResolutionHeight = cameraResolution.height;
            cameraParameters.pixelFormat = CapturePixelFormat.BGRA32;

            // Activate the camera
            photoCapture.StartPhotoModeAsync(cameraParameters, OnStartPhotoMode);
        });

        DebugText.SetText("Photo Mode Activated!");
    }

    void OnStartPhotoMode(PhotoCapture.PhotoCaptureResult result)
    {
        if (result.success)
        {
            photoModeActivated = true;
            filename = string.Format(@"CapturedImage{0}_n.jpg", Time.time);
            filePath = System.IO.Path.Combine(Application.persistentDataPath, filename);
        }
        preparingPhotoShoot = false;
    }

    void PhotoShoot()
    {
        if (photoModeActivated && !photoTaken)
        {
            photoTaken = true;
            slider.Value = 0f;
            message.text = "Capturing Environment...";
            progressBar.SetActive(true);
            //photoUI.gameObject.SetActive(true);
            //photoCaptureObject.TakePhotoAsync(filePath, PhotoCaptureFileOutputFormat.JPG,OnCapturedPhotoToDisk);
            photoTakenPoint = new GameObject("temp").transform;
            photoTakenPoint.position = Camera.main.transform.position;
            photoTakenPoint.rotation = Camera.main.transform.rotation;
            photoCapture.TakePhotoAsync(OnCapturedPhotoToMemory);
            DebugText.SetText("Photo Taken!");
            completePhotoButton.SetActive(true);
        } 
    }
    void OnCapturedPhotoToDisk(PhotoCapture.PhotoCaptureResult result)
    {
        if (result.success)
        {
        }
        else
        {
            Debug.Log("Failed to save Photo to disk");
        }
    }
    void OnCapturedPhotoToMemory(PhotoCapture.PhotoCaptureResult result, PhotoCaptureFrame photoCaptureFrame)
    {
        if (result.success)
        {
            slider.Value = 0.2f;
            message.text = "Analyzing Environment Content...";

            // Copy the raw image data into our target texture
            photoCaptureFrame.UploadImageDataToTexture(targetTexture);
            
            if(showPhoto){
                targetTexture.wrapMode = TextureWrapMode.Clamp;
                // Create a gameobject that we can apply our texture to

                photoQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                photoQuad.transform.parent =  Camera.main.transform;
                photoQuad.transform.localPosition = new Vector3(0f, -0.1f, 1f);
                photoQuad.transform.localScale = new Vector3((float)cameraResolution.width / cameraResolution.height, 1f, 1f) * 0.3f;
                photoQuad.transform.localEulerAngles = Vector3.zero;
                photoQuad.transform.parent = null;
//  
                Renderer quadRenderer = photoQuad.GetComponent<Renderer>() as Renderer;
                quadRenderer.material = photoMaterial;
                quadRenderer.sharedMaterial.SetTexture("_MainTex", targetTexture);
            }
            
            byte[] photoData = targetTexture.EncodeToPNG();
            currentImageString = Convert.ToBase64String(photoData);
            gptComm.ClearHistory();

            Action<string> gptCallback = ProcessCallback;
            Action<string> gptCallback2 = ProcessCallback2;
            gptComm.GenerateCompletionForImage(promptMessage, currentImageString, gptCallback);
            gptComm.GenerateCompletionForImage(promptMessage2, currentImageString, gptCallback2);
        }
    }

    void ProcessCallback(string result){
        if(!photoModeActivated){
            return;
        }
        gptResponseContainer.text = result;
        composition = JsonConvert.DeserializeObject<Composition>(result);
        Debug.Log(composition.time_sig);
        callback1Finished = true;
        slider.Value = 0.4f;
        message.text = "Detecting Key Objects...";
    }

    void ProcessCallback2(string result){
        if(!photoModeActivated){
            return;
        }
        attends = JsonConvert.DeserializeObject<Attends>(result);
        Debug.Log(result);
        Debug.Log(attends.key_coords[0].coord.x +", "+attends.key_coords[0].coord.y);
        //string debugtext = "";
        foreach(KeyCoord key_coord in attends.key_coords){
            if(showPhoto){
                Vector3 viewportPos = new Vector3(key_coord.coord.x - 0.5f, key_coord.coord.y - 0.5f, -0.05f);
                gazebleLocations.Add(viewportPos);
            } else {
                Vector3 viewportPos = new Vector3(key_coord.coord.x, key_coord.coord.y, 1.5f);
                Vector3 worldSpacePos = Camera.main.ViewportToWorldPoint(viewportPos);
                Vector3 objectSpacePos = Camera.main.transform.InverseTransformPoint(worldSpacePos);
                Vector3 realWorldSpacePos = photoTakenPoint.TransformPoint(objectSpacePos);
                gazebleLocations.Add(realWorldSpacePos);
            }
        }
        gptResponseContainer.text = result;
        callback2Finished = true;
        slider.Value = 0.4f;
        message.text = "Generating Music Composition...";
    }

    void GenerateMusic(int index){
        gptComm.ClearHistory();
        gptComm.SetSystemPrompt(systemPrompt);
        Action<string, int> gptCallback = ProcessCallback3;
        string promptMessageMusic = "Compose a looping melody with the given image and the following descriptions: \n" 
            + "key: " + composition.key + ", tempo: " + composition.tempo + ", scale: " + composition.scale + ", time signature: " + composition.time_sig + ", description: " + attends.key_coords[index].description;
        gptComm.GenerateCompletionForImage(promptMessageMusic, currentImageString, index, gptCallback);
    }

    void ProcessCallback3(string result, int index){
        if(!photoModeActivated){
            return;
        }
        gptResponseContainer.text = result;
        noteInfos.Clear();
        foreach(Match match in Regex.Matches(result, @"(?<![A-Za-z\d])([A-G](?:#|b)?\d(?:-\d+(?:\/\d+)?(?:-\d+(?:\.\d+)?)?)+)(?![A-Za-z\d])", RegexOptions.IgnoreCase)){
            string[] infos = match.Value.Split("-");
            noteInfos.Add(new NoteInfo(){
                pitch = NoteToPitch(infos[0]), 
                duration = new Fraction(infos[1]).ToDouble(),
                time = float.Parse(infos[2])
            });
        }

        
        musicPlayer.currentTempo = composition.tempo;
        if(showPhoto){
            GameObject gazebleObj = Instantiate(Gazeble, photoQuad.transform);
            gazebleObj.transform.localPosition = gazebleLocations[index];
            gazebleObj.transform.localScale = new Vector3((float)cameraResolution.height / cameraResolution.width, 1f, 1f) * 0.1f;
            Transform descriptionTransform = gazebleObj.GetComponent<GazeableMelody>().descriptionObject.transform;
            descriptionTransform.localEulerAngles = Vector3.zero;
            gazebleObj.GetComponent<GazeableMelody>().descriptionObject.transform.localScale = descriptionTransform.localScale * 2f;
            gazebleObj.GetComponent<GazeableMelody>().melody = noteInfos.ToArray();
            gazebleObj.GetComponent<GazeableMelody>().bpm = composition.tempo;
            gazebleObj.GetComponent<GazeableMelody>().description = attends.key_coords[index].description;
            gazebleObjects.Add(gazebleObj);
        } else {
            GameObject gazebleObj = Instantiate(Gazeble);
            gazebleObj.transform.position = gazebleLocations[index];
            gazebleObj.transform.localScale = Vector3.one *  0.1f;
            gazebleObj.transform.LookAt(new Vector3(Camera.main.transform.position.x, gazebleObj.transform.position.y, Camera.main.transform.position.z));
            gazebleObj.GetComponent<GazeableMelody>().melody = noteInfos.ToArray();
            gazebleObj.GetComponent<GazeableMelody>().bpm = composition.tempo;
            gazebleObj.GetComponent<GazeableMelody>().description = attends.key_coords[index].description;
            gazebleObjects.Add(gazebleObj);
        }

        foreach(NoteInfo note in noteInfos)
            Debug.Log(note.pitch + ", " + note.duration + ", " + note.time);
        
        numOfMelodyGenerated++;
        if(numOfMelodyGenerated == 1){
            currentLocationBasedMeloody = new LocationBasedMelody(){
                position = photoTakenPoint.position,
                melody = new List<NoteInfo>(),
                tempo = composition.tempo
            };
        } else {
            NoteInfo lastNote = currentLocationBasedMeloody.melody.Last();
            float endTime = (float)lastNote.duration / 0.25f + lastNote.time;
            for(int i = 0; i < noteInfos.Count; i++){
                NoteInfo obj = noteInfos[i];
                obj.time += endTime;
                noteInfos[i] = obj;
            }
        } 
        currentLocationBasedMeloody.melody.AddRange(noteInfos);
        
        slider.Value = 0.6f + 0.4f * numOfMelodyGenerated/gazebleLocations.Count;
        if(index >= gazebleLocations.Count -1){
            message.text = "Completed!";
            foreach(LocationBasedMelody obj in locationBasedMelody){
                if(Vector3.Distance(obj.position, photoTakenPoint.position) < 2f){
                    if(obj.locator){
                        Destroy(obj.locator);
                    }
                    locationBasedMelody.Remove(obj);
                }
            }
            GameObject loc = Instantiate(locator);
            loc.transform.position = photoTakenPoint.position;
            currentLocationBasedMeloody.locator = loc;
            loc.SetActive(false);
            locationBasedMelody.Add(currentLocationBasedMeloody);
        }
    }


    int NoteToPitch(string note){
        int oct = int.Parse(note[note.Length-1].ToString());
        string letter = note.Substring(0, note.Length-1);
        int index = 0;
        for(int i = 0; i < notes.GetLength(0); i++){
            for(int j = 0; j < notes.GetLength(1); j++){
                if(letter == notes[i,j]){
                    index = i;
                }
            }
        }
        return index+oct*12+12;
    }


    public void CompletePhotoShoot()
    {
        if(!photoModeActivated) return;
        if(!photoTaken) return;
        photoCapture.StopPhotoModeAsync(OnStoppedPhotoMode);
        photoModeActivated = false;
        photoTaken = false;
        DebugText.SetText("Photo Finished!");
        gazebleLocations.Clear();
        foreach(GameObject obj in gazebleObjects){
            Destroy(obj);
        }
        if(locationBasedMelody.Count>0){
            locationBasedMelody.Last().locator.SetActive(true);
        }
        gazebleObjects.Clear();
        musicPlayer.StopPlay();
        progressBar.SetActive(false);
        Destroy(photoTakenPoint.gameObject);
        //uiController.SetUIVisible(true);
        //photoUI.gameObject.SetActive(false);
        if(photoQuad)
            //photoQuad.SetActive(false);
            Destroy(photoQuad);
    }

    void OnStoppedPhotoMode(PhotoCapture.PhotoCaptureResult result)
    {
        // Shutdown our photo capture resource
        photoCapture.Dispose();
        photoCapture = null;
    }

    public void GenerateTestCompletion(){
        Action<string> gptCallback = TestCallback;
        gptComm.GenerateTestCompletion(gptCallback);
        //openAIAPIManager.GenerateCompletion("Hi please introduce yourself.", gptCallback);
    }

    void TestCallback(string text){
        gptResponseContainer.text = text;
    }

    public void ShowPhoto(bool active){
        showPhoto = active;
    }

}


