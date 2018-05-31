 using UnityEngine;
 using System.Collections;
 
 public class HiResScreenShots : MonoBehaviour
{
    public int resWidth = 3840;
    public int resHeight = 2160;

    private bool takeHiResShot = false;

    public GameObject controller0;
    public GameObject controller1;

    //private ControllerInfo controller0Info;
    //private ControllerInfo controller1Info;

    void Init()
    {
        //controller0Info = new ControllerInfo(controller0);
        //controller1Info = new ControllerInfo(controller1);
    }

    public static string ScreenShotName(int width, int height)
    {
        return string.Format("screenshots/screen_{1}x{2}_{3}.png",
                             Application.dataPath,
                             width, height,
                             System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
    }

    public void TakeHiResShot()
    {
        takeHiResShot = true;
    }

    void LateUpdate()
    {
        takeHiResShot |= Input.GetKeyDown("k");

        if (controller0.GetComponent<SteamVR_TrackedObject>().index != SteamVR_TrackedObject.EIndex.None && controller1.GetComponent<SteamVR_TrackedObject>().index != SteamVR_TrackedObject.EIndex.None)
        {
            takeHiResShot |= SteamVR_Controller.Input((int)controller0.GetComponent<SteamVR_TrackedObject>().index).GetPressDown(SteamVR_Controller.ButtonMask.Grip);
            takeHiResShot |= SteamVR_Controller.Input((int)controller1.GetComponent<SteamVR_TrackedObject>().index).GetPressDown(SteamVR_Controller.ButtonMask.Grip);
        }

        
        if (takeHiResShot)
        {
            RenderTexture rt = new RenderTexture(resWidth, resHeight, 24);
            GetComponent<Camera>().targetTexture = rt;
            Texture2D screenShot = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
            GetComponent<Camera>().Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
            GetComponent<Camera>().targetTexture = null;
            RenderTexture.active = null; // JC: added to avoid errors
            Destroy(rt);
            byte[] bytes = screenShot.EncodeToPNG();
            string filename = ScreenShotName(resWidth, resHeight);
            System.IO.File.WriteAllBytes(filename, bytes);
            Debug.Log(string.Format("Took screenshot to: {0}", filename));
            takeHiResShot = false;
        }
    }
}