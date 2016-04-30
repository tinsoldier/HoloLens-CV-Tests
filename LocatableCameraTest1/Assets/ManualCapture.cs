// ReSharper disable CheckNamespace

using UnityEngine;
using UnityEngine.VR.WSA.Input;

public class ManualCapture : MonoBehaviour
{
    private CameraCapture _cameraCapture;
    private GestureRecognizer _gestureRecognizer;

    void Start()
    {
        _cameraCapture = GetComponent<CameraCapture>();

        _gestureRecognizer = new GestureRecognizer();
        _gestureRecognizer.TappedEvent += (source, tapCount, ray) =>
        {
            TakePicture();
        };
        _gestureRecognizer.StartCapturingGestures();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            TakePicture();
        }
    }

    private void TakePicture()
    {
        _cameraCapture.TakePicture();
    }
}
