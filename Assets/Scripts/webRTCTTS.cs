using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using WebSocketSharp; // Make sure you have this library (e.g., from Unity Asset Store or package)
using Unity.WebRTC; // Requires Unity WebRTC package
using UnityEngine.Serialization;

// --- Message Structures ---
// These classes help in serializing/deserializing JSON messages
// to match the JavaScript client's structure.



public class WebRtcProvider : MonoBehaviour
{
    private WebSocket ws;
    private RTCPeerConnection _peerConnection;
    private MediaStream _receiveStream; // MediaStream to hold incoming tracks
    private readonly Queue<string> _messageQueue = new Queue<string>();
    private bool _processingMessage = false; // To ensure one message is processed at a time by HandleServerMessage

    [SerializeField] private AudioSource outputAudioSource; // Assign this in the Unity Editor

    void Start()
    {
        if (outputAudioSource == null)
        {
            Debug.LogError("Output AudioSource is not assigned in the Inspector!");
            // Optionally, try to add one dynamically, though assignment in editor is preferred
            // outputAudioSource = gameObject.AddComponent<AudioSource>();
        }

        StartCoroutine(WebRTC.Update()); // Needed for WebRTC operations to run

        // Initialize WebSocket connection to signaling server
        ws = new WebSocket("ws://localhost:8080/ws"); // Replace with your signaling server URL

        ws.OnOpen += (sender, e) =>
        {
            Debug.Log("WebSocket connected. Registering as receiver...");
            RegisterAsReceiver();
        };

        ws.OnMessage += (sender, e) =>
        {
            // Message received from WebSocket, queue it for processing on the main thread
            Debug.Log("Message received from server: " + e.Data);
            lock (_messageQueue)
            {
                _messageQueue.Enqueue(e.Data);
            }
        };

        ws.OnError += (sender, e) =>
        {
            Debug.LogError("WebSocket error: " + e.Message);
        };

        ws.OnClose += (sender, e) =>
        {
            Debug.Log($"WebSocket closed. Code: {e.Code}, Reason: {e.Reason}");
        };

        Debug.Log("Attempting to connect to WebSocket...");
        ws.ConnectAsync(); // Use ConnectAsync for non-blocking connection

        // Set up WebRTC peer connection
        SetupPeerConnection();
    }

    void Update()
    {
        // Process messages from the queue on the main thread
        if (_messageQueue.Count > 0 && !_processingMessage)
        {
            string message;
            lock (_messageQueue)
            {
                if (_messageQueue.Count > 0) // Double check, another thread might have cleared it
                {
                    message = _messageQueue.Dequeue();
                }
                else
                {
                    return;
                }
            }
            _processingMessage = true;
            StartCoroutine(HandleServerMessage(message));
        }
        
        if (Input.GetKeyDown("t"))
        {
            print("Pressed e");
            ActivateTestText();
        }
    }
    
    private void ActivateTestText()
    {
        const string str = @"When predicting the future, such as the longevity of the Berlin Wall, the hypotheses we need to
        evaluate are all the possible durations of the phenomenon at hand: will it last a week, a month, a year,
            a decade? To apply Bayes’s Rule, as we have seen, we first need to assign a prior probability to each
            of these durations. And it turns out that the Copernican Principle is exactly what results from applying
            Bayes’s Rule using what is known as an uninformative prior";
        _ = ListenTo(str);
    }


