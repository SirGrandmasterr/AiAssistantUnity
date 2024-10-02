using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NativeWebSocket;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.Serialization;

public class Brain : MonoBehaviour
{
    public WebSocketClient webSocketTtsClient;
    public AssistantMovementController movementController;
    public MusicAction musicManager;
    private WebSocket _websocket;
    public Dictionary<string, int> actionDict;

    [Serializable]
    public struct AssistantContext
    {
        [FormerlySerializedAs("Location")] public string location;
        [FormerlySerializedAs("PlayerVisible")] public bool playerVisible;
        [FormerlySerializedAs("PlayerAudible")] public bool playerAudible;
        [FormerlySerializedAs("AssetsInView")] public string[] assetsInView; 
        [FormerlySerializedAs("AvailableActions")] public string[] availableActions;

        [FormerlySerializedAs("WalkingState")] public string walkingState;
        [FormerlySerializedAs("FocusedAsset")] public string focusedAsset;
        [FormerlySerializedAs("SelectedBasePrompt")] public string selectedBasePrompt;
    }
    
    [Serializable]
    public struct PlayerContext
    {
        [FormerlySerializedAs("Location")] public string location;
        [FormerlySerializedAs("AssetsInView")] public string[] assetsInView;
        [FormerlySerializedAs("InConversation")] public bool inConversation;
        [FormerlySerializedAs("PlayerUsername")] public string playerUsername;
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
    public struct LlamaWebsockRequest
    {
        [FormerlySerializedAs("MessageType")] public string messageType;
        [FormerlySerializedAs("PlayerActionType")] public string playerActionType;
        [FormerlySerializedAs("Speech")] public string speech;
        [FormerlySerializedAs("AssistantContext")] public AssistantContext assistantContext;
        [FormerlySerializedAs("PlayerContext")] public PlayerContext playerContext;
        public ActionContext actionContext;
    }

    
    //State of Assistant
    public bool PlayerAudible;
    public bool PlayerVisible;
    public string[] AssetsInView;
    public bool PlayerInConversation;
    public GazeObject PlayerGaze;
    
    public Material headMaterial;

    private bool talkAndFollow;
    public bool isSpeaking = false;
    

    //
  

    // Start is called before the first frame update
    private async void Start()
    {
        
        actionDict = new Dictionary<string, int>();
        _websocket = new WebSocket("ws://localhost:3000/ws/id");

        _websocket.OnOpen += () => { Debug.Log("Connection to Llamacommunicator open!"); };

        _websocket.OnError += e => { Debug.Log("Error! " + e); };

        _websocket.OnClose += _ => { Debug.Log("LlamaConnection Closed!"); };

        _websocket.OnMessage += bytes =>
        {
            Debug.Log("Got a message from LlamaCommunicator");
            //Decode bytes into String, assume it's JSON, Decode into Websocketmsg
            var msg = LlamaWebsockMsg.CreateFromJson(Encoding.UTF8.GetString(bytes));

            if (msg.type == "speech")
            {
                isSpeaking = true;
                Task task = webSocketTtsClient.ListenTo(msg.text);
                return;
            }

            EvaluateAction(msg);
                
                    
            
        };

        // waiting for messages
        await _websocket.Connect();
    }

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        _websocket.DispatchMessageQueue();
#endif
    }

    public void SendPlayerSpeech(string text)
    {
        var ac = InquireAssistantContext(true);
        var pc = InquirePlayerContext();
        print(ac.ToString());
        print(pc.ToString());
        LlamaWebsockRequest msg = new LlamaWebsockRequest();
        msg.messageType = "speech"; // "speech" "assistantUpdate" "playerAction" "suggestAction"
        msg.playerActionType = "speech"; // "speech", "enteringVision", "leavingVision", "vandalism"
        msg.speech = text;
        msg.assistantContext = ac;
        msg.playerContext = pc;
        
        print(JsonUtility.ToJson(msg));
        _websocket.SendText(JsonUtility.ToJson(msg));
    }

    public void SendHistoryUpdate(string text)
    {
        var ac = InquireAssistantContext(false);
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
        
        print(JsonUtility.ToJson(msg));
        _websocket.SendText(JsonUtility.ToJson(msg));
    }

    public void SendActionUpdate(string token, string actionName, int stage, bool conditional, string speech)
    {
        var ac = InquireAssistantContext(false);
        var pc = InquirePlayerContext();
        var acc = new ActionContext();
        acc.actionName = actionName;
        acc.permission = conditional;
        acc.token = token;
        acc.stage = stage;
        LlamaWebsockRequest msg = new LlamaWebsockRequest();
        msg.messageType = "actionUpdate"; // "speech" "assistantUpdate" "playerAction" "suggestAction"
        msg.playerActionType = ""; // "speech", "enteringVision", "leavingVision", "vandalism"
        msg.speech = speech;
        msg.assistantContext = ac;
        msg.playerContext = pc;
        
        print(JsonUtility.ToJson(msg));
        _websocket.SendText(JsonUtility.ToJson(msg));
    }
    
    public void SendAlarmEvent()


    private async void EvaluateAction(LlamaWebsockMsg msg)
    {   Debug.Log("Received Action Instruction");
        switch (msg.actionName)
        {
            case "followVisitor":
                movementController.movementState = 2;
                break;
            case "stopFollowingVisitor":
                movementController.movementState = 0;
                break;
            case "standIdle":
                movementController.movementState = 0;
                break;
            case "patrol":
                movementController.movementState = 1;
                break;
            case "playMusic":
                switch (msg.stage)
                {
                    case 1:
                        actionDict.Add(msg.token, 1);
                        break;
                    case 2:
                        await musicManager.LoadAudioqueue(msg.text, msg.token);
                        actionDict[msg.token] = 2;
                        break;
                }
                break;
            case "explainOptions":
                break;
        }
    }


    private AssistantContext InquireAssistantContext(bool speech)
    {
        string walkingstate;
        string[] opts = IdeateActionOptions(speech);
        switch (movementController.movementState)
        {
            case 0:
                walkingstate = "idle";
                break;
            case 1:
                walkingstate = "patrolling";
                break;
            case 2:
                walkingstate= "followPlayer";
                break;
            default:
                walkingstate = "unknown";
                break;
        }

        var context = new AssistantContext
        {
            location = movementController.location, playerVisible = PlayerVisible, playerAudible = PlayerAudible,
            assetsInView = AssetsInView, availableActions = opts,walkingState = walkingstate, focusedAsset = "",
            selectedBasePrompt = "museumAssistant"
        };
        
        
        return context;
    }

    //Function to find out what actions the LLM can take at given time.
    private String[] IdeateActionOptions(bool speech)
    {
        var list =new List<string>();
        if (movementController.movementState == 2)
        {
            list.Add("stopFollowingVisitor");
        }
        else
        {
            list.Add("followVisitor");
        }

        if (speech)
        {
            list.Add("explainWhatYouCanDo");
            list.Add("continueConversation");
        }

        if (!speech)
        {
            list.Add("standIdle");
            list.Add("patrol");
        }

        //list.Add("walkToVisitor");

        if (musicManager.isPlaying)
        {
            list.Add("stopMusic");
        }
        else
        {
            list.Add("playMusic");
        }

        //list.Add("warnVisitor");
        //list.Add("followAndTalk");
        //list.Add("provideInformation");
        //list.Add("NoActionRequested");
       
        return list.ToArray();
    }

    private PlayerContext InquirePlayerContext()
    {
        var ctx = new PlayerContext();
        if (!PlayerVisible && !PlayerAudible)
        {
            ctx.location = "unknown";
            ctx.assetsInView = new[] { "" };
            ctx.inConversation = false;
            return ctx;
        }

        ctx.location = "Gallery";
        ctx.assetsInView = new[] { " " };
        ctx.inConversation = false;
        ctx.playerUsername = "Sir Grandmasterr";
        return ctx;
    }

    public void UpdateVisibility(bool update)
    {
        if (PlayerVisible == update) return;
        PlayerVisible = update;
        SetHeadColor();
    }

    public void UpdateConversationStatus(bool update)
    {
        if (PlayerInConversation == update) return;
        PlayerInConversation = update;
        if (update)
        {
            movementController.EnableConversationBodyLanguage();
        }
        else
        {
            movementController.DisableConversationBodyLanguage();
        }

        SetHeadColor();
    }

    //Called by Eyes, updates GazeObject in MovementController to look at same object as player does.
    public void UpdatePlayerGaze(GazeObject gaze, bool visible)
    {
        PlayerGaze = gaze;
        if (!visible)
        {
            PlayerGaze.Valid = false;
        } 

        movementController.UpdatePlayerGaze(PlayerGaze);
    }

    private void SetHeadColor()
    {
        if (PlayerVisible && !PlayerInConversation)
        {
            headMaterial.color = new Color(0.43f, 0.73f, 0.28f, 255);
        }

        if (PlayerVisible && movementController.movementState == 2)
        {
            headMaterial.color = new Color(0.9339623f, 0.2599235f, 0.6762744f);
        }
        
        if (PlayerVisible && PlayerInConversation)
        {
            headMaterial.color = new Color(0.123f, 0.18f, 0.99f, 255);
            
        }
    }

    public void MusicFound(MusicAction.musicFeedback feedback)
    {
        print("MusicFound triggered");
        if (feedback.found)
        {
            SendHistoryUpdate("NARRATOR: " + feedback.query + "music starts playing." );
            SendActionUpdate(feedback.token, "playMusic", 3, false, "");
            return;
        }
        
        SendActionUpdate(feedback.token, "playMusic", 3, true, ""); // Trigger speech stage that tells user that no music was found. 

    }
}


[Serializable]
public class LlamaWebsockMsg
{
    public string type;
    public string text;
    public string actionName;
    public string token;
    public int stage;

    public static LlamaWebsockMsg CreateFromJson(string jsonString)
    {
        Debug.Log(jsonString);
        return JsonUtility.FromJson<LlamaWebsockMsg>(jsonString);
    }

    // Given JSON input:
    // {"name":"Dr Charles","lives":3,"health":0.8}
    // this example will return a PlayerInfo object with
    // name == "Dr Charles", lives == 3, and health == 0.8f.
}



