using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ViewPlaneController : MonoBehaviour {

    void Start()
    {
        GetComponent<Renderer>().material.SetTextureScale("_MainTex", new Vector2(-1, 1));
    }
}