    private void SetupPeerConnection()
    {
        var configuration = GetSelectedSdpSemantics(); // Get default configuration

        _peerConnection = new RTCPeerConnection(ref configuration);
        Debug.Log("RTCPeerConnection created.");

        // Subscribe to ICE candidate events
        _peerConnection.OnIceCandidate = candidate =>
        {
            // This event is triggered when a local ICE candidate is gathered.
            // Send this candidate to the remote peer via the signaling server.
            if (candidate != null)
            {
                SendIceCandidate(candidate);
            }
        };

        // Subscribe to track events (when a remote track is added)
        _receiveStream = new MediaStream(); // Initialize the stream that will hold received tracks
        _receiveStream.OnAddTrack += OnAddTrackEvent; // Your handler when a track is added to this stream

        _peerConnection.OnTrack = e =>
        {
            // This event is triggered when the RTCPeerConnection receives a remote track.
            Debug.Log($"Track received: {e.Track.Kind}");
            if (e.Track.Kind == TrackKind.Audio) // We are interested in audio tracks
            {
                _receiveStream.AddTrack(e.Track); // Add the track to our MediaStream
                                                  // This will trigger _receiveStream.OnAddTrack
            }
            // If you were expecting video, you'd handle e.Track.Kind == TrackKind.Video here.
        };
        
        // For a receiver, we expect to receive media.
        // Add a transceiver for audio, set to receive-only. This is CRUCIAL.
        // This tells the other peer what kind of media we are prepared to receive.
        // It's important to do this BEFORE an offer/answer exchange that establishes the track.
        var audioTransceiverInit = new RTCRtpTransceiverInit { direction = RTCRtpTransceiverDirection.RecvOnly };
        _peerConnection.AddTransceiver(TrackKind.Audio, audioTransceiverInit);
        Debug.Log("Added audio transceiver with direction RecvOnly.");


        // OnNegotiationNeeded is typically handled by the peer initiating the call (provider/caller).
        // For a pure receiver role that waits for an offer (like the HTML example),
        // this might not be needed or could lead to "glare" if both sides try to offer.
        // AddTransceiver can trigger OnNegotiationNeeded. If the provider sends the offer,
        // the receiver usually doesn't need to send one back from here.
        _peerConnection.OnNegotiationNeeded = () =>
        {
            Debug.Log("OnNegotiationNeeded event fired. For a receiver, this is often ignored if waiting for an external offer.");
            // If your signaling logic requires the receiver to also be able to initiate an offer,
            // you might call StartCoroutine(CreateOffer()) here.
            // However, to match the HTML example (receiver waits for offer), we'll not create an offer here.
        };

        _peerConnection.OnIceConnectionChange = newstate =>
        {
            Debug.Log($"ICE Connection State Changed: {newstate}");
        };

        _peerConnection.OnConnectionStateChange = newstate =>
        {
             Debug.Log($"Peer Connection State Changed: {newstate}");
        };
    }

