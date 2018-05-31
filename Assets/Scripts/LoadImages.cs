using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Linq;
using ImageMagick;
using GhostscriptSharp;
using System.Text.RegularExpressions;
using System;

public class LoadImages : MonoBehaviour
{
    public static readonly string RESOURCE_TAG = "resource";
    public static string resourceFolderPath;
    public static string convertedFilesPath; 

    public int rowsPerPanel;
    public int columnsPerPanel;
    public float spacing;
    public float padding;
    public GameObject panels;
    public GameObject uiPanel;
    public GameObject buttonPrefab;

    private FileInfo[] resourceFiles;
    private int panelCount;
    private float cellWidth;
    private float cellHeight;

    void Start()
    {
        resourceFolderPath = Application.dataPath + "/Photos/original_photos";
        convertedFilesPath = Application.dataPath + "/Photos/converted_photos";

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

            Sprite tempImageSprite = LoadResource(resource, convertedFilesPath);
            image2DButton.image.sprite = tempImageSprite;

            // Give button a reference to image file path
            image2DButton.gameObject.AddComponent<ImageInfo>();
            image2DButton.GetComponent<ImageInfo>().SetImageSprite(tempImageSprite);

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
    public static Sprite LoadResource(FileInfo resource, string convertedFilesPath)
    {
        if (resource.Extension == ".pdf")
        {
            //Debug.Log("Pdf was found");
            return LoadPDF(resource, convertedFilesPath);
        }
        else    // Load as JPG if no other file type can be found
        {
            return LoadImage(resource);
        }
    }

    /*
     * Returns a sprite version of the first page of a pdf, which can be placed on a button
     */ 
    public static Sprite LoadPDF(FileInfo pdf, string convertedFilesPath)
    {
        string fileWithoutExtension = Path.GetFileNameWithoutExtension(pdf.FullName);
        string fileWithNewExtension = fileWithoutExtension + ".png";                 // Make a version of the filename with a new extension

        string pathToFolder = Path.Combine(convertedFilesPath, fileWithoutExtension + " pages");    // Path to folder holding all the different pages of a pdf

        if (!Directory.Exists(pathToFolder))
        {
            Directory.CreateDirectory(pathToFolder);
        }

        string newFilePath = Path.Combine(pathToFolder, fileWithNewExtension);  // Final path for the new files

        //Debug.Log("New file path: " + newFilePath);

        GhostscriptSettings settings = new GhostscriptSettings();
        settings.Page.AllPages = true;
        settings.Page.Start = 1;
        settings.Device = GhostscriptSharp.Settings.GhostscriptDevices.png16m;
        settings.Size.Native = GhostscriptSharp.Settings.GhostscriptPageSizes.letter;
        settings.Resolution = new DrawingSize(300, 300);

        if (!File.Exists(newFilePath))  // TODO: make sure this just checks first page
        {
            GhostscriptWrapper.GenerateOutput(pdf.FullName, newFilePath, settings);
            //GhostscriptWrapper.GeneratePageThumb(pdf.FullName, newFilePath, 0, 700, 700);
            //GhostscriptWrapper.GeneratePageThumb(pdf.FullName, newFilePath, 1, 300, 300);
        }


        return LoadImage(new FileInfo(newFilePath)); // Make sure this just loads first page
    }

    /*
     * Returns a sprite version of a jpg, which can be placed on a unity button
     */
    public static Sprite LoadImage(FileInfo jpg)
    {
        // Load image into Texture2D
        Texture2D texture = new Texture2D(1, 1);
        string filePath = Path.GetFullPath(jpg.FullName);
        texture.LoadImage(File.ReadAllBytes(filePath));

        // Make a sprite version
        Sprite tempSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);

        return tempSprite;
    }

    /**
     * Returns the number of pages in a pdf
     */ 
    public static int GetNumPages(string path)
    {
        if (path != null)
        {
            try
            {
                using (var stream = new StreamReader(File.OpenRead(path)))
                {
                    var regex = new Regex(@"/Type\s*/Page[^s]");
                    var matches = regex.Matches(stream.ReadToEnd());

                    return matches.Count;
                }
            }
            catch (Exception e)
            {
                print(e.Message);
                return 0;
            }
        }
        return 0;
    }
}
