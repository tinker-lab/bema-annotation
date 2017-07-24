using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RejectController : MonoBehaviour {

	// Use this for initialization
	void Start () {

        this.GetComponent<Button>().onClick.AddListener(() => Reject());

    }
	
    void Reject()
    {
        GameObject.Find("UIController").GetComponent<UIController>().ChangeState(new StartState());

    }



	// Update is called once per frame
	void Update () {
		
	}
}
