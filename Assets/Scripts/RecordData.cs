using UnityEngine; 
using System.Collections.Generic; 
using System.Xml; 
using System.Xml.Serialization; 
using System.IO; 
using System.Text;

// This example is from http://wiki.unity3d.com/index.php?title=Save_and_Load_from_XML
// The wiki references a further example where the encoding can be found at 
// http://www.eggheadcafe.com/articles/system.xml.xmlserialization.asp 
// THey use the KISS method and cheat a little and use 
// the examples from the web page since they are fully described.
// The class is altered to record our ExperimentData.

public class ExperimentData{

    public Dictionary<int, TrialData> trialData;
    //possible additions to this class:
    //  public string selectionInterface;
    //  public int participantID;
    // Is there a way to put all interfaces for one participant also in one file w/out more nested dictionaries / classes? 
    // Would that even be useful, or is it better to keep them separate?

    public ExperimentData(){
        trialData = new Dictionary<int, TrialData>();       // is this an absurd way to save this? I think all the trials should be in one file,
                                                                // does that mean the all need to be saved in one object?
    }
}

public class TrialData
{
    public string selectionInterface;
    public float selectedArea;                  // total selected area -> updated to hold final amount
    public float timeElapsed;                   // total duration spent on a trial
    public List<Time> timeStamps;               // recorded time at every HandleEvents call
    public List<Vector3> controller1Locations;  // left controller location at every HandleEvents call
    public List<Vector3> controller2Locations;  // right controller location at every HandleEvents call
    public Dictionary<Time, string> events;     // Dictionary where key corresponds to a time stamp every time a button or swipe event takes place 
                                                // and value is the name of the event
    public TrialData(){
        selectionInterface = "";                //should selectionInterface be saved here attached to every trial number or once in experimentData. would it ever be overwritten there?
        selectedArea = 0f;
        timeElapsed = 0f;
        timeStamps = new List<Time>();
        controller1Locations = new List<Vector3>();
        controller2Locations = new List<Vector3>();
        events = new Dictionary<Time, string>();
    }
}

public class RecordData {

    ControllerInfo controller1;
    ControllerInfo controller2;
    InteractionState currentState;

    ExperimentData myData;

    bool _ShouldSave, _ShouldLoad, _SwitchSave, _SwitchLoad;
    string _FileLocation, _FileName;
    string _data;
    int trialID;

    public RecordData(ControllerInfo leftHand, ControllerInfo rightHand, InteractionState state){
        trialID = 0;
        controller1 = leftHand;
        controller2 = rightHand;
        currentState = state;

        _FileLocation = "";
        _FileName = "";

        myData = new ExperimentData();
    }

    public void writeToFile( float area, float duration){
        myData.trialData[trialID].selectedArea = area;
        myData.trialData[trialID].timeElapsed = duration;
        myData.trialData[trialID].selectionInterface = currentState.Desc;

        // Time to creat our XML! 
        _data = SerializeObject(myData);
        // This is the final resulting XML from the serialization process 
        CreateXML();
        trialID++;                                              //should trialID increment like this or be attached to specific scenes? probably scenes.
        Debug.Log(_data);
    }

    public void updateLists(Time timeStamp, string eventStr = ""){
        myData.trialData[trialID].timeStamps.Add(timeStamp);
        myData.trialData[trialID].controller1Locations.Add(controller1.trackedObj.transform.position);
        myData.trialData[trialID].controller2Locations.Add(controller2.trackedObj.transform.position);

        if (!eventStr.Equals("")){
            myData.trialData[trialID].events.Add(timeStamp, eventStr);
        }
    }

    /* The following metods came from the referenced URL */
    string UTF8ByteArrayToString(byte[] characters)
    {
        UTF8Encoding encoding = new UTF8Encoding();
        string constructedString = encoding.GetString(characters);
        return (constructedString);
    }

    byte[] StringToUTF8ByteArray(string pXmlString)
    {
        UTF8Encoding encoding = new UTF8Encoding();
        byte[] byteArray = encoding.GetBytes(pXmlString);
        return byteArray;
    }

    // Here we serialize our UserData object of myData 
    string SerializeObject(object pObject)
    {
        string XmlizedString = null;
        MemoryStream memoryStream = new MemoryStream();
        XmlSerializer xs = new XmlSerializer(typeof(ExperimentData));
        XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8);
        xs.Serialize(xmlTextWriter, pObject);
        memoryStream = (MemoryStream)xmlTextWriter.BaseStream;
        XmlizedString = UTF8ByteArrayToString(memoryStream.ToArray());
        return XmlizedString;
    }

    // Here we deserialize it back into its original form 
    object DeserializeObject(string pXmlizedString)
    {
        XmlSerializer xs = new XmlSerializer(typeof(ExperimentData));
        MemoryStream memoryStream = new MemoryStream(StringToUTF8ByteArray(pXmlizedString));
        XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8);
        return xs.Deserialize(memoryStream);
    }

    // Finally our save and load methods for the file itself 
    void CreateXML()
    {
        StreamWriter writer;
        FileInfo t = new FileInfo(_FileLocation + "\\" + _FileName);
        if (!t.Exists)
        {
            writer = t.CreateText();
        }
        else
        {
            t.Delete();
            writer = t.CreateText();
        }
        writer.Write(_data);
        writer.Close();
        Debug.Log("File written.");
    }

    void LoadXML()
    {
        StreamReader r = File.OpenText(_FileLocation + "\\" + _FileName);
        string _info = r.ReadToEnd();
        r.Close();
        _data = _info;
        Debug.Log("File Read");
    }
}