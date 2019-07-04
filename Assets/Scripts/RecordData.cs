using UnityEngine;
using UnityEngine.SceneManagement;
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

    [XmlElement]
    public int participant;
    public Trial[] trials;


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


public class MotionData
{
    public TrialMotion[] trials;
    public int participant;

    public MotionData()
    {
        trials = new TrialMotion[0];
        participant = 0;
    }

    public MotionData(int numTrials, int participantID)
    {
        trials = new TrialMotion[numTrials];
        participant = participantID;
    }
}

public class TrialMotion
{
    public List<long> timeStamps;               // recorded time at every HandleEvents call. its System.DateTime.Now.Ticks, which measures the instant of time in segments of 100 nanoseconds.
    [XmlArray("controller1"), XmlArrayItem("location")]
    public List<ObjectPose> controller1Locations;  // left controller location at every HandleEvents call
    [XmlArray("controller2"), XmlArrayItem("location")]
    public List<ObjectPose> controller2Locations;  // right controller location at every HandleEvents call
    [XmlArray("hmd"), XmlArrayItem("location")]
    public List<ObjectPose> hmdLocations;

    public TrialMotion()
    {
        timeStamps = new List<long>();
        controller1Locations = new List<ObjectPose>();
        controller2Locations = new List<ObjectPose>();
        hmdLocations = new List<ObjectPose>();
    }
}
public class Trial
{
    [XmlElement("id")]
    public int trialOrder;
    public string selectionInterface;
    public string sceneName;
    public double tpArea;
    public double fpArea;
    public double fnArea;
    public double tnArea;
    public double f1;
    public double mcc;
    public float timeElapsed;                   // total duration spent on a trial
   
    public List<EventRecord> events;     // Dictionary where key corresponds to a time stamp every time a button or swipe event takes place 
                                                // and value is the name of the event
    public Trial(){
        trialOrder = 0;
        selectionInterface = "";                //should selectionInterface be saved here attached to every trial number or once in experimentData. would it ever be overwritten there?
        tpArea = 0;
        fpArea = 0;
        fnArea = 0;
        tnArea = 0;
        f1 = 0;
        mcc = 0;
        timeElapsed = 0f;
       
        events = new List<EventRecord>();
    }
}

public class ObjectPose
{
    public Vector3 position;
    public Quaternion rotation;

    public ObjectPose() {
        position = new Vector3();
        rotation = new Quaternion();
    }

    public void Add(Vector3 pos, Quaternion rot){
        position = pos;
        rotation = rot;
    }
}

public class EventRecord
{
    public long ticksTimestamp;
    public long elapsedNanoSeconds;
    public string eventName;

    public EventRecord()
    {
        elapsedNanoSeconds = (long)0;
        ticksTimestamp = (long)0;
        eventName = "";
    }

    public void Add(long ticks, long nanosec, string name){
        ticksTimestamp = ticks;
        elapsedNanoSeconds = nanosec;
        eventName = name;
    }
}

public class RecordData : MonoBehaviour {

    public static RecordData instance;

    ControllerInfo controller1;
    ControllerInfo controller2;

    ExperimentData trialData;
    MotionData motion;

    bool _ShouldSave, _ShouldLoad, _SwitchSave, _SwitchLoad;
    string _FileLocation, _FileName;
    string _data;
    string _dataM;
    private int trialID;
    int participantID;
    //int trialCount;
    InteractionState lastState;

