using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ScreenshotCamera : MonoBehaviour
{
    public int FileCounter = 0;
    public RenderTexture RT;
    public string FilePath = "ScreenshotTest"; // of the form "/folder1/folder2/.../folderN/"
    public string FileName = "capture"; // of the form "name" (NO EXTENSION)
    private Camera ScreenshotCam;

    private void Awake()
    {
        FileCounter = 0;
        ScreenshotCam = GetComponent<Camera>();

        //Debug.Log(RT.sRGB + " sRGB test"); // @TO-DO: this returns False, but ideally should be True, yet read-only ??
        ScreenshotCam.targetTexture = new RenderTexture(RT.width, RT.height, RT.depth, RT.format, RenderTextureReadWrite.sRGB);

        Debug.Log(ScreenshotCam.targetTexture);
        Debug.Log(ScreenshotCam.targetTexture.width);
        Debug.Log(ScreenshotCam.targetTexture.height);

        Activate(false);
    }

    // called by self or other object to activate (or deactivate!) screenshot camera
    // which will cause it to capture a single screenshot (see LateUpdate())
    public void Activate(bool toggle=true)
    {
        // enable camera ready to be picked up by LateUpdate() call
        ScreenshotCam.enabled = toggle;
    }

    /*
     * Activation for screenshot on key press is currently handled by PlayerControls
     * Uncomment this section to migrate screenshot management to self
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9))
        {
            Debug.Log("ScreenshotCam activated");
            // activate camera ready to be picked up by LateUpdate() call
            ScreenshotCam.Activate();
        }
    } */

    // LateUpdate is generally best for camera updates bc it's post-movement-computation
    private void LateUpdate()
    {
        // if we have activated to capture a frame, then capture it
        if (ScreenshotCam.enabled)
        {
            CameraCapture();
            Debug.Log("capturing from ScreenshotCamera . . .");
            // deactivate camera now we have captured
            Activate(false);
        }
    }

    void CameraCapture()
    {

        // actually need to tell it to render because it won't have done up to this point
        ScreenshotCam.Render();
        RenderTexture.active = ScreenshotCam.targetTexture;
        Debug.Log(RenderTexture.active + (RenderTexture.active!=null ? " SUCCESS !" : " FAILURE"));

        Texture2D image = new Texture2D(ScreenshotCam.targetTexture.width, ScreenshotCam.targetTexture.height, TextureFormat.RGB24, true);
        image.ReadPixels(new Rect(0, 0, ScreenshotCam.targetTexture.width, ScreenshotCam.targetTexture.height), 0, 0);
        //image.Apply();
        byte[] bytes = image.EncodeToPNG();
        Destroy(image);

        string path = string.Format("{0}/{1}/{2}{3}_{4}.png",
            Application.dataPath,
            FilePath, FileName, FileCounter,
            System.DateTime.Now.ToString("dd-MM_HH-mm-ss"));
        Debug.Log(path);
        File.WriteAllBytes(path, bytes);
        FileCounter++;
    }
}
