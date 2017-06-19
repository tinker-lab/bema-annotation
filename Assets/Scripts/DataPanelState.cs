using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DataPanelState : MonoBehaviour {

    private int currentPanel;
    private int numPanels;

	// Use this for initialization
	void Start () {
        currentPanel = 0;
        numPanels = 0;
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    public int GetCurrentPanel()
    {
        return currentPanel;
    }

    public void SetCurrentPanel(int currentPanel)
    {
        this.currentPanel = currentPanel;
    }

    public int GetNumPanels()
    {
        return numPanels;
    }

    public void SetNumPanels(int numPanels)
    {
        this.numPanels = numPanels;
    }
}