    public RecordData(ControllerInfo leftHand, ControllerInfo rightHand, int numTrials) {
        trialID = 0;
        participantID = 5;
        controller1 = leftHand;
        controller2 = rightHand;
        //trialCount = numTrials;

        _FileLocation = "Assets/WrittenData";
        _FileName = participantID.ToString() + "_" + System.DateTime.Now.ToString("yyyyMMddHHmmssfff") + "-test";

        trialData = new ExperimentData(numTrials, participantID);
        motion = new MotionData(numTrials, participantID);
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

    public void SetTrialID(int id, int order, InteractionState state)
    {
        trialID = id;
        trialData.trials[trialID] = new Trial();
        trialData.trials[trialID].trialOrder = order;
        trialData.trials[trialID].selectionInterface = state.Desc;
        trialData.trials[trialID].sceneName = SceneManager.GetSceneByBuildIndex(id).name;

        motion.trials[trialID] = new TrialMotion();
        lastState = state;
    }

    public InteractionState GetSelectionState()
    {
        return lastState;
    }

    public void EndTrial(double tpArea, double fpArea, double fnArea, double tnArea, double f1, double mcc, float duration) {
        trialData.trials[trialID].tpArea = tpArea;
        trialData.trials[trialID].fpArea = fpArea;
        trialData.trials[trialID].fnArea = fnArea;
        trialData.trials[trialID].tnArea = tnArea;
        trialData.trials[trialID].f1 = f1;
        trialData.trials[trialID].mcc = mcc;
        trialData.trials[trialID].timeElapsed = duration;

        //trialID++;
    }

    public double GetPrecisionPercentage()
    {
        // precision = TP / (TP + FP);
        return trialData.trials[trialID].tpArea / (trialData.trials[trialID].tpArea + trialData.trials[trialID].fpArea) * 100.0;
    }

    // Returns the percentage accuractly selected
    public double GetRecallPercentage()
    {
        // i.e. recall TP / (TP + FN)
        return trialData.trials[trialID].tpArea / (trialData.trials[trialID].tpArea + trialData.trials[trialID].fnArea) * 100.0;
    }

    public void UpdateLists(ControllerInfo controller1, ControllerInfo controller2, Transform hmd, long ticks, long elapsedNanoSec, string eventStr = "") {
        motion.trials[trialID].timeStamps.Add(elapsedNanoSec);
        ObjectPose pose1 = new ObjectPose();
        pose1.Add(controller1.trackedObj.transform.position, controller1.trackedObj.transform.rotation);
        motion.trials[trialID].controller1Locations.Add(pose1);

        ObjectPose pose2 = new ObjectPose();
        pose2.Add(controller2.trackedObj.transform.position, controller2.trackedObj.transform.rotation);
        motion.trials[trialID].controller2Locations.Add(pose2);

        ObjectPose headPose = new ObjectPose();
        headPose.Add(hmd.position, hmd.rotation);
        motion.trials[trialID].hmdLocations.Add(headPose);

        if (!eventStr.Equals("")) {
            EventRecord eventRecord = new EventRecord();
            eventRecord.Add(ticks, elapsedNanoSec, eventStr);
            trialData.trials[trialID].events.Add(eventRecord);
        }
    }

    public void WriteToFile()
    {
        // Time to creat our XML! 
        _data = SerializeObject(trialData);
        _dataM = SerializeObject(motion);
        // This is the final resulting XML from the serialization process 
        CreateXML(_data, _FileName);
        CreateXML(_dataM, _FileName + "Motion");

        Debug.Log("Files Written");
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
        XmlSerializer xs = new XmlSerializer(pObject.GetType());
        XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8);
        xs.Serialize(xmlTextWriter, pObject);
        memoryStream = (MemoryStream)xmlTextWriter.BaseStream;
        XmlizedString = UTF8ByteArrayToString(memoryStream.ToArray());
        return XmlizedString;
    }

    //// Here we deserialize it back into its original form 
    //object DeserializeObject(string pXmlizedString)
    //{
    //    XmlSerializer xs = new XmlSerializer(typeof(ExperimentData));
    //    MemoryStream memoryStream = new MemoryStream(StringToUTF8ByteArray(pXmlizedString));
    //    XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8);
    //    return xs.Deserialize(memoryStream);
    //}

    // Finally our save and load methods for the file itself 
    void CreateXML(string dataset, string fileName)
    {
        StreamWriter writer;
        FileInfo t = new FileInfo(_FileLocation + "\\" + fileName+ ".xml");
        if (!t.Exists)
        {
            writer = t.CreateText();
        }
        else
        {
            t.Delete();
            writer = t.CreateText();
        }
        writer.Write(dataset);
        writer.Close();
        //Debug.Log("File written. " + fileName);
    }

    //void LoadXML()
    //{
    //    StreamReader r = File.OpenText(_FileLocation + "\\" + _FileName);
    //    string _info = r.ReadToEnd();
    //    r.Close();
    //    _data = _info;
    //    Debug.Log("File Read");
    //}
}