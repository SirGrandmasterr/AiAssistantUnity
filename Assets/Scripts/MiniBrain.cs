using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NativeWebSocket;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Whisper;
using Whisper.Utils;
using Debug = UnityEngine.Debug;
using Random = System.Random;

public class MiniBrain : MonoBehaviour
{
    //public WebSocketClient webSocketTtsClient;
    public WebRtcProvider webRtcTts;
    private WebSocket _websocket;
    public Dictionary<string, int> actionDict;
    private Random rnd;
    private Queue<GameObject> repairQueue;
    private bool isSpeaking;
    public Button sendButton;
    public TextMeshProUGUI gradeText;
    public TextMeshProUGUI justificationText;
    public EmotionStatisticsManager emotionStatisticsManager;
    
    public WhisperManager whisper;
    public MicrophoneRecord microphoneRecord;
    private string _buffer;
    public bool streamSegments = true;
    public bool printLanguage = true;
    private Stopwatch recordingStopwatch;
    public Text outputText;
    public Text timeText;
    public TMP_InputField evalInput;
    

    public EmotionMeter emotionMeter;
    private Stopwatch sw;
    private int tk;
    


    [Serializable]
    public struct AssistantContext
    {
        [FormerlySerializedAs("Location")] public string location;

        [FormerlySerializedAs("PlayerVisible")]
        public bool playerVisible;

        [FormerlySerializedAs("PlayerAudible")]
        public bool playerAudible;

        [FormerlySerializedAs("AssetsInView")] public string[] assetsInView;

        [FormerlySerializedAs("AvailableActions")]
        public string[] availableActions;

        [FormerlySerializedAs("WalkingState")] public string walkingState;
        [FormerlySerializedAs("FocusedAsset")] public string focusedAsset;

        [FormerlySerializedAs("SelectedBasePrompt")]
        public string selectedBasePrompt;

        public EmotionalState emotionalState;
        public Dictionary<string, float> facePercentages;
    }
    
    public class EvaluationResult
    {
        // These properties will be populated by the JSON deserialization.
        // The names 'grade' and 'justification' must match the JSON keys.
        public string grade;
        public string justification;

        /// <summary>
        /// A helper method to print the deserialized content to the Unity console.
        /// </summary>
        public void PrintDetails()
        {
            Debug.Log($"Evaluation Grade: {grade}");
            Debug.Log($"Justification: {justification}");
        }
    }

    [Serializable]
    public struct PlayerContext
    {
        [FormerlySerializedAs("Location")] public string location;
        [FormerlySerializedAs("AssetsInView")] public string[] assetsInView;

        [FormerlySerializedAs("InConversation")]
        public bool inConversation;

        [FormerlySerializedAs("PlayerUsername")]
        public string playerUsername;
    }

    [Serializable]
    public struct ActionContext
    {
        public string actionName;
        public string token;
        public int stage;
        public bool permission;
    }

    [Serializable]
    public struct EventContext
    {
        public string[] relevantObjects;
        public string eventLocation;
    }

    [Serializable]
    public struct LlamaWebsockRequest
    {
        [FormerlySerializedAs("MessageType")] public string messageType;

        [FormerlySerializedAs("PlayerActionType")]
        public string playerActionType;

        [FormerlySerializedAs("Speech")] public string speech;

        [FormerlySerializedAs("AssistantContext")]
        public AssistantContext assistantContext;

        [FormerlySerializedAs("PlayerContext")]
        public PlayerContext playerContext;

        public ActionContext actionContext;
        public EventContext eventContext;
    }


    //State of Assistant
    public bool PlayerAudible;
    public bool PlayerVisible;
    public string[] AssetsInView;
    public string[] PlayerAssetsInView;
    public bool PlayerInConversation;
    public GazeObject PlayerGaze;
    public float bored;
    public Material headMaterial;

    private bool talkAndFollow;


    //


    // Start is called before the first frame update
    private async void Start()
    {
        Cursor.visible = true;
        sendButton.onClick.AddListener(SendEvalEvent);
        recordingStopwatch = new Stopwatch();
        tk = 0;
        sw = new Stopwatch();
        isSpeaking = false;
        whisper.OnNewSegment += OnNewSegment;
        whisper.OnProgress += OnProgressHandler;
        recordingStopwatch = new Stopwatch();
        emotionStatisticsManager = EmotionStatisticsManager.Instance;

        microphoneRecord.OnRecordStop += OnRecordStop;

        rnd = new Random();
        bored = 30f;
        actionDict = new Dictionary<string, int>();
        string url = PlayerPrefs.GetString("Address");
        url = "ws://" + url + "/ws/" + PlayerPrefs.GetString("playerJwt");
        Debug.Log(url);
        _websocket = new WebSocket(url);
        webRtcTts = WebRtcProvider.Instance;

        _websocket.OnOpen += () =>
        {
            Debug.Log("Connection to Llamacommunicator open!");
            LlamaWebsockRequest msg = new LlamaWebsockRequest();
            msg.messageType = "initializePlayer";
            msg.playerContext = new PlayerContext();
            msg.playerContext.playerUsername = PlayerPrefs.GetString("username");
            _websocket.SendText(JsonUtility.ToJson(msg));
        };

        _websocket.OnError += e => { Debug.Log("Error! " + e); };

        _websocket.OnClose += _ => { Debug.Log("LlamaConnection Closed!"); };

        _websocket.OnMessage += bytes =>
        {
            //Decode bytes into String, assume it's JSON, Decode into Websocketmsg
            var msg = LlamaWebsockMsg.CreateFromJson(Encoding.UTF8.GetString(bytes));
            switch (msg.type)
            {
                case "speech":
                {
                    if (!isSpeaking)
                    {
                        //print(sw.ElapsedMilliseconds + "history tokens: " + tk);
                        SendHistoryUpdate("ASSISTANT: '" + msg.text);
                        isSpeaking = true;
                    }
                    else if (msg.actionName == "stopSpeak")
                    {
                        SendHistoryUpdate(msg.text + "'");
                        isSpeaking = false; //remove
                    }
                    else
                    {
                        SendHistoryUpdate(msg.text);
                    }

                    //Task task = webSocketTtsClient.ListenTo(msg.text);
                    Task task = webRtcTts.SendTextMessageForTTS(msg.text);
                    return;
                }
                case "actionSelection":
                {
                    var secondary = msg.text;
                    var original = msg.actionName;
                    msg.actionName = secondary;
                    EvaluateAction(msg);
                    msg.actionName = original;
                    EvaluateAction(msg);
                    return;
                }
                default:
                    EvaluateAction(msg);
                    break;
            }
        };

        // waiting for messages
        await _websocket.Connect();
    }

    private void Update()
    {
        if (Input.GetKeyDown("e"))
        {
            print("Pressed e");
            OnButtonPressed();
        }
            
           

        if (Input.GetKeyUp("e"))
        {
            print("Released e");
            OnButtonPressed();
        }
        
#if !UNITY_WEBGL || UNITY_EDITOR
        _websocket.DispatchMessageQueue();
#endif
    }

    public void SendTestEvent()
    {
        SendHistoryUpdate(
            "I will now explain the concept of programming. Programming is the process of designing, writing, testing, and maintaining the instructions that a computer follows to perform a specific task. These instructions are called programs. A program consists of a series of statements or commands that tell the computer what actions to take in order to achieve a particular goal. The programmer writes these statements using a programming language, which is a set of rules and syntax for communicating with computers. There are many different types of programming languages, I will now explain the concept of programming. Programming is the process of designing, writing, testing, and maintaining the instructions that a computer follows to perform a specific task. These instructions are called programs. A program consists of a series of statements or commands that tell the computer what actions to take in order to achieve a particular goal. The programmer writes these statements using a programming language, which is a set of rules and syntax for communicating with computers. There are many different types of programming languages... is it.");
        sw.Restart();
        tk += 200;
        var ac = InquireAssistantContext(true, false);
        var pc = InquirePlayerContext();
        LlamaWebsockRequest msg = new LlamaWebsockRequest();
        msg.messageType = "speech";
        msg.playerActionType = "speech";
        msg.speech = "text";
        msg.assistantContext = ac;
        msg.playerContext = pc;
        msg.eventContext = new EventContext();

        if (PlayerInConversation && PlayerGaze.Valid)
        {
            msg.assistantContext.focusedAsset = PlayerGaze.ObjectOfInterest.name;
        }

        msg.assistantContext.availableActions = new string[] { "testAction" };
        _websocket.SendText(Newtonsoft.Json.JsonConvert.SerializeObject(msg));
    }

    public void SendPlayerSpeech(string text)
    {
        var ac = InquireAssistantContext(true, false);
        var pc = InquirePlayerContext();
        print(ac.ToString());
        print(pc.ToString());
        LlamaWebsockRequest msg = new LlamaWebsockRequest();
        msg.messageType = "speech";
        msg.playerActionType = "speech";
        msg.speech = text;
        msg.assistantContext = ac;
        msg.playerContext = pc;
        msg.eventContext = new EventContext();
        emotionStatisticsManager.DisplayFinalEmotionSummary();
        _websocket.SendText(Newtonsoft.Json.JsonConvert.SerializeObject(msg));
    }

    public void SendHistoryUpdate(string text)
    {
        var ac = InquireAssistantContext(false, false);
        var pc = InquirePlayerContext();
        var acc = new ActionContext();
        acc.actionName = "";
        acc.permission = false;
        acc.token = "";
        acc.stage = 0;
        LlamaWebsockRequest msg = new LlamaWebsockRequest();
        msg.messageType = "playerHistoryUpdate"; // "speech" "assistantUpdate" "playerAction" "suggestAction"
        msg.playerActionType = ""; // "speech", "enteringVision", "leavingVision", "vandalism"
        msg.speech = text;
        msg.assistantContext = ac;
        msg.playerContext = pc;
        msg.eventContext = new EventContext();
        print(Newtonsoft.Json.JsonConvert.SerializeObject(msg));
        _websocket.SendText(Newtonsoft.Json.JsonConvert.SerializeObject(msg));
    }

    public void SendActionUpdate(string token, string actionName, int stage, bool permission, string speech,
        string focus, string[] options)
    {
        print("Sending ActionUpdate");
        var ac = InquireAssistantContext(false, false);
        var pc = InquirePlayerContext();
        var acc = new ActionContext();
        acc.actionName = actionName;
        acc.permission = permission;
        acc.token = token;
        acc.stage = stage;
        LlamaWebsockRequest msg = new LlamaWebsockRequest();
        msg.messageType = "actionUpdate"; // "speech" "assistantUpdate" "playerAction" "suggestAction"
        msg.playerActionType = ""; // "speech", "enteringVision", "leavingVision", "vandalism"
        msg.speech = speech;
        msg.assistantContext = ac;
        msg.playerContext = pc;
        msg.eventContext = new EventContext();
        msg.actionContext = acc;

        if (msg.actionContext.actionName == "repair")
        {
            msg.assistantContext.focusedAsset = focus;
            msg.assistantContext.availableActions = options;
        }

        print(Newtonsoft.Json.JsonConvert.SerializeObject(msg));
        _websocket.SendText(Newtonsoft.Json.JsonConvert.SerializeObject(msg));
    }

    public void SendEnvEvent(string description, EventContext eventContext, string[] actionOptions)
    {
        SendHistoryUpdate(description);
        var ac = InquireAssistantContext(false, false);
        var pc = InquirePlayerContext();
        var acc = new ActionContext();
        acc.actionName = "";
        acc.permission = false;
        acc.token = "";
        acc.stage = 0;
        LlamaWebsockRequest msg = new LlamaWebsockRequest();
        msg.messageType = "envEvent"; // "speech" "assistantUpdate" "playerAction" "suggestAction"
        msg.playerActionType = ""; // "speech", "enteringVision", "leavingVision", "vandalism"
        msg.speech = description;
        msg.assistantContext = ac;
        msg.assistantContext.availableActions = actionOptions;
        msg.playerContext = pc;
        msg.eventContext = eventContext;
        _websocket.SendText(Newtonsoft.Json.JsonConvert.SerializeObject(msg));
    }


    private async void EvaluateAction(LlamaWebsockMsg msg)
    {
        Debug.Log("Received Action Instruction");
        Debug.Log(msg.actionName);
        switch (msg.actionName)
        {
            case "repair":
                switch (msg.stage)
                {
                    case 1: // We know repair was selected.
                        print("Case 1");
                        actionDict.Add(msg.token, 1);
                        if (repairQueue.Count > 0)
                        {
                            var obj = repairQueue.Peek();
                            SendActionUpdate(msg.token, msg.actionName, msg.stage + 1, true, "", obj.name,
                                new string[] { });
                        }

                        break;
                    case 2:
                        print("Case 2"); //
                        var targetobj = repairQueue.Dequeue();

                        SendActionUpdate(msg.token, msg.actionName, msg.stage + 1, true, "", "",
                            new string[] { "walkToVisitor" });
                        break;
                    case 3:
                        print("Case 3");

                        break;
                    case 4:
                        print("Case 4");
                        SendActionUpdate(msg.token, msg.actionName, msg.stage + 1, true, "", "",
                            new string[] { "standIdle" });
                        break;
                    case 5:
                        actionDict.Remove(msg.token);
                        break;
                }

                break;
            case "evaluateShownEmotions":
                switch (msg.stage)
                {
                    case 1:
                        print("Case 1");
                        actionDict.Add(msg.token, 1);

                        break;
                    case 2:
                        print("Case 2"); //
                        EvaluationResult result = DeserializeJsonString(msg.text);
                        gradeText.text = result.grade;
                        justificationText.text = result.justification;
                            
                        
                        break;
                }

                break;
        }
    }


    private AssistantContext InquireAssistantContext(bool speech, bool innerThought)
        {
            string walkingstate;
            string[] opts;
            opts = IdeateActionOptions(speech, innerThought);

            var emotionalState = emotionMeter.GetEmotionalState();
            var context = new AssistantContext
            {
                location = "lower gallery",
                playerVisible = PlayerVisible,
                playerAudible = PlayerAudible,
                assetsInView = AssetsInView,
                availableActions = opts,
                walkingState = "idle",
                focusedAsset = "",
                selectedBasePrompt = GetBasePrompt(),
                emotionalState = emotionalState,
                facePercentages = emotionStatisticsManager.GetFinalEmotionSummary().emotionPercentages
            };

            return context;
        }

        private string GetBasePrompt()
        {
            return PlayerPrefs.HasKey("selectedBasePrompt")
                ? PlayerPrefs.GetString("selectedBasePrompt")
                : "galleryGuideInterpreter";
        }


        //Function to find out what actions the LLM can take at given time.
        private String[] IdeateActionOptions(bool speech, bool innerThought)
        {
            var list = new List<string>();

            list.Add("evaluateShownEmotions");

            return list.ToArray();
        }

        private PlayerContext InquirePlayerContext()
        {
            var ctx = new PlayerContext();
            if (!PlayerVisible && !PlayerAudible)
            {
                ctx.location = "unknown";
                ctx.assetsInView = PlayerAssetsInView;
                ctx.inConversation = PlayerInConversation;
                ctx.playerUsername = PlayerPrefs.GetString("username");
                return ctx;
            }

            ctx.location = "unknown";
            ctx.assetsInView = PlayerAssetsInView;
            ctx.inConversation = PlayerInConversation;
            ctx.playerUsername = PlayerPrefs.GetString("username");
            return ctx;
        }
        
        private void OnButtonPressed()
        {
            if (!microphoneRecord.IsRecording)
            {
                microphoneRecord.StartRecord();
                recordingStopwatch.Start();
            }
            else
            {
                microphoneRecord.StopRecord();
                Debug.Log(recordingStopwatch.ElapsedMilliseconds);
                if (recordingStopwatch.ElapsedMilliseconds < 500)
                {
                    recordingStopwatch.Stop();
                    recordingStopwatch.Reset();
                }
                else
                {
                    Debug.Log("Recording Action");
                    recordingStopwatch.Stop();
                    recordingStopwatch.Reset();
                }
            }
        }
        
        private async void OnRecordStop(AudioChunk recordedAudio)
        {
            _buffer = "";

            var sw = new Stopwatch();
            sw.Start();

            var res = await whisper.GetTextAsync(recordedAudio.Data, recordedAudio.Frequency, recordedAudio.Channels);
            //brain.SendHistoryUpdate("VISITOR: " + res.Result + "\n", true, "speech");
            SendPlayerSpeech(res.Result);
            evalInput.text = res.Result;
            
            //if (res == null || !outputText) 
            //    return;

            var time = sw.ElapsedMilliseconds;
            var rate = recordedAudio.Length / (time * 0.001f);
            //timeText.text = $"Time: {time} ms\nRate: {rate:F1}x";

            var text = res.Result;
            if (printLanguage)
                text += $"\n\nLanguage: {res.Language}";

            //outputText.text = text;
            sendButton.enabled = true;
        }
        
        private void OnProgressHandler(int progress)
        {
            if (!timeText)
                return;
            timeText.text = $"Progress: {progress}%";
        }

        private void OnNewSegment(WhisperSegment segment)
        {
            if (!streamSegments || !outputText)
                return;

            _buffer += segment.Text;
            outputText.text = _buffer + "...";
        }

        private void SendEvalEvent()
        {
            SendPlayerSpeech(evalInput.text);
            sendButton.enabled = false;
        }
        
        public EvaluationResult DeserializeJsonString(string jsonInput)
        {
            EvaluationResult result = null;
            try
            {
                // Use JsonConvert.DeserializeObject<T> to convert the JSON string into an object of type EvaluationResult.
                result = JsonConvert.DeserializeObject<EvaluationResult>(jsonInput);

                if (result != null)
                {
                    Debug.Log("JSON deserialization successful!");
                    result.PrintDetails(); // Call the helper method to display the data
                }
                else
                {
                    Debug.LogError("Deserialized object is null. Check JSON string format and class structure.");
                }
            }
            catch (JsonSerializationException e)
            {
                // Catch specific JSON serialization errors (e.g., malformed JSON).
                Debug.LogError($"JSON Serialization Error: {e.Message}");
                Debug.LogError($"JSON Input: {jsonInput}");
            }
            catch (System.Exception e)
            {
                // Catch any other general exceptions.
                Debug.LogError($"An unexpected error occurred during deserialization: {e.Message}");
                Debug.LogError($"JSON Input: {jsonInput}");
            }
            return result;
        }


    }