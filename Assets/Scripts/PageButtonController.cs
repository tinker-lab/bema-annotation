using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PageButtonController : MonoBehaviour {

    public GameObject panels;
    public GameObject panelText;

    private bool isLeftButton;
    private int currentPanel;
    private int numPanels;

	// Use this for initialization
	void Start () {

        isLeftButton = (this.gameObject.name == "LeftPageButton") ?  true : false;  // Determine which button this is

        this.GetComponent<Button>().onClick.AddListener(() => switchPage());    // Add event listener
        this.tag = LoadImages.BUTTON_TAG;

        numPanels = panels.transform.childCount;
        print("Num panels: " + numPanels);
        panelText.GetComponent<TextMesh>().text = "Page 1/" + numPanels;
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    void switchPage()
    {
        currentPanel = panels.GetComponent<DataPanelState>().GetCurrentPanel();
        numPanels = panels.transform.childCount;

        print("num panels" + numPanels);

        if (isLeftButton)
        {
            if (currentPanel == 0)
            {
                return;
            }
            else
            {
                panels.transform.Find("Panel " + currentPanel).gameObject.SetActive(false);
                currentPanel--;
                panels.transform.Find("Panel " + currentPanel).gameObject.SetActive(true);
                panelText.GetComponent<TextMesh>().text = "Page " + (currentPanel + 1) + "/" + numPanels;
            }
        }
        else
        {
            if (currentPanel == numPanels - 1)
            {
                return;
            }
            else
            {
                panels.transform.Find("Panel " + currentPanel).gameObject.SetActive(false);
                currentPanel++;
                panels.transform.Find("Panel " + currentPanel).gameObject.SetActive(true);
                panelText.GetComponent<TextMesh>().text = "Page " + (currentPanel + 1) + "/" + numPanels;
            }
        }

        panels.GetComponent<DataPanelState>().SetCurrentPanel(currentPanel);

        this.GetComponent<Outline>().enabled = false;
        print("num panels" + numPanels);
    }
}
