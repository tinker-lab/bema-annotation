using UnityEngine;
using System.IO;

/**
 * A component to be attached to buttons in PickResourceState so that higher quality versions of images can be loaded
 */ 
public class ImageInfo : MonoBehaviour{

    private Sprite sprite;

    public Sprite GetImageSprite()
    {
        return this.sprite;
    }

    public void SetImageSprite(Sprite sprite)
    {
        this.sprite = sprite;
    }
}
