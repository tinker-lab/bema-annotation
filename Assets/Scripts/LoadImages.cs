using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Linq;

public class LoadImages : MonoBehaviour
{
    public static readonly string RESOURCE_TAG = "resource"; 

    public int rowsPerPanel;
    public int columnsPerPanel;
    public float spacing;
    public float padding;
    public GameObject panels;
    public GameObject uiPanel;
    public GameObject buttonPrefab;
    public string imageFolderPath;

    private FileInfo[] imageFiles;
    private int panelCount;
    private float cellWidth;
    private float cellHeight;

    void Start()
    {

        DirectoryInfo imageFolder = new DirectoryInfo(imageFolderPath);
        var imageFiles = imageFolder.GetFiles().Where(name => !(name.Extension == ".meta"));        // Only retrieves files without the .meta extension, which is something that unity creates I believe

        // Calculate ideal cell width and height based on spacing/padding
        float mainPanelWidth = uiPanel.GetComponent<RectTransform>().rect.width;
        float mainPanelHeight = uiPanel.GetComponent<RectTransform>().rect.height;
        float cellPanelWidth = mainPanelWidth - (padding * 2);                          // Dimensions for actual space occupied by cells in each panel
        float cellPanelHeight = mainPanelHeight - (padding * 2);                        //
        cellWidth = (cellPanelWidth - (spacing * (columnsPerPanel - 1))) / columnsPerPanel;
        cellHeight = (cellPanelHeight - (spacing * (rowsPerPanel - 1))) / rowsPerPanel;

        panelCount = 0;
        int resourcesOnCurrentPanel = 0;
        GameObject currentPanel = makeNewPanel();

        foreach (FileInfo image in imageFiles)
        {
            // Load image into Texture2D
            Texture2D texture = new Texture2D(1, 1);
            string filePath = Path.GetFullPath(image.FullName);
            texture.LoadImage(File.ReadAllBytes(filePath));

            // Make a sprite version
            Sprite tempSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);

            // Make button and load sprite version onto it
            GameObject image2D = Instantiate(buttonPrefab, uiPanel.transform);
            Button image2DButton = image2D.GetComponent<Button>();
            image2DButton.image.sprite = tempSprite;

            // Make button clickable
            image2DButton.onClick.AddListener(() => onResourceClicked(image2DButton));
            image2D.tag = RESOURCE_TAG;
            image2D.layer = uiPanel.layer;

            BoxCollider boxCollider = image2D.gameObject.AddComponent<BoxCollider>();
            float buttonWidth = currentPanel.GetComponent<GridLayoutGroup>().cellSize.x;
            float buttonHeight = currentPanel.GetComponent<GridLayoutGroup>().cellSize.y;
            boxCollider.size = new Vector3(buttonWidth, buttonHeight, 0.005f);              

            // Make button highlightable
            Outline buttonOutline = image2DButton.image.gameObject.AddComponent<Outline>();
            buttonOutline.enabled = false;
            buttonOutline.effectDistance = new Vector2(0.01f, 0.01f);

            // Give button descriptive name
            image2DButton.name = image.Name + " button";

            if (resourcesOnCurrentPanel >= (columnsPerPanel * rowsPerPanel))
            {
                currentPanel.transform.SetParent(panels.transform);
                resetTransform(currentPanel);
                currentPanel = makeNewPanel();
                resourcesOnCurrentPanel = 0;
            }

            image2D.transform.SetParent(currentPanel.transform);
            resetTransform(image2D);
            resourcesOnCurrentPanel++;
        }

        if (!currentPanel.transform.IsChildOf(panels.transform))
        {
            currentPanel.transform.SetParent(panels.transform);
            resetTransform(currentPanel);
            //panels.GetComponent<DataPanelState>().SetNumPanels(panelCount);
        }
    }

    // Update is called once per frame
    void Update()
    {

    }

    /// <summary>
    /// On click method for all annotation resources
    /// </summary>
    /// <param name="button"></param>
    public void onResourceClicked(Button button)
    {
        print(button.name + " was clicked");
    }

    /// <summary>
    /// Returns a panel that is ready to be filled up with resources
    /// </summary>
    /// <returns></returns>
    GameObject makeNewPanel()
    {
        GameObject panel = new GameObject("Panel " + panelCount);

        if (panelCount == 0)
        {
            panel.SetActive(true);
        }
        else
        {
            panel.SetActive(false);
        }

        panelCount++;
        //panels.GetComponent<DataPanelState>().SetNumPanels(panelCount);

        panel.AddComponent<CanvasRenderer>();

        RectTransform rectT = panel.AddComponent<RectTransform>();
        float rectWidth = rectT.rect.width;
        float rectHeight = rectT.rect.height;
        rectWidth = uiPanel.GetComponent<RectTransform>().rect.width;
        rectHeight = uiPanel.GetComponent<RectTransform>().rect.height;


        GridLayoutGroup gridLayout = panel.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(cellWidth, cellHeight); // Set cell size appropriately (TODO: make this size correctly for panel)
        //gridLayout.padding.left = padding;        //
        //gridLayout.padding.top = padding;        // Adjust padding
        gridLayout.spacing = new Vector2(spacing, spacing);
        gridLayout.startAxis = GridLayoutGroup.Axis.Vertical;
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        gridLayout.constraintCount = rowsPerPanel;
        gridLayout.childAlignment = TextAnchor.MiddleCenter;

        return panel;
    }

    void resetTransform(GameObject obj)
    {
        obj.transform.localRotation = Quaternion.identity; 
        obj.transform.localPosition = Vector3.zero;        
        obj.transform.localScale = Vector3.one;            
    }

    int getPanelCount()
    {
        return panelCount;
    }

}
