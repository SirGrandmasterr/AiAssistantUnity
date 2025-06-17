using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using WebSocketSharp; // Ensure you have this library
using Unity.WebRTC;   // Requires Unity WebRTC package

// --- Message Structures for JSON Serialization ---
// These now match the Go server's expected message format.



public class WebRtcProvider : MonoBehaviour
{
    
    public static WebRtcProvider Instance { get; private set; }

    public string connectionState = "";
    
    private WebSocket ws;
    private RTCPeerConnection _peerConnection;
    private MediaStream _receiveStream;
    private readonly Queue<string> _messageQueue = new Queue<string>();
    private bool _processingMessage = false;

    // To store the ID of the provider we are connected to.
    private string _providerId;

    private AudioSource outputAudioSource;

    private void Awake()
    {
        // --- Singleton Implementation ---
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Persist this GameObject across scene loads
            Debug.Log("WebRtcProvider instance created and set to not destroy on load.");
        }
        else
        {
            // If an instance already exists, destroy this new one.
            Destroy(gameObject);
            return;
        }
        // ---
    }

    void Start()
    {
        // if (outputAudioSource == null)
        // {
        //     Debug.LogError("Output AudioSource is not assigned in the Inspector!");
        //     outputAudioSource = gameObject.AddComponent<AudioSource>();
        // }

        StartCoroutine(WebRTC.Update());

        ws = new WebSocket("ws://localhost:8080/ws");

        ws.OnOpen += (sender, e) =>
        {
            Debug.Log("WebSocket connected. Registering as receiver...");
            RegisterAsReceiver();
        };

        ws.OnMessage += (sender, e) =>
        {
            Debug.Log("Message received from server: " + e.Data);
            lock (_messageQueue)
            {
                _messageQueue.Enqueue(e.Data);
            }
        };

        ws.OnError += (sender, e) => Debug.LogError("WebSocket error: " + e.Message);
        ws.OnClose += (sender, e) => Debug.Log($"WebSocket closed. Code: {e.Code}, Reason: {e.Reason}");

        Debug.Log("Attempting to connect to WebSocket...");
        ws.ConnectAsync();

        SetupPeerConnection();
    }

    void Update()
    {
        if (_messageQueue.Count > 0 && !_processingMessage)
        {
            string message;
            lock (_messageQueue)
            {
                message = _messageQueue.Dequeue();
            }
            _processingMessage = true;
            StartCoroutine(HandleServerMessage(message));
        }
        
        // Example of how to send a text message for TTS
        if (Input.GetKeyDown(KeyCode.T))
        {
            Debug.Log("'T' key pressed. Sending test text for TTS.");
            string testText = "Hello from Unity! This is a test of the text-to-speech system.";
            _ = SendTextMessageForTTS(testText);
        }
    }

    public string GetConnectionState()
    {
        return connectionState;
    }
    
    public void SetAudioSource(AudioSource source)
    {
        if (source != null)
        {
            outputAudioSource = source;
            Debug.Log("AudioSource has been successfully assigned to the WebRtcProvider.");
        }
        else
        {
            Debug.LogError("Attempted to assign a null AudioSource.");
        }
    }

    private void SetupPeerConnection()
    {
        var configuration = GetSelectedSdpSemantics();
        _peerConnection = new RTCPeerConnection(ref configuration);
        Debug.Log("RTCPeerConnection created.");

        _peerConnection.OnIceCandidate = candidate =>
        {
            if (candidate != null && !string.IsNullOrEmpty(_providerId))
            {
                SendIceCandidate(candidate);
            }
        };

        _receiveStream = new MediaStream();
        _receiveStream.OnAddTrack += OnAddTrackEvent;
        _peerConnection.OnTrack = e =>
        {
            Debug.Log($"Track received: {e.Track.Kind}");
            if (e.Track.Kind == TrackKind.Audio)
            {
                _receiveStream.AddTrack(e.Track);
            }
        };

        var audioTransceiverInit = new RTCRtpTransceiverInit { direction = RTCRtpTransceiverDirection.RecvOnly };
        _peerConnection.AddTransceiver(TrackKind.Audio, audioTransceiverInit);
        Debug.Log("Added audio transceiver with direction RecvOnly.");

        _peerConnection.OnIceConnectionChange += state => Debug.Log($"ICE Connection State Changed: {state}");
        _peerConnection.OnConnectionStateChange += state =>
        {
            Debug.Log($"Peer Connection State Changed: {state}");
            connectionState = state.ToString();
            if (state == RTCPeerConnectionState.Failed || state == RTCPeerConnectionState.Closed || state == RTCPeerConnectionState.Disconnected)
            {
                Debug.LogWarning("Peer connection lost. Consider cleaning up and resetting.");
                // Here you might want to reset the peer connection or the provider ID
                _providerId = null;
            }
        };
    }

    private RTCConfiguration GetSelectedSdpSemantics()
    {
        return new RTCConfiguration
        {
            iceServers = new RTCIceServer[]
            {
                new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } }
            }
        };
    }

    private void RegisterAsReceiver()
    {
        var msg = new Message { type = "register", role = "receiver" };
        ws.Send(JsonUtility.ToJson(msg));
    }

    private IEnumerator HandleServerMessage(string jsonMessage)
    {
        Message msg;
        try
        {
            msg = JsonUtility.FromJson<Message>(jsonMessage);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to parse message JSON: {jsonMessage}. Error: {ex.Message}");
            _processingMessage = false;
            yield break;
        }

        if (msg == null)
        {
            Debug.LogError($"Parsed message is null: {jsonMessage}");
             _processingMessage = false;
            yield break;
        }


        switch (msg.type)
        {
            case "offer":
                Debug.Log($"Received offer from provider with ID: {msg.id}. Creating answer...");
                
                // **CRITICAL**: Store the provider's ID to use as the target for our responses.
                _providerId = msg.id;

                var offerDesc = new RTCSessionDescription { type = RTCSdpType.Offer, sdp = msg.sdp.sdp };
                var setRemoteOp = _peerConnection.SetRemoteDescription(ref offerDesc);
                yield return setRemoteOp;

                if (setRemoteOp.IsError)
                {
                    Debug.LogError($"Failed to set remote description (offer): {setRemoteOp.Error.message}");
                    break;
                }
                Debug.Log("Remote description (offer) set successfully.");

                var createAnswerOp = _peerConnection.CreateAnswer();
                yield return createAnswerOp;

                if (createAnswerOp.IsError)
                {
                    Debug.LogError($"Failed to create answer: {createAnswerOp.Error.message}");
                    break;
                }

                var answerDesc = createAnswerOp.Desc;
                var setLocalOp = _peerConnection.SetLocalDescription(ref answerDesc);
                yield return setLocalOp;

                if (setLocalOp.IsError)
                {
                    Debug.LogError($"Failed to set local description (answer): {setLocalOp.Error.message}");
                    break;
                }
                
                // **CRITICAL**: Send the answer back to the correct provider.
                var answerSdp = new SdpObject { sdp = answerDesc.sdp, type = "answer" };
                var answerMsg = new Message { type = "answer", sdp = answerSdp, targetId = _providerId };
                ws.Send(JsonUtility.ToJson(answerMsg));
                Debug.Log($"Sent answer to provider {_providerId}.");
                break;

            case "candidate":
                Debug.Log($"Received ICE candidate from provider {_providerId}.");
                var iceCandidate = new RTCIceCandidate(new RTCIceCandidateInit
                {
                    candidate = msg.candidate.candidate,
                    sdpMid = msg.candidate.sdpMid,
                    sdpMLineIndex = msg.candidate.sdpMLineIndex
                });
                _peerConnection.AddIceCandidate(iceCandidate);
                Debug.Log("Added remote ICE candidate.");
                break;
            
            case "error":
                Debug.LogError($"Received error from server: {msg.error}");
                break;

            default:
                Debug.LogWarning("Unexpected message type received: " + msg.type);
                break;
        }
        _processingMessage = false;
    }

    private void SendIceCandidate(RTCIceCandidate rtcCandidate)
    {
        if (string.IsNullOrEmpty(rtcCandidate.Candidate)) return;

        var candidatePayload = new RTCIceCandidateInitShim
        {
            candidate = rtcCandidate.Candidate,
            sdpMid = rtcCandidate.SdpMid,
            sdpMLineIndex = (ushort?)rtcCandidate.SdpMLineIndex,
            usernameFragment = rtcCandidate.UserNameFragment
        };
        
        // **CRITICAL**: Send the candidate to the correct provider.
        var msg = new Message { type = "candidate", candidate = candidatePayload, targetId = _providerId };
        string jsonMsg = JsonUtility.ToJson(msg);
        Debug.Log($"Sending local ICE candidate to provider {_providerId}.");
        ws.Send(jsonMsg);
    }

    private void OnAddTrackEvent(MediaStreamTrackEvent e)
    {
        if (e.Track is AudioStreamTrack audioTrack)
        {
            if (outputAudioSource != null)
            {
                Debug.Log("Audio track received. Setting to AudioSource.");
                outputAudioSource.SetTrack(audioTrack);
                outputAudioSource.loop = false; // TTS audio should not loop
                outputAudioSource.Play();
                Debug.Log("AudioSource started playing received track.");
            }
            else
            {
                Debug.LogWarning("Received an audio track, but the AudioSource is not yet assigned!");
            }
        }
    }
    
    public Task SendTextMessageForTTS(string text, string voice = "dan")
    {
        if (ws != null && ws.IsAlive)
        {
            var textMsg = new TextMessage { text = text, voice = voice };
            ws.Send(JsonUtility.ToJson(textMsg));
            Debug.Log($"Sent text for TTS: '{text}'");
        }
        else
        {
            Debug.LogWarning("Cannot send text message, WebSocket is not connected.");
        }
        return Task.CompletedTask;
    }

    void OnDestroy()
    {
        Debug.Log("WebRTCProvider OnDestroy called.");
        if (_peerConnection != null)
        {
            _peerConnection.Close();
            _peerConnection = null;
        }
        if (_receiveStream != null)
        {
            _receiveStream.Dispose();
            _receiveStream = null;
        }
        if (ws != null && ws.IsAlive)
        {
            ws.CloseAsync();
        }
    }
}
[System.Serializable]
public class SdpObject
{
    public string sdp;
    public string type;
}

[System.Serializable]
public class RTCIceCandidateInitShim
{
    public string candidate;
    public string sdpMid;
    public ushort? sdpMLineIndex;
    public string usernameFragment;
}

[System.Serializable]
public class Message
{
    public string type;
    public string role;
    public string id; // ID of the sender (e.g., the provider who sent the offer)
    public string targetId; // ID of the recipient (e.g., the provider we are sending an answer to)
    public SdpObject sdp;
    public RTCIceCandidateInitShim candidate;
    public string error;
}

[System.Serializable]
public class TextMessage
{
    public string type = "text_message";
    public string text;
    public string voice = "dan"; // Specify a voice, can be changed
}