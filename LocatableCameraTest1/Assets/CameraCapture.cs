// ReSharper disable CheckNamespace

using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.VR.WSA.Input;
using UnityEngine.VR.WSA.WebCam;

/// <summary>
/// Camera script that will initialize the camera system and take pictures with each Tap gesture and Fire1 press
/// </summary>
public class CameraCapture : MonoBehaviour
{

    public Text TextObject;

    private PhotoCapture _photoCaptureObject;
    private GestureRecognizer _gestureRecognizer;
    private bool _photoModeStarted;
    
    void Start ()
    {
        Debug.Log("Start - CameraCapture");
        Debug.Log("Pictures will be stored at: " + Application.persistentDataPath);

        _gestureRecognizer = new GestureRecognizer();
        _gestureRecognizer.TappedEvent += (source, tapCount, ray) =>
        {
            TakePicture();
        };
        _gestureRecognizer.StartCapturingGestures();

        PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
    }

    void Update()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            TakePicture();
        }
    }

    void TakePicture()
    {
        if (_photoModeStarted)
        {
            string filename = string.Format(@"CapturedImage{0}_n.jpg", Time.time);
            string filePath = System.IO.Path.Combine(Application.persistentDataPath, filename);

            _photoCaptureObject.TakePhotoAsync(filePath, PhotoCaptureFileOutputFormat.JPG, OnCapturedPhotoToDisk);
        }
    }

    void OnPhotoCaptureCreated(PhotoCapture captureObject)
    {
        Debug.Log("OnPhotoCaptureCreated");
        _photoCaptureObject = captureObject;

        //TODO: Try enumerating all of the support resolutions (in OnStart), we may not need the highest resolution for what we're doing
        //Grab highest resolution camera
        Resolution cameraResolution = PhotoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();

        CameraParameters c = new CameraParameters
        {
            hologramOpacity = 0.0f,
            cameraResolutionWidth = cameraResolution.width,
            cameraResolutionHeight = cameraResolution.height,
            pixelFormat = CapturePixelFormat.BGRA32
        };

        captureObject.StartPhotoModeAsync(c, false, OnPhotoModeStarted);
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

    void OnCapturedPhotoToDisk(PhotoCapture.PhotoCaptureResult result)
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

    private void SetDebugText(string text)
    {
        if (TextObject != null)
        {
            TextObject.text = text;
        }
    }

    void OnStoppedPhotoMode(PhotoCapture.PhotoCaptureResult result)
    {
        _photoCaptureObject.Dispose();
        _photoCaptureObject = null;
    }
}
