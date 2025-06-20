using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using WebSocketSharp;
using Unity.WebRTC;
using uLipSync;

public class WebRtcProvider : MonoBehaviour
{
    public static WebRtcProvider Instance { get; private set; }
    public string connectionState = "";
    
    private WebSocket ws;
    private RTCPeerConnection _peerConnection;
    private MediaStream _receiveStream;
    private readonly Queue<string> _messageQueue = new Queue<string>();
    private bool _processingMessage = false;
    private AudioStreamTrack _audioTrack;
    private string _providerId;

    // --- MODIFIED: Store references to all linked avatar components ---
    private AudioSource _linkedAudioSource;
    private uLipSync.uLipSync _linkedLipSync;
    private AudioEmotionRecognizer _linkedEmotionRecognizer; // --- NEW: Add reference to the emotion recognizer ---
    private float[] _monoBuffer; // Buffer for converting stereo to mono

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("WebRtcProvider instance created and set to not destroy on load.");
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        StartCoroutine(WebRTC.Update());
        ws = new WebSocket("ws://localhost:8080/ws");
        ws.OnOpen += (sender, e) => RegisterAsReceiver();
        ws.OnMessage += (sender, e) => { lock (_messageQueue) { _messageQueue.Enqueue(e.Data); } };
        ws.OnError += (sender, e) => Debug.LogError("WebSocket error: " + e.Message);
        ws.OnClose += (sender, e) => Debug.Log($"WebSocket closed. Code: {e.Code}, Reason: {e.Reason}");
        ws.ConnectAsync();
        SetupPeerConnection();
    }

    void Update()
    {
        if (_messageQueue.Count > 0 && !_processingMessage)
        {
            string message;
            lock (_messageQueue) { message = _messageQueue.Dequeue(); }
            _processingMessage = true;
            StartCoroutine(HandleServerMessage(message));
        }
    }
    
    // --- MODIFIED: New method signature to link all necessary components from the avatar ---
    /// <summary>
    /// Links the necessary avatar components to the WebRTC Provider.
    /// This allows the provider to stream audio data for playback, lip-sync, and emotion recognition.
    /// </summary>
    /// <param name="source">The AudioSource for audio playback.</param>
    /// <param name="lipSync">The uLipSync component for lip-sync animation.</param>
    /// <param name="emotionRecognizer">The AudioEmotionRecognizer for emotion analysis.</param>
    public void LinkAvatarComponents(AudioSource source, uLipSync.uLipSync lipSync, AudioEmotionRecognizer emotionRecognizer)
    {
        if (source != null)
        {
            _linkedAudioSource = source;
            Debug.Log("AudioSource has been successfully linked to the WebRtcProvider.");
        }
        else
        {
            Debug.LogError("Attempted to link a null AudioSource.");
        }

        if (lipSync != null)
        {
            _linkedLipSync = lipSync;
            _linkedLipSync.manualAudioInput = true;
            Debug.Log("uLipSync component has been successfully linked and set to manual input mode.");
        }
        else
        {
            Debug.LogError("Attempted to link a null uLipSync component.");
        }
        
        // --- NEW: Link the AudioEmotionRecognizer ---
        if (emotionRecognizer != null)
        {
            _linkedEmotionRecognizer = emotionRecognizer;
            Debug.Log("AudioEmotionRecognizer has been successfully linked.");
        }
        else
        {
            Debug.LogWarning("Attempted to link a null AudioEmotionRecognizer.");
        }

        // If an audio track has already been received, configure it now.
        if (_audioTrack != null)
        {
            SetupAudioTrack();
        }
    }

    private void OnAddTrackEvent(MediaStreamTrackEvent e)
    {
        if (e.Track is AudioStreamTrack audioTrack)
        {
            _audioTrack = audioTrack;
            SetupAudioTrack();
        }
    }

    // --- MODIFIED: Centralized method for setting up the audio track for all consumers ---
    private void SetupAudioTrack()
    {
        // 1. Set up audio for playback via the linked AudioSource
        if (_linkedAudioSource != null)
        {
            Debug.Log("Audio track received. Setting to linked AudioSource for playback.");
            _linkedAudioSource.SetTrack(_audioTrack);
            _linkedAudioSource.loop = false;
            _linkedAudioSource.Play();
        }
        else
        {
            Debug.LogWarning("Received an audio track, but the AudioSource is not yet linked!");
        }

        // 2. Set up audio for lip-sync analysis by subscribing to the raw data event
        if (_linkedLipSync != null)
        {
            Debug.Log("Subscribing to OnAudioReceived event for lip-sync analysis.");
            _audioTrack.onReceived += OnAudioReceivedForLipSync;
        }
        else
        {
             Debug.LogWarning("Received an audio track, but the uLipSync component is not yet linked!");
        }
        
        // --- NEW: Set up audio for emotion recognition ---
        if (_linkedEmotionRecognizer != null)
        {
            Debug.Log("Subscribing to OnAudioReceived event for emotion analysis.");
            _audioTrack.onReceived += OnAudioReceivedForEmotion;
        }
        else
        {
            Debug.LogWarning("Received an audio track, but the AudioEmotionRecognizer is not yet linked!");
        }
    }
    
    private void OnAudioReceivedForLipSync(float[] data, int channels, int timestamp)
    {
        if (_linkedLipSync == null) return;
        
        // uLipSync's InjectAudioData expects a mono buffer.
        // We must convert the data if the source is stereo or multi-channel.
        if (channels > 1)
        {
            int monoLength = data.Length / channels;
            if (_monoBuffer == null || _monoBuffer.Length != monoLength)
            {
                _monoBuffer = new float[monoLength];
            }
            
            // De-interleave the data, taking only the first channel.
            for (int i = 0; i < monoLength; i++)
            {
                _monoBuffer[i] = data[i * channels];
            }
            _linkedLipSync.InjectAudioData(_monoBuffer);
        }
        else
        {
            // Audio is already mono, inject it directly.
            _linkedLipSync.InjectAudioData(data);
        }
    }

    // --- NEW: Event handler to feed raw audio data directly to the AudioEmotionRecognizer ---
    private void OnAudioReceivedForEmotion(float[] data, int channels, int timestamp)
    {
        Debug.Log("timestamp: " + timestamp);
        if (_linkedEmotionRecognizer != null && _audioTrack != null)
        {
            // Inject the raw audio data, number of channels, and the track's sample rate
            // into the emotion recognizer's buffer.
            _linkedEmotionRecognizer.InjectAudioData(data, channels, timestamp);
        }
    }
    
    #region WebRTC and WebSocket Logic
    public string GetConnectionState() => connectionState;

    private void SetupPeerConnection()
    {
        var configuration = new RTCConfiguration { iceServers = new[] { new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } } };
        _peerConnection = new RTCPeerConnection(ref configuration);
        _peerConnection.OnIceCandidate = candidate => { if (candidate != null && !string.IsNullOrEmpty(_providerId)) SendIceCandidate(candidate); };
        _receiveStream = new MediaStream();
        _receiveStream.OnAddTrack += OnAddTrackEvent;
        _peerConnection.OnTrack = e => { if (e.Track.Kind == TrackKind.Audio) _receiveStream.AddTrack(e.Track); };
        _peerConnection.AddTransceiver(TrackKind.Audio, new RTCRtpTransceiverInit { direction = RTCRtpTransceiverDirection.RecvOnly });
        _peerConnection.OnConnectionStateChange += state => { connectionState = state.ToString(); Debug.Log($"Peer Connection State Changed: {state}"); };
    }

    private void RegisterAsReceiver()
    {
        var msg = new Message { type = "register", role = "receiver" };
        ws.Send(JsonUtility.ToJson(msg));
    }

    private IEnumerator HandleServerMessage(string jsonMessage)
    {
        Message msg;
        try { msg = JsonUtility.FromJson<Message>(jsonMessage); }
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
                _providerId = msg.id;
                var offerDesc = new RTCSessionDescription { type = RTCSdpType.Offer, sdp = msg.sdp.sdp };
                var setRemoteOp = _peerConnection.SetRemoteDescription(ref offerDesc);
                yield return setRemoteOp;
                if (setRemoteOp.IsError) { Debug.LogError($"SetRemoteDescription failed: {setRemoteOp.Error.message}"); break; }

                var createAnswerOp = _peerConnection.CreateAnswer();
                yield return createAnswerOp;
                if (createAnswerOp.IsError) { Debug.LogError($"CreateAnswer failed: {createAnswerOp.Error.message}"); break; }

                var answerDesc = createAnswerOp.Desc;
                var setLocalOp = _peerConnection.SetLocalDescription(ref answerDesc);
                yield return setLocalOp;
                if (setLocalOp.IsError) { Debug.LogError($"SetLocalDescription failed: {setLocalOp.Error.message}"); break; }

                var answerSdp = new SdpObject { sdp = answerDesc.sdp, type = "answer" };
                var answerMsg = new Message { type = "answer", sdp = answerSdp, targetId = _providerId };
                ws.Send(JsonUtility.ToJson(answerMsg));
                break;

            case "candidate":
                var iceCandidate = new RTCIceCandidate(new RTCIceCandidateInit { candidate = msg.candidate.candidate, sdpMid = msg.candidate.sdpMid, sdpMLineIndex = msg.candidate.sdpMLineIndex });
                _peerConnection.AddIceCandidate(iceCandidate);
                break;
            case "error":
                Debug.LogError($"Server error: {msg.error}");
                break;
        }
        _processingMessage = false;
    }

    private void SendIceCandidate(RTCIceCandidate rtcCandidate)
    {
        if (string.IsNullOrEmpty(rtcCandidate.Candidate)) return;
        var candidatePayload = new RTCIceCandidateInitShim { candidate = rtcCandidate.Candidate, sdpMid = rtcCandidate.SdpMid, sdpMLineIndex = (ushort?)rtcCandidate.SdpMLineIndex };
        var msg = new Message { type = "candidate", candidate = candidatePayload, targetId = _providerId };
        ws.Send(JsonUtility.ToJson(msg));
    }
    
    public Task SendTextMessageForTTS(string text, string voice = "dan")
    {
        text = RemoveTextInAsterisks(text);
        text = CleanSentence(text);
        if (ws != null && ws.IsAlive)
        {
            var textMsg = new TextMessage { text = text, voice = voice };
            ws.Send(JsonUtility.ToJson(textMsg));
        }
        return Task.CompletedTask;
    }
    
    public static string RemoveTextInAsterisks(string inputText) => Regex.Replace(inputText ?? "", @"\*.*?\*", string.Empty);
    private static readonly HashSet<string> allowedTags = new HashSet<string> { "laugh", "chuckle", "sigh", "cough", "sniffle", "groan", "yawn", "gasp" };
    public static string CleanSentence(string inputSentence)
    {
        string result = Regex.Replace(inputSentence ?? "", @"<([^>]*)>", (Match match) => allowedTags.Contains(match.Groups[1].Value) ? match.Value : "");
        result = Regex.Replace(result, @"\s+,", ",");
        result = Regex.Replace(result, @"\s\s+", " ").Trim();
        return result;
    }

    void OnDestroy()
    {
        // --- MODIFIED: Unsubscribe all event listeners ---
        if (_audioTrack != null)
        {
            _audioTrack.onReceived -= OnAudioReceivedForLipSync;
            _audioTrack.onReceived -= OnAudioReceivedForEmotion; // --- NEW: Unsubscribe emotion handler
        }
        if (_peerConnection != null) { _peerConnection.Close(); }
        if (_receiveStream != null) { _receiveStream.Dispose(); }
        if (ws != null && ws.IsAlive) { ws.CloseAsync(); }
    }
    #endregion
}

#region Message Structures
[System.Serializable] public class SdpObject { public string sdp; public string type; }
[System.Serializable] public class RTCIceCandidateInitShim { public string candidate; public string sdpMid; public ushort? sdpMLineIndex; public string usernameFragment; }
[System.Serializable] public class Message { public string type; public string role; public string id; public string targetId; public SdpObject sdp; public RTCIceCandidateInitShim candidate; public string error; }
[System.Serializable] public class TextMessage { public string type = "text_message"; public string text; public string voice = "dan"; }
#endregion
