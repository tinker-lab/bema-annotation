using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Linq;
using ImageMagick;

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
    public string resourceFolderPath;
    public string convertedFilesPath;

    private FileInfo[] resourceFiles;
    private int panelCount;
    private float cellWidth;
    private float cellHeight;

    void Start()
    {

        DirectoryInfo resourceFolder = new DirectoryInfo(resourceFolderPath);
        var resourceFiles = resourceFolder.GetFiles().Where(name => !(name.Extension == ".meta"));        // Only retrieves files without the .meta extension, which is something that unity creates I believe

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

        foreach (FileInfo resource in resourceFiles)
        {

            // Make button and load sprite version onto it
            GameObject image2D = Instantiate(buttonPrefab, uiPanel.transform);
            Button image2DButton = image2D.GetComponent<Button>();
            image2DButton.image.preserveAspect = true;
            image2DButton.image.sprite = LoadResource(resource);

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
            buttonOutline.effectDistance = new Vector2(0.05f, 0.05f);

            // Give button descriptive name
            image2DButton.name = Path.GetFileNameWithoutExtension(resource.FullName);

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


    /*
     * Takes a resource file, and loads it differently depending on what its file type is
     */
    Sprite LoadResource(FileInfo resource)
    {
        if (resource.Extension == ".pdf")
        {
            Debug.Log("Pdf was found");
            return LoadPDF(resource);
        }
        else    // Load as JPG if no other file type can be found
        {
            return LoadJPG(resource);
        }
    }

    /*
     * Returns a sprite version of the first page of a pdf, which can be placed on a button
     */ 
    Sprite LoadPDF(FileInfo pdf)
    {
        string fileWithNewExtension = Path.GetFileNameWithoutExtension(pdf.FullName) + ".jpg";   // Make a path for the converted version
        string newFilePath = Path.Combine(convertedFilesPath, fileWithNewExtension);             //

        byte[] data;    // Data of new file
        using (MagickImageCollection collection = new MagickImageCollection())
        {
            MagickReadSettings settings = new MagickReadSettings();
            settings.FrameIndex = 0; // Start at first page
            settings.FrameCount = 1; // Number of pages to read
            settings.Density = new Density(600, 600);
;
            // Read only the first page of the pdf file
            collection.Read(pdf, settings);

            // Read image from file
            MagickImage image = (MagickImage)collection.ElementAt(0);
            
            // Sets the output format to jpeg
            image.Format = MagickFormat.Jpeg;
            //image.Density = 
            // Create byte array that contains a jpeg file
            data = image.ToByteArray();
        }

        if (!File.Exists(newFilePath))   // If a converted version doesn't already exist, make a new file
        {
            File.WriteAllBytes(newFilePath, data);
        }

        return LoadJPG(new FileInfo(newFilePath));
    }

    /*
     * Returns a sprite version of a jpg, which can be placed on a unity button
     */
    Sprite LoadJPG(FileInfo jpg)
    {
        // Load image into Texture2D
        Texture2D texture = new Texture2D(1, 1);
        string filePath = Path.GetFullPath(jpg.FullName);
        texture.LoadImage(File.ReadAllBytes(filePath));

        // Make a sprite version
        Sprite tempSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);

        return tempSprite;
    }
}
