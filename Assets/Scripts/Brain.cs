using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NativeWebSocket;
using UnityEngine;
using UnityEngine.Serialization;
using Random = System.Random;

public class Brain : MonoBehaviour
{
    public WebSocketClient webSocketTtsClient;
    public AssistantMovementController movementController;
    public MusicAction musicManager;
    private WebSocket _websocket;
    public Dictionary<string, int> actionDict;
    private Random rnd;
    private Queue<GameObject> repairQueue;
    private bool repairAvailable;
    public bool isSpeaking;
    

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
        [FormerlySerializedAs("PlayerActionType")] public string playerActionType;
        [FormerlySerializedAs("Speech")] public string speech;
        [FormerlySerializedAs("AssistantContext")] public AssistantContext assistantContext;
        [FormerlySerializedAs("PlayerContext")] public PlayerContext playerContext;
        public ActionContext actionContext;
        public EventContext eventContext;
    }

    
    //State of Assistant
    public bool PlayerAudible;
    public bool PlayerVisible;
    public string[] AssetsInView;
    public bool PlayerInConversation;
    public GazeObject PlayerGaze;
    public float bored;
    public Material headMaterial;

    private bool talkAndFollow;
    
    

    //
  

    // Start is called before the first frame update
    private async void Start()
    {
        isSpeaking = false;
        repairAvailable = false;
        repairQueue = new Queue<GameObject>();
        rnd = new Random();
        bored = 30f;
        StartCoroutine(InnerThought());
        actionDict = new Dictionary<string, int>();
        _websocket = new WebSocket("ws://localhost:3000/ws/id");

        _websocket.OnOpen += () =>
        {
            Debug.Log("Connection to Llamacommunicator open!");
            LlamaWebsockRequest msg = new LlamaWebsockRequest();
            msg.messageType = "initializePlayer";
            msg.playerContext = new PlayerContext();
            msg.playerContext.playerUsername = "Sir Grandmasterr";
            _websocket.SendText(JsonUtility.ToJson(msg));
        };

        _websocket.OnError += e => { Debug.Log("Error! " + e); };

        _websocket.OnClose += _ => { Debug.Log("LlamaConnection Closed!"); };

        _websocket.OnMessage += bytes =>
        {
            Debug.Log("Got a message from LlamaCommunicator");
            //Decode bytes into String, assume it's JSON, Decode into Websocketmsg
            var msg = LlamaWebsockMsg.CreateFromJson(Encoding.UTF8.GetString(bytes));

            if (msg.type == "speech")
            {
                if (!isSpeaking)
                {
                    SendHistoryUpdate("ASSISTANT: '" + msg.text);
                    isSpeaking = true;
                }
                else if (msg.actionName == "stopSpeak")
                {
                    SendHistoryUpdate(msg.text + "'");
                }
                else
                {
                    SendHistoryUpdate(msg.text);
                }
                Task task = webSocketTtsClient.ListenTo(msg.text);
                return;
            }

            if (msg.type == "actionSelection")
            {   // only single-stage actions for this
                var secondary = msg.text;
                var original = msg.actionName;
                msg.actionName = secondary;
                EvaluateAction(msg);
                msg.actionName = original;
                EvaluateAction(msg);
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
        var ac = InquireAssistantContext(true,false);
        var pc = InquirePlayerContext();
        print(ac.ToString());
        print(pc.ToString());
        LlamaWebsockRequest msg = new LlamaWebsockRequest();
        msg.messageType = "speech"; // "speech" "assistantUpdate" "playerAction" "suggestAction"
        msg.playerActionType = "speech"; // "speech", "enteringVision", "leavingVision", "vandalism"
        msg.speech = text;
        msg.assistantContext = ac;
        msg.playerContext = pc;
        msg.eventContext = new EventContext();

        if (PlayerInConversation && PlayerGaze.Valid)
        {
            msg.assistantContext.focusedAsset = PlayerGaze.ObjectOfInterest.name;
        }
        
        print("Focused Asset: "+ msg.assistantContext.focusedAsset);
        _websocket.SendText(JsonUtility.ToJson(msg));
    }

    public void SendHistoryUpdate(string text)
    {
        var ac = InquireAssistantContext(false,false);
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
        print(JsonUtility.ToJson(msg));
        _websocket.SendText(JsonUtility.ToJson(msg));
    }

    public void SendActionUpdate(string token, string actionName, int stage, bool permission, string speech, string focus, string[] options)
    {
        print("Sending ActionUpdate");
        var ac = InquireAssistantContext(false,false);
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
        
        print(JsonUtility.ToJson(msg));
        _websocket.SendText(JsonUtility.ToJson(msg));
    }

    public void SendEnvEvent(string description, EventContext eventContext, string[] actionOptions)
    {
        SendHistoryUpdate(description);
        var ac = InquireAssistantContext(false,false);
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
        _websocket.SendText(JsonUtility.ToJson(msg));
    }

    public void SendInnerThoughtEvent()
    {
        var ac = InquireAssistantContext(false, true);
        var pc = InquirePlayerContext();
        var acc = new ActionContext();
        acc.actionName = "";
        acc.permission = false;
        acc.token = "";
        acc.stage = 0;
        LlamaWebsockRequest msg = new LlamaWebsockRequest();
        msg.messageType = "innerThoughtEvent"; // "speech" "assistantUpdate" "playerAction" "suggestAction"
        msg.playerActionType = ""; // "speech", "enteringVision", "leavingVision", "vandalism"
        msg.speech = "speech";
        msg.assistantContext = ac;
        msg.playerContext = pc;
        _websocket.SendText(JsonUtility.ToJson(msg));
    }


    private async void EvaluateAction(LlamaWebsockMsg msg)
    {   Debug.Log("Received Action Instruction");
        Debug.Log(msg.actionName);
        switch (msg.actionName)
        {
            case "followVisitor":
                SendHistoryUpdate("NARRATOR: " + "The Assistant started following the visitor.\n");
                movementController.FollowVisitor(); 
                break;
            case "provideArtInformation":
                break;
            case "walkToVisitor":
                movementController.WalkToPlayer();
                break;
            case "walkToObject":
                switch (msg.stage)
                {
                    case 1:
                        break;
                    case 2:
                        movementController.WalkForce(GameObject.Find(msg.text)); // Walk to location, then idle
                        bored = rnd.Next(10, 20);
                        break;
                }

                break;
            case "admireArt":    
                switch (msg.stage)
                {
                    case 1:
                        break;
                    case 2:
                        
                        SendHistoryUpdate("NARRATOR: " + "The assistant begins admiring " + msg.text + "\n");
                        movementController.WalkToLocation(msg.text, 0); // Walk to location, then idle
                        bored = rnd.Next(10, 20);
                        break;
                }
                
                break;
            case "stopFollowingVisitor":
                movementController.movementState = 0;
                bored = rnd.Next(10, 30);
                break;
            case "standIdle":
                movementController.movementState = 0;
                bored = rnd.Next(10, 20);
                break;
            case "repair":
                switch (msg.stage)
                {
                    case 1: // We know repair was selected.
                        print("Case 1");
                        actionDict.Add(msg.token, 1);
                        if (repairQueue.Count > 0)
                        {
                           var obj = repairQueue.Peek();
                           SendActionUpdate(msg.token, msg.actionName, msg.stage +1, true, "", obj.name, new  string[]{} );
                        }
                        break;
                    case 2:
                        print("Case 2"); //
                        var targetobj = repairQueue.Dequeue(); // We do get the object as an answer, but this is less performance intensive.
                        movementController.Walk(targetobj);
                        repairAvailable = false;
                        SendActionUpdate(msg.token, msg.actionName, msg.stage +1, true, "", "", new  string[]{"followVisitor"} );
                        break;
                    case 3:
                        print("Case 3");
                        break;
                    case 4:    
                        print("Case 4");
                        SendActionUpdate(msg.token, msg.actionName, msg.stage +1, true, "", "", new  string[]{"stopFollowingVisitor"} );
                        break;
                    case 5:
                        actionDict.Remove(msg.token);
                        break;
                }

                break;
            case "patrol":
                movementController.movementState = 1;
                bored = rnd.Next(10, 20);
                break;
            case "playMusic":
                switch (msg.stage)
                {
                    case 1:
                        print("Adding playMusic Token: " + msg.token);
                        actionDict.Add(msg.token, 1);
                        break;
                    case 2:
                        await musicManager.LoadAudioqueue(msg.text, msg.token);
                        actionDict[msg.token] = 2;
                        print("Updating playMusic Token: " + msg.token);
                        bored = rnd.Next(10, 20);
                        break;
                }
                break;
            case "investigate":
                switch (msg.stage) 
                {
                    case 1:
                        print("Made Decision to investigate");
                        break;
                    case 2:
                        SendHistoryUpdate("NARRATOR: " + "The Assistant walked to " + msg.text + "and started investigating.\n");
                        movementController.WalkToLocation(msg.text, 1); // Walk to location, then patrol
                        bored = rnd.Next(10, 20);
                        break; 
                }

                break;
            case "explainOptions":
                break;
            case "ignore":
                break;
            case "stopMusic":
                if (musicManager.isPlaying)
                {
                    SendHistoryUpdate("NARRATOR: " + "The Music stopped. \n");
                    musicManager.StopPlay();
                }

                break;
        }
    }


    private AssistantContext InquireAssistantContext(bool speech, bool innerThought )
    {
        string walkingstate;
        string[] opts;
        opts = IdeateActionOptions(speech, innerThought);

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
            case 3:
                walkingstate = "moving";
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


    private IEnumerator  InnerThought()
        //This function is a loop that occasionally asks the Assistant what he wants to do, as long as the user is not involved.
    {
        while (true)
        {
            if (PlayerInConversation || movementController.movementState == 2)
            {
                bored = 30f;
            }
            if (bored <= 0)
            {
                print("I'm bored and will think about what to do.");
                SendInnerThoughtEvent();
                bored = rnd.Next(10, 20);
            }
            else
            {
                yield return new WaitForSeconds(0.2f);
                bored = bored - 0.2f;
            }
        }
        
    }
    //Function to find out what actions the LLM can take at given time.
    private String[] IdeateActionOptions(bool speech, bool innerThought)
    {
        var list =new List<string>();
        if (innerThought)
        {
            list.Add("standIdle");
            list.Add("patrol");
            list.Add("admireArt");
            if (repairAvailable)
            {
                list.Add("repair");
            }
            return list.ToArray();
        }
        
        if (movementController.movementState == 2)
        {
            list.Add("stopFollowingVisitor");
        }
        else
        {
            list.Add("followVisitor");
        }

        if (speech && !PlayerInConversation)
        {
            list.Add("explainWhatYouCanDo");
            list.Add("continueConversation");
            list.Add("walkToObject");
            list.Add("walkToVisitor");
            if (musicManager.isPlaying)
            {
                list.Add("stopMusic");
            }
            else
            {
                list.Add("playMusic");
            }
        } else if (speech && PlayerInConversation)
        {
            list.Add("explainWhatYouCanDo");
            list.Add("continueConversation");
            list.Add("provideArtInformation");
            list.Add("walkToObject");
            if (musicManager.isPlaying)
            {
                list.Add("stopMusic");
            }
            else
            {
                list.Add("playMusic");
            }
        }
       
        return list.ToArray();
    }

    private PlayerContext InquirePlayerContext()
    {
        var ctx = new PlayerContext();
        if (!PlayerVisible && !PlayerAudible)
        {
            ctx.location = "unknown";
            ctx.assetsInView = new[] { "" };
            ctx.inConversation = PlayerInConversation;
            ctx.playerUsername = "Sir Grandmasterr";
            return ctx;
        }

        ctx.location = "unknown";
        ctx.assetsInView = new[] { " " };
        ctx.inConversation = PlayerInConversation;
        ctx.playerUsername = "Sir Grandmasterr";
        return ctx;
    }

    public void UpdateVisibility(bool update)
    {
        if (PlayerVisible == update) return;
        PlayerVisible = update;
        SetHeadColor();
    }

    public void UpdateVisibleAssets(string[] objs)
    {
        AssetsInView = objs;
        if (repairQueue.Count > 0 && movementController.movementState == 2)
        {
            if (AssetsInView.Contains(repairQueue.Peek().name))
            {
                repairAvailable = true;
            }
        }
    }

    public void UpdateConversationStatus(bool update)
    {
        if (PlayerInConversation == update) return;
        PlayerInConversation = update;
        if (update)
        {
            SendHistoryUpdate("NARRATOR: " + "The visitor entered a conversation with the Assistant.\n");
            movementController.EnableConversationBodyLanguage();
        }
        else
        {
            SendHistoryUpdate("NARRATOR: " + "The conversation stopped.\n");
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
        print(feedback.token);
        if (feedback.found)
        {
            SendHistoryUpdate("NARRATOR: " + feedback.query + "music starts playing.\n" );
            SendActionUpdate(feedback.token, "playMusic", 3, false, "", null, new  string[]{} );
            actionDict.Remove(feedback.token);
            return;
        }
        
        SendActionUpdate(feedback.token, "playMusic", 3, true, "", null, new  string[]{} ); // Trigger speech stage that tells user that no music was found. 
        actionDict.Remove(feedback.token);

    }

    public void AddToRepairQueue(GameObject obj)
    { 
        
        repairQueue.Enqueue(obj);
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