    private RTCConfiguration GetSelectedSdpSemantics()
    {
        // Standard configuration with a STUN server.
        // STUN servers help discover public IP addresses and port mappings for NAT traversal.
        return new RTCConfiguration
        {
            iceServers = new RTCIceServer[]
            {
                new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } }
                // You can add more STUN/TURN servers here if needed
                // e.g., new RTCIceServer { urls = new[] { "stun:stun1.l.google.com:19302" } }
            }
        };
    }

    private void RegisterAsReceiver()
    {
        var msg = new Message
        {
            type = "register",
            role = "receiver"
        };
        Debug.Log("Sending receiver registration to signaling server.");
        ws.Send(JsonUtility.ToJson(msg));
    }

    private IEnumerator HandleServerMessage(string jsonMessage)
    {
        Message msg = null;
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

        if (msg == null) {
            Debug.LogError($"Parsed message is null: {jsonMessage}");
            _processingMessage = false;
            yield break;
        }


        Debug.Log($"Handling server message of type: {msg.type}");

        switch (msg.type)
        {
            case "offer":
                // Received an offer from the remote peer (provider)
                if (msg.sdp == null || string.IsNullOrEmpty(msg.sdp.sdp))
                {
                    Debug.LogError("Received offer with invalid SDP.");
                    break;
                }
                Debug.Log("Received offer. Creating answer...");
                // Ensure peer connection is ready (it should be from Start)
                if (_peerConnection == null) SetupPeerConnection();

                var offerDesc = new RTCSessionDescription
                {
                    type = RTCSdpType.Offer, // The received SDP is an offer
                    sdp = msg.sdp.sdp
                };

                var setRemoteDescOp = _peerConnection.SetRemoteDescription(ref offerDesc);
                yield return setRemoteDescOp; // Wait for operation to complete

                if (setRemoteDescOp.IsError)
                {
                    Debug.LogError($"Failed to set remote description (offer): {setRemoteDescOp.Error.message}");
                    break;
                }
                Debug.Log("Remote description (offer) set successfully.");

                // Now create an answer to this offer
                var createAnswerOp = _peerConnection.CreateAnswer();
                yield return createAnswerOp;

                if (createAnswerOp.IsError)
                {
                    Debug.LogError($"Failed to create answer: {createAnswerOp.Error.message}");
                    break;
                }

                var answerDesc = createAnswerOp.Desc;
                var setLocalDescOp = _peerConnection.SetLocalDescription(ref answerDesc);
                yield return setLocalDescOp;

                if (setLocalDescOp.IsError)
                {
                    Debug.LogError($"Failed to set local description (answer): {setLocalDescOp.Error.message}");
                    break;
                }
                Debug.Log("Local description (answer) created and set.");

                // Send the answer back to the provider via the signaling server
                var answerSdpObject = new SdpObject { sdp = answerDesc.sdp, type = "answer" };
                var answerMsg = new Message { type = "answer", sdp = answerSdpObject };
                ws.Send(JsonUtility.ToJson(answerMsg));
                Debug.Log("Sent answer to provider.");
                break;

            case "candidate":
                // Received an ICE candidate from the remote peer
                if (msg.candidate == null || string.IsNullOrEmpty(msg.candidate.candidate))
                {
                    Debug.LogWarning("Received null or empty ICE candidate string. Ignoring.");
                    break;
                }
                Debug.Log($"Received ICE candidate: {msg.candidate.candidate.Substring(0, Math.Min(30, msg.candidate.candidate.Length))}...");

                var iceCandidateInit = new RTCIceCandidateInit
                {
                    candidate = msg.candidate.candidate,
                    sdpMid = msg.candidate.sdpMid,
                    sdpMLineIndex = msg.candidate.sdpMLineIndex // This is ushort? and should map correctly
                };
                _peerConnection.AddIceCandidate(new RTCIceCandidate(iceCandidateInit));
                Debug.Log("Added remote ICE candidate.");
                break;
            
            // This case is typically for the peer that INITIATED the offer.
            // If this receiver doesn't send offers, it might not receive "answer" messages.
            // However, if OnNegotiationNeeded were to send an offer, this would be relevant.
            case "answer":
                Debug.Log("Received answer (should not happen if this peer only receives offers and sends answers).");
                 if (msg.sdp == null || string.IsNullOrEmpty(msg.sdp.sdp))
                {
                    Debug.LogError("Received answer with invalid SDP.");
                    break;
                }
                var remoteAnswerDesc = new RTCSessionDescription
                {
                    type = RTCSdpType.Answer, 
                    sdp = msg.sdp.sdp
                };
                var op = _peerConnection.SetRemoteDescription(ref remoteAnswerDesc);
                yield return op;
                if(op.IsError) {
                    Debug.LogError($"Failed to set remote description (answer): {op.Error.message}");
                } else {
                    Debug.Log("Remote description (answer) set successfully.");
                }
                break;

            case "provider_disconnected":
                Debug.LogWarning("Provider disconnected. Cleaning up WebRTC connection.");
                // ClosePeerConnection(); // Implement cleanup if needed
                break;

            default:
                Debug.LogWarning("Unexpected message type received: " + msg.type);
                break;
        }
        _processingMessage = false;
        yield return null;
    }
    
    // This coroutine is not strictly needed if the receiver doesn't initiate offers.
    // Kept for completeness if that behavior changes.
    private IEnumerator CreateOffer()
    {
        if (_peerConnection == null)
        {
            Debug.LogError("PeerConnection is null, cannot create offer.");
            yield break;
        }
        Debug.Log("Creating offer...");
        var offerOp = _peerConnection.CreateOffer();
        yield return offerOp;

        if (offerOp.IsError)
        {
            Debug.LogError($"CreateOffer error: {offerOp.Error.message}");
            yield break;
        }

        var desc = offerOp.Desc;
        var localDescOp = _peerConnection.SetLocalDescription(ref desc);
        yield return localDescOp;

        if (localDescOp.IsError)
        {
            Debug.LogError($"SetLocalDescription (offer) error: {localDescOp.Error.message}");
            yield break;
        }
        
        Debug.Log("Offer created and local description set.");

        var sdpObject = new SdpObject { sdp = desc.sdp, type = "offer" };
        var offerMsg = new Message { type = "offer", sdp = sdpObject };
        ws.Send(JsonUtility.ToJson(offerMsg));
        Debug.Log("Sent offer to signaling server.");
    }


    private void SendIceCandidate(RTCIceCandidate rtcCandidate)
    {
        // rtcCandidate is from Unity.WebRTC.RTCIceCandidate
        if (string.IsNullOrEmpty(rtcCandidate.Candidate))
        {
            // This can happen; it often signals the end of candidate gathering.
            Debug.Log("Local ICE candidate string is null or empty. Not sending.");
            return;
        }

        // Create a shim object that matches the structure expected by the remote peer (JS)
        if (rtcCandidate.SdpMLineIndex != null)
        {
            var candidatePayload = new RTCIceCandidateInitShim
            {
                candidate = rtcCandidate.Candidate, // The actual candidate string
                sdpMid = rtcCandidate.SdpMid,
                sdpMLineIndex = (ushort)rtcCandidate.SdpMLineIndex, // ushort from RTCIceCandidate
                usernameFragment = rtcCandidate.UserNameFragment // string from RTCIceCandidate
            };

            var msg = new Message // Constructing a proper Message object
            {
                type = "candidate",
                candidate = candidatePayload
            };
            string jsonMsg = JsonUtility.ToJson(msg);
            Debug.Log($"Sending local ICE candidate: {candidatePayload.candidate.Substring(0, Math.Min(30, candidatePayload.candidate.Length))}...");
            ws.Send(jsonMsg);
        }
    }

    // This method is called when a track is added to the _receiveStream
    private void OnAddTrackEvent(MediaStreamTrackEvent e)
    {
        if (e.Track == null)
        {
            Debug.LogError("Track in OnAddTrackEvent is null.");
            return;
        }

        Debug.Log($"Track added to MediaStream: Kind={e.Track.Kind}, ID={e.Track.Id}");

        if (e.Track is AudioStreamTrack audioTrack)
        {
            if (outputAudioSource != null)
            {
                Debug.Log("Audio track received. Setting to AudioSource.");
                outputAudioSource.SetTrack(audioTrack); // Unity.WebRTC extension method
                outputAudioSource.loop = true; // Optional: loop the audio
                outputAudioSource.Play();
                Debug.Log("AudioSource started playing.");
            }
            else
            {
                Debug.LogError("outputAudioSource is null. Cannot play received audio track.");
            }
        }
        else
        {
            Debug.LogWarning($"Received track is not an AudioStreamTrack: {e.Track.Kind}");
        }
    }

    void OnDestroy()
    {
        Debug.Log("WebRTCProvider OnDestroy called.");

        // Clean up WebRTC resources
        if (_peerConnection != null)
        {
            _peerConnection.Close();
            _peerConnection = null;
            Debug.Log("RTCPeerConnection closed.");
        }

        if (_receiveStream != null)
        {
            // Dispose tracks in the stream if necessary, though closing PC should handle this.
            // foreach (var track in _receiveStream.GetTracks()) { track.Dispose(); }
            _receiveStream.Dispose();
            _receiveStream = null;
            Debug.Log("Receive MediaStream disposed.");
        }
       


        // Close WebSocket connection
        if (ws != null && ws.IsAlive)
        {
            ws.CloseAsync();
            Debug.Log("WebSocket connection closed.");
        }
    }
    
    private Task SendWebSocketMessage(string text)
    {
        if (ws != null && ws.IsAlive)
            // Sending plain text
        {
            var textmsg = new TextMessage();
            textmsg.text = text;
            textmsg.type = "text_message";
            
            ws.Send(JsonUtility.ToJson(textmsg));
        }

        
        return Task.CompletedTask;
    }

    public async Task ListenTo(string text)
    {
        print("WebsocketClient listened and sends: " + text);
        await SendWebSocketMessage(text);
    }
}



[System.Serializable]
public class SdpObject
{
    public string sdp; // The actual SDP string
    public string type; // "offer" or "answer"
}

[System.Serializable]
public class RTCIceCandidateInitShim // Shim to represent RTCIceCandidateInit fields for JSON
{
    public string candidate;
    public string sdpMid;
    public ushort? sdpMLineIndex; // Unity's RTCIceCandidateInit uses ushort? for sdpMLineIndex
    public string usernameFragment; // Optional, often null but part of RTCIceCandidate
}

[System.Serializable]
public class Message
{
    public string type; // e.g., "register", "offer", "answer", "candidate", "provider_disconnected"
    public string role; // e.g., "receiver" (for registration message)
    public SdpObject sdp; // For offer/answer messages
    public RTCIceCandidateInitShim candidate; // For ICE candidate messages
}

[System.Serializable]
public class TextMessage
{
    public string type;
    public string text;
}

