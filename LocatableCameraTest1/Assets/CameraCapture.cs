// ReSharper disable CheckNamespace

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using System.Linq;
using UnityEditor.VersionControl;
using UnityEngine.UI;
using UnityEngine.VR.WSA.Input;
using UnityEngine.VR.WSA.WebCam;
using Debug = UnityEngine.Debug;

/// <summary>
/// Camera script that will initialize the camera system and take pictures.
/// </summary>
public class CameraCapture : MonoBehaviour
{
    //TODO: Nonsensical to give knowledge of the Text element to this Behavior, should be abstracted (e.g. piggybacking off Debug.Log)
    public Text DebugText;
    [Tooltip("Enable automatic capturing of camera images.")]
    public bool AutoCapture;
    [Tooltip("Time in milliseconds to wait between image captures.")]
    [Range(0, 5000)]
    public int RefreshMs = 100;
    [Tooltip("Specifies what method of capture should be used.\r\n\nFile: Save to file on disk.\r\nSceneObject: Apply as texture to a new object in the scene.")]
    public CaptureModeType CaptureMode;

    private PhotoCapture _photoCaptureObject;
    private GestureRecognizer _gestureRecognizer;
    private bool _photoModeStarted;
    private bool _autoCaptureStarted;
    private List<GameObject> _gameObjects = new List<GameObject>();
    
    private void Start ()
    {
        Debug.Log("Start - CameraCapture");
        Debug.Log("Pictures will be stored at: " + Application.persistentDataPath);

        PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);

        if (AutoCapture)
        {
            StartAutomaticCapture();
        }
    }

    public void StartAutomaticCapture()
    {
        if (_autoCaptureStarted)
        {
            return;
        }

        _autoCaptureStarted = true;
        StartCoroutine(AutomaticCaptureRoutine());
    }

    public void StopAutomaticCapture()
    {
        if (!_autoCaptureStarted)
        {
            return;
        }

        _autoCaptureStarted = false;
        StopCoroutine("AutomaticCaptureRoutine");
    }

    public void TakePicture()
    {
        Debug.Log("Taking picture...");
        if (_photoModeStarted && _photoCaptureObject != null)
        {
            if (CaptureMode == CaptureModeType.File)
            {
                var filename = string.Format(@"CapturedImage{0}_n.jpg", Time.time);
                var filePath = System.IO.Path.Combine(Application.persistentDataPath, filename);
                _photoCaptureObject.TakePhotoAsync(filePath, PhotoCaptureFileOutputFormat.JPG, OnCapturedPhotoToDisk); 
            }
            else
            {
                _photoCaptureObject.TakePhotoAsync(OnCapturedPhotoToMemory);
            }
        }
    }


    private IEnumerator AutomaticCaptureRoutine()
    {
        var processingTime = 0f;
        while (true)
        {
            var refreshInSeconds = RefreshMs / 1000f; //recalc every loop in case code changes this value in the background
            yield return new WaitForSeconds(Mathf.Max(refreshInSeconds - processingTime, 0f));

            //Track start and end time, subtract the total image processing time from the RefreshMS value above to level off processing rate
            var startTime = Time.time;

            //Acquire an image
            TakePicture();

            //Process the image to find tags
            var job = new FindImageTagsJob();
            job.Start();

            //Wait for the processing to complete
            yield return job.WaitFor();
            Debug.Log("Async Processing Done.");

            //Job processing is now done, collect and process results

            //See above, tracking processing time
            var endTime = Time.time;
            processingTime = endTime - startTime;
            if (processingTime > refreshInSeconds)
            {
                Debug.LogWarning("Image processing is falling behind desired refresh rate.");
            }

            if (!AutoCapture)
            {
                break;
            }
        }
    }

    private void OnPhotoCaptureCreated(PhotoCapture captureObject)
    {
        Debug.Log("OnPhotoCaptureCreated");
        _photoCaptureObject = captureObject;

        //TODO: Try enumerating all of the support resolutions (in OnStart), we may not need the highest resolution for what we're doing
        //Grab highest resolution camera
        var cameraResolution = PhotoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();

        var cameraParameters = new CameraParameters
        {
            hologramOpacity = 0.0f,
            cameraResolutionWidth = cameraResolution.width,
            cameraResolutionHeight = cameraResolution.height,
            pixelFormat = CapturePixelFormat.BGRA32
        };

        captureObject.StartPhotoModeAsync(cameraParameters, false, OnPhotoModeStarted);
    }

    private void OnPhotoModeStarted(PhotoCapture.PhotoCaptureResult result)
    {
        Debug.Log("OnPhotoModeStarted");
        if (result.success)
        {
            Debug.Log("OnPhotoModeStarted - success");
            _photoModeStarted = true;

            SetDebugText("Ready to take picture.");
        }
        else
        {
            Debug.LogError("Unable to start photo mode!");
        }
    }

    private void OnCapturedPhotoToDisk(PhotoCapture.PhotoCaptureResult result)
    {
        if (result.success)
        {
            Debug.Log("Saved Photo to disk!");
            
            //TODO: Determine if it's better to stop and restart camera mode with each "click" or keep it running constantly
            //Disabled this line for now, we either need to start camera mode in TakePicture() and stop it here or keep it running all the time
            //photoCaptureObject.StopPhotoModeAsync(OnStoppedPhotoMode);

            SetDebugText("Picture taken!  Ready to take another picture.");
        }
        else
        {
            Debug.Log("Failed to save Photo to disk");
        }
    }

    private void OnCapturedPhotoToMemory(PhotoCapture.PhotoCaptureResult result, PhotoCaptureFrame photoCaptureFrame)
    {
        if (result.success)
        {
            // Create our Texture2D for use and set the correct resolution
            Resolution cameraResolution = PhotoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();
            Texture2D targetTexture = new Texture2D(cameraResolution.width, cameraResolution.height);
            // Copy the raw image data into our target texture
            photoCaptureFrame.UploadImageDataToTexture(targetTexture);

            // The frame will only have location information if we're using a LocatableCamera (your computer camera doesn't qualify)
            Matrix4x4 cameraToWorld;
            if (!photoCaptureFrame.TryGetCameraToWorldMatrix(out cameraToWorld))
            {
                Debug.Log("Unable to acquire camera to world matrix, falling back to main camera position");
                cameraToWorld = Camera.main.cameraToWorldMatrix;
            }

            //TODO: This creates a square quad but the captured image probably isn't square.  Should resize quad to match the proportions of the image.
            GameObject quadGameObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadGameObject.transform.position = cameraToWorld.MultiplyPoint(Vector3.zero); //place the new object at the location of the camera
            quadGameObject.transform.forward = cameraToWorld.MultiplyVector(Vector3.back); //use back so the image is facing the camera

            var quadRenderer = quadGameObject.GetComponent<Renderer>();
            if (quadRenderer != null)
            {
                quadRenderer.material.mainTexture = targetTexture;
            }

            //Add a list for easy access/cleanup later.
            _gameObjects.Add(quadGameObject);
        }
        
        // Clean up
        // TODO: Determine best pattern for enabling/disable photo mode.
        //_photoCaptureObject.StopPhotoModeAsync(OnStoppedPhotoMode);
    }

    private void SetDebugText(string text)
    {
        if (DebugText != null)
        {
            DebugText.text = text;
        }
    }

    private void OnStoppedPhotoMode(PhotoCapture.PhotoCaptureResult result)
    {
        _photoCaptureObject.Dispose();
        _photoCaptureObject = null;
    }
}
