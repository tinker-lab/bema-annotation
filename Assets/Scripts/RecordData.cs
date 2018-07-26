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

    //possible additions to this class:
    //  public string selectionInterface;
    // Is there a way to put all interfaces for one participant also in one file w/out more nested dictionaries / classes? 
    // Would that even be useful, or is it better to keep them separate?

    public Trial[] trials;
    int participant;


    public ExperimentData(){
    // is this an absurd way to save this? I think all the trials should be in one file,
                                                            // does that mean the all need to be saved in one object?
        trials = new Trial[0] ;
        participant = 0;
    }

    public ExperimentData(int numTrials, int participantID)
    {
        trials = new Trial[numTrials];
        participant = participantID;
    }
}

public class Trial
{
    public int trialID;
    public string selectionInterface;
    public float selectedArea;                  // total selected area -> updated to hold final amount
    public float timeElapsed;                   // total duration spent on a trial
    public List<long> timeStamps;               // recorded time at every HandleEvents call. its System.DateTime.Now.Ticks, which measures the instant of time in segments of 100 nanoseconds.
    public List<Vector3> controller1Locations;  // left controller location at every HandleEvents call
    public List<Vector3> controller2Locations;  // right controller location at every HandleEvents call
    public List<EventRecord> events;     // Dictionary where key corresponds to a time stamp every time a button or swipe event takes place 
                                                // and value is the name of the event
    public Trial(){
        trialID = 0;
        selectionInterface = "";                //should selectionInterface be saved here attached to every trial number or once in experimentData. would it ever be overwritten there?
        selectedArea = 0f;
        timeElapsed = 0f;
        timeStamps = new List<long>();
        controller1Locations = new List<Vector3>();
        controller2Locations = new List<Vector3>();
        events = new List<EventRecord>();
    }
}

public class EventRecord
{
    long timeStamp;
    string eventName;

    public EventRecord()
    {
        timeStamp = (long)0;
        eventName = "";
    }

    public EventRecord(long time, string name)
    {
        timeStamp = time;
        eventName = name;
    }
}

public class RecordData : MonoBehaviour {

    public static RecordData instance;

    ControllerInfo controller1;
    ControllerInfo controller2;

    ExperimentData myData;

    bool _ShouldSave, _ShouldLoad, _SwitchSave, _SwitchLoad;
    string _FileLocation, _FileName;
    string _data;
    public int trialID;
    int participantID;
    int trialCount;
    InteractionState lastState;

    public RecordData(ControllerInfo leftHand, ControllerInfo rightHand, int numTrials) {
        trialID = 0;
        participantID = 0;
        controller1 = leftHand;
        controller2 = rightHand;
        trialCount = numTrials;

        _FileLocation = "Assets/WrittenData";
        _FileName = participantID.ToString() + "test.xml";

        myData = new ExperimentData(numTrials, participantID);
    }

    void Awake()
    {
        // If the instance reference has not been set, yet, 
        if (instance == null)
        {
            // Set this instance as the instance reference.
            instance = this;
        }
        else if (instance != this)
        {
            // If the instance reference has already been set, and this is not the
            // the instance reference, destroy this game object.
            UnityEngine.Object.Destroy(gameObject);
        }

        // Do not destroy this object, when we load a new scene.
        //UnityEngine.Object.DontDestroyOnLoad(gameObject);
    }

    public void SetTrialID(int id, InteractionState state)
    {
        trialID = id;
        myData.trials[trialID] = new Trial();
        myData.trials[trialID].trialID = trialID;
        myData.trials[trialID].selectionInterface = state.Desc;
        lastState = state;
    }

    public InteractionState GetSelectionState()
    {
        return lastState;
    }

    public void EndTrial(float area, float duration) {
        myData.trials[trialID].selectedArea = area;
        myData.trials[trialID].timeElapsed = duration;

        trialID++;
    }

    public void UpdateLists(ControllerInfo controller1, ControllerInfo controller2, long timeStamp, string eventStr = "") {
        myData.trials[trialID].timeStamps.Add(timeStamp);
        myData.trials[trialID].controller1Locations.Add(controller1.trackedObj.transform.position);
        myData.trials[trialID].controller2Locations.Add(controller2.trackedObj.transform.position);

        if (!eventStr.Equals("")) {
            myData.trials[trialID].events.Add(new EventRecord(timeStamp, eventStr));
        }
    }

    public void WriteToFile()
    {
        // Time to creat our XML! 
        _data = SerializeObject(myData);
        // This is the final resulting XML from the serialization process 
        CreateXML();
        //should trialID increment like this or be attached to specific scenes? probably scenes.
        Debug.Log(_data);
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