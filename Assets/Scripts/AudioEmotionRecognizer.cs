using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Data structures for emotion analysis result from the server.
// Other data structures like EmotionStatistics have been moved to EmotionStatisticsManager.
[System.Serializable]
public class EmotionAnalysis
{
    public List<EmotionResult> timeline;
    public EmotionResult overall;
}

[System.Serializable]
public class BlendshapeWeights
{
    // Eye expressions
    public float eyeBlinkLeft, eyeBlinkRight, eyesLookUp, eyesLookDown, eyeSquintLeft, eyeSquintRight, eyeWideLeft, eyeWideRight;
    // Eyebrow expressions
    public float browDownLeft, browDownRight, browInnerUp, browOuterUpLeft, browOuterUpRight;
    // Mouth expressions
    public float mouthFrownLeft, mouthFrownRight, mouthSmileLeft, mouthSmileRight, mouthPucker, mouthFunnel, mouthDimpleLeft, mouthDimpleRight;
    public float mouthStretchLeft, mouthStretchRight, mouthRollLower, mouthRollUpper, mouthShrugLower, mouthShrugUpper, mouthPressLeft, mouthPressRight;
    public float mouthUpperUpLeft, mouthUpperUpRight, mouthLowerDownLeft, mouthLowerDownRight, mouthLeft, mouthRight;
    // Cheek expressions
    public float cheekPuff, cheekSquintLeft, cheekSquintRight;
    // Nose expressions
    public float noseSneerLeft, noseSneerRight;
    // Jaw expressions
    public float jawForward, jawLeft, jawRight, jawOpen;
    // Tongue
    public float tongueOut;
}

[System.Serializable]
public class BodyAnimation
{
    public Vector3 headRotation, spineRotation, leftShoulderRotation, rightShoulderRotation, leftArmRotation, rightArmRotation, bodyPosition;
}

[System.Serializable]
public class EmotionState
{
    public string currentEmotion = "neutral";
    public float intensity = 0f;
    public float duration = 0f;
    public int consecutiveCount = 0;
    public bool isPersistent = false;
}

[System.Serializable]
public class EmotionResult
{
    public string emotion;
    public float confidence;
    public float timestamp;
    public Dictionary<string, float> probabilities;
}

/// <summary>
/// Analyzes a real-time audio stream injected from an external source for emotional content
/// and translates it into character animations. It offloads statistics management to the
/// EmotionStatisticsManager singleton.
/// </summary>
public class AudioEmotionRecognizer : MonoBehaviour
{
    [Header("Input Settings")]
    [Tooltip("Start analysis automatically when the scene loads.")]
    [SerializeField] private bool analyzeOnStart = true;
    
    [Header("Analysis Settings")]
    [Tooltip("The frequency at which to process the buffered audio for analysis.")]
    [SerializeField] private float analysisInterval = 1.0f;
    [Tooltip("The volume threshold required to trigger an analysis.")]
    [SerializeField] private float audioGainThreshold = 0.01f;

    [Header("Server Settings")]
    [SerializeField] private string serverUrl = "http://localhost:6000";
    [SerializeField] private string analyzeEndpoint = "/analyze_emotion";

    [Header("Blendshape Animation")]
    [SerializeField] private SkinnedMeshRenderer targetRenderer;
    [SerializeField] private bool enableBlendshapeAnimation = true;
    [SerializeField] private float blendshapeTransitionSpeed = 5.0f;
    [SerializeField] private float emotionIntensityMultiplier = 1.0f;

    [Header("Body Animation")]
    [SerializeField] private bool enableBodyAnimation = true;
    [SerializeField] private Transform headTransform, spineTransform, leftShoulderTransform, rightShoulderTransform, leftArmTransform, rightArmTransform;
    [SerializeField] private float bodyAnimationSpeed = 2.0f;
    [SerializeField] private float bodyAnimationIntensity = 1.0f;

    [Header("Persistent Emotion System")]
    [SerializeField] private bool enablePersistentEmotions = true;
    [SerializeField] private int consecutiveThreshold = 2;
    [SerializeField] private float persistentEmotionDuration = 8.0f;
    [SerializeField] private float persistentIntensityMultiplier = 1.2f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    // Public events for other scripts to subscribe to
    public event Action<EmotionResult> OnEmotionDetected;
    public event Action<BlendshapeWeights> OnBlendshapeUpdate;
    public event Action<EmotionState> OnEmotionStateChanged;
    
    // Internal state for audio buffering
    private readonly List<float> _audioBuffer = new List<float>();
    private int _bufferChannels;
    private int _bufferSampleRate;
    private Coroutine _analysisCoroutine;
    
    // Internal state for animation and emotion logic
    public bool isAnalyzing { get; private set; } = false;
    private BlendshapeWeights currentBlendshapes = new BlendshapeWeights();
    private BlendshapeWeights targetBlendshapes = new BlendshapeWeights();
    private BodyAnimation currentBodyAnimation = new BodyAnimation();
    private BodyAnimation targetBodyAnimation = new BodyAnimation();
    private Dictionary<string, int> blendshapeIndices = new Dictionary<string, int>();
    private Dictionary<Transform, Vector3> originalPositions = new Dictionary<Transform, Vector3>();
    private Dictionary<Transform, Quaternion> originalRotations = new Dictionary<Transform, Quaternion>();
    public EmotionState emotionState { get; private set; } = new EmotionState();

    private void Start()
    {
        Log("Audio Emotion Recognizer starting up.");
        InitializeBlendshapeMapping();
        InitializeBodyAnimation();

        if (analyzeOnStart)
        {
            StartAnalysis();
        }
    }

    private void Update()
    {
        if (!isAnalyzing) return;

        if (enableBlendshapeAnimation && targetRenderer != null)
        {
            UpdateBlendshapeAnimation();
        }

        if (enableBodyAnimation)
        {
            UpdateBodyAnimation();
        }

        if (enablePersistentEmotions)
        {
            UpdateEmotionState();
        }
    }
    
    /// <summary>
    /// Injects raw audio data into the recognizer's buffer for analysis.
    /// </summary>
    public void InjectAudioData(float[] audioData, int channels, int sampleRate)
    {
        if (!isAnalyzing) return;

        lock (_audioBuffer)
        {
            _audioBuffer.AddRange(audioData);
            _bufferChannels = channels;
            _bufferSampleRate = sampleRate;
        }
    }

    [ContextMenu("Start Analysis")]
    public void StartAnalysis()
    {
        if (isAnalyzing)
        {
            Log("Analysis is already running.", LogType.Warning);
            return;
        }
        
        isAnalyzing = true;
        _analysisCoroutine = StartCoroutine(AnalysisLoopCoroutine());
        Log("Audio analysis started. Waiting for injected audio data.");
    }

    [ContextMenu("Stop Analysis")]
    public void StopAnalysis()
    {
        if (!isAnalyzing) return;

        isAnalyzing = false;
        if (_analysisCoroutine != null)
        {
            StopCoroutine(_analysisCoroutine);
            _analysisCoroutine = null;
        }
        
        lock (_audioBuffer)
        {
            _audioBuffer.Clear();
        }

        Log("Audio stream analysis stopped.");
    }

    private IEnumerator AnalysisLoopCoroutine()
    {
        while (isAnalyzing)
        {
            yield return new WaitForSeconds(analysisInterval);

            float[] audioChunk;
            int channels;
            int frequency;

            lock (_audioBuffer)
            {
                if (_audioBuffer.Count == 0) continue;
                audioChunk = _audioBuffer.ToArray();
                channels = _bufferChannels;
                frequency = _bufferSampleRate;
                _audioBuffer.Clear();
            }

            float currentVolume = GetRootMeanSquare(audioChunk);
            if (currentVolume < audioGainThreshold)
            {
                continue;
            }

            Log($"Volume threshold met ({currentVolume:F4}). Analyzing audio chunk of {audioChunk.Length} samples.");
            yield return StartCoroutine(AnalyzeAudioSegment(audioChunk, frequency, channels, ProcessEmotionResult));
        }
    }

    private void ProcessEmotionResult(EmotionResult result)
    {
        if (result == null)
        {
            Log("Analysis did not return a valid emotion.", LogType.Warning);
            return;
        }

        result.timestamp = Time.time;
        Log($"Emotion Detected: {result.emotion} (Confidence: {result.confidence:F2})");
        
        // Invoke local event
        OnEmotionDetected?.Invoke(result);
        
        // Offload to statistics manager
        if (EmotionStatisticsManager.Instance != null)
        {
            EmotionStatisticsManager.Instance.RecordEmotion(result);
        }
        else
        {
            Log("EmotionStatisticsManager not found. Statistics will not be recorded.", LogType.Error);
        }

        // Handle animation and state logic
        UpdateEmotionTracking(result);
        ApplyEmotionToBlendshapes(result);
        ApplyEmotionToBody(result);
    }

    #region Initialization and Teardown
    private void InitializeBlendshapeMapping()
    {
        if (targetRenderer == null || targetRenderer.sharedMesh == null) return;
        for (int i = 0; i < targetRenderer.sharedMesh.blendShapeCount; i++)
        {
            blendshapeIndices[targetRenderer.sharedMesh.GetBlendShapeName(i)] = i;
        }
        Log($"Initialized {blendshapeIndices.Count} blendshapes.");
    }

    private void InitializeBodyAnimation()
    {
        Transform[] bodyParts = { headTransform, spineTransform, leftShoulderTransform, rightShoulderTransform, leftArmTransform, rightArmTransform };
        foreach (var part in bodyParts)
        {
            if (part != null)
            {
                originalPositions[part] = part.localPosition;
                originalRotations[part] = part.localRotation;
            }
        }
        Log("Body animation transforms initialized.");
    }
    
    private void OnDestroy()
    {
        StopAnalysis();
    }
    #endregion

    #region Emotion Logic and Application
    private void UpdateEmotionTracking(EmotionResult result)
    {
        // Normalize emotion name before comparison
        string normalizedEmotion = EmotionStatisticsManager.Instance != null ?
                                   EmotionStatisticsManager.Instance.NormalizeEmotion(result.emotion) :
                                   result.emotion.ToLower();


        if (normalizedEmotion == emotionState.currentEmotion)
        {
            emotionState.consecutiveCount++;
            emotionState.intensity = Mathf.Max(emotionState.intensity, result.confidence);
        }
        else
        {
            emotionState.currentEmotion = normalizedEmotion;
            emotionState.consecutiveCount = 1;
            emotionState.intensity = result.confidence;
            emotionState.duration = 0f;
            emotionState.isPersistent = false;
        }

        if (enablePersistentEmotions && emotionState.consecutiveCount >= consecutiveThreshold && !emotionState.isPersistent)
        {
            emotionState.isPersistent = true;
            emotionState.duration = 0f;
            Log($"Emotion '{result.emotion}' is now persistent.");
            OnEmotionStateChanged?.Invoke(emotionState);
        }
    }

    private void UpdateEmotionState()
    {
        if (emotionState.isPersistent)
        {
            emotionState.duration += Time.deltaTime;
            if (emotionState.duration >= persistentEmotionDuration)
            {
                Log($"Persistent emotion '{emotionState.currentEmotion}' has faded.");
                emotionState.isPersistent = false;
                emotionState.currentEmotion = "neutral";
                emotionState.intensity = 0f;
                emotionState.consecutiveCount = 0;
                ResetToNeutral();
                OnEmotionStateChanged?.Invoke(emotionState);
            }
        }
    }
    
    [ContextMenu("Reset to Neutral")]
    public void ResetToNeutral()
    {
        targetBlendshapes = new BlendshapeWeights();
        ApplyNeutralBody();
        Log("Resetting character to neutral state.");
    }
    
    private void ApplyEmotionToBlendshapes(EmotionResult emotion)
    {
        if (!enableBlendshapeAnimation || targetRenderer == null) return;
        
        targetBlendshapes = new BlendshapeWeights();
        
        float baseIntensity = emotion.confidence * emotionIntensityMultiplier;
        float intensity = emotionState.isPersistent ? baseIntensity * persistentIntensityMultiplier : baseIntensity;
        
        intensity = Mathf.Clamp(intensity, 0f, 1.5f);

        // Use the already normalized emotion from the state
        switch (emotionState.currentEmotion)
        {
            case "happy": ApplyHappyExpression(intensity); break;
            case "sad": ApplySadExpression(intensity); break;
            case "angry": ApplyAngryExpression(intensity); break;
            case "fear": ApplyFearExpression(intensity); break;
            case "surprise": ApplySurpriseExpression(intensity); break;
            case "disgust": ApplyDisgustExpression(intensity); break;
            default: break; // Neutral is default (all zero)
        }
        
        OnBlendshapeUpdate?.Invoke(targetBlendshapes);
    }
    
    // Expression methods (ApplyHappyExpression, ApplySadExpression, etc.) remain the same
    private void ApplyHappyExpression(float i) { targetBlendshapes.mouthSmileLeft = i; targetBlendshapes.mouthSmileRight = i; targetBlendshapes.cheekSquintLeft = i * 0.9f; targetBlendshapes.cheekSquintRight = i * 0.9f; targetBlendshapes.eyeSquintLeft = i * 0.6f; targetBlendshapes.eyeSquintRight = i * 0.6f; targetBlendshapes.browOuterUpLeft = i * 0.4f; targetBlendshapes.browOuterUpRight = i * 0.4f; }
    private void ApplySadExpression(float i) { targetBlendshapes.mouthFrownLeft = i; targetBlendshapes.mouthFrownRight = i; targetBlendshapes.browInnerUp = i; targetBlendshapes.mouthLowerDownLeft = i * 0.5f; targetBlendshapes.mouthLowerDownRight = i * 0.5f; }
    private void ApplyAngryExpression(float i) { targetBlendshapes.browDownLeft = i; targetBlendshapes.browDownRight = i; targetBlendshapes.eyeSquintLeft = i; targetBlendshapes.eyeSquintRight = i; targetBlendshapes.mouthFrownLeft = i * 0.7f; targetBlendshapes.mouthFrownRight = i * 0.7f; targetBlendshapes.noseSneerLeft = i * 0.8f; targetBlendshapes.noseSneerRight = i * 0.8f; }
    private void ApplyFearExpression(float i) { targetBlendshapes.eyeWideLeft = i; targetBlendshapes.eyeWideRight = i; targetBlendshapes.browInnerUp = i; targetBlendshapes.browOuterUpLeft = i * 0.9f; targetBlendshapes.browOuterUpRight = i * 0.9f; targetBlendshapes.mouthStretchLeft = i * 0.8f; targetBlendshapes.mouthStretchRight = i * 0.8f; targetBlendshapes.jawOpen = i * 0.5f; }
    private void ApplySurpriseExpression(float i) { targetBlendshapes.eyeWideLeft = i; targetBlendshapes.eyeWideRight = i; targetBlendshapes.browInnerUp = i; targetBlendshapes.jawOpen = i; }
    private void ApplyDisgustExpression(float i) { targetBlendshapes.noseSneerLeft = i; targetBlendshapes.noseSneerRight = i; targetBlendshapes.mouthFrownLeft = i; targetBlendshapes.mouthFrownRight = i; targetBlendshapes.eyeSquintLeft = i * 0.7f; targetBlendshapes.eyeSquintRight = i * 0.7f; }

    private void ApplyEmotionToBody(EmotionResult emotion)
    {
        if (!enableBodyAnimation) return;
        
        float baseIntensity = emotion.confidence * bodyAnimationIntensity;
        float intensity = emotionState.isPersistent ? baseIntensity * persistentIntensityMultiplier : baseIntensity;
        
        targetBodyAnimation = new BodyAnimation();

        // Use the already normalized emotion from the state
        switch (emotionState.currentEmotion)
        {
            case "happy": ApplyHappyBody(intensity); break;
            case "sad": ApplySadBody(intensity); break;
            case "angry": ApplyAngryBody(intensity); break;
            case "fear": ApplyFearBody(intensity); break;
            case "surprise": ApplySurpriseBody(intensity); break;
            case "disgust": ApplyDisgustBody(intensity); break;
            default: ApplyNeutralBody(); break;
        }
    }

    // Body animation methods remain the same
    private void ApplyHappyBody(float i) => targetBodyAnimation.headRotation = new Vector3(-5f * i, 0, 0);
    private void ApplySadBody(float i) { targetBodyAnimation.headRotation = new Vector3(15f * i, 0, 0); targetBodyAnimation.spineRotation = new Vector3(8f * i, 0, 0); }
    private void ApplyAngryBody(float i) { targetBodyAnimation.headRotation = new Vector3(-8f * i, 0, 0); targetBodyAnimation.spineRotation = new Vector3(-5f * i, 0, 0); }
    private void ApplyFearBody(float i) { targetBodyAnimation.headRotation = new Vector3(10f * i, 0, 0); targetBodyAnimation.bodyPosition = new Vector3(0, -0.02f * i, 0.02f * i); }
    private void ApplySurpriseBody(float i) => targetBodyAnimation.headRotation = new Vector3(-10f * i, 0, 0);
    private void ApplyDisgustBody(float i) => targetBodyAnimation.headRotation = new Vector3(5f * i, -10f * i, 5f * i);
    private void ApplyNeutralBody() => targetBodyAnimation = new BodyAnimation();

    #endregion

    #region Animation Updates
    // LerpBlendshapes, ApplyBlendshapesToRenderer, SetBlendshapeWeight, UpdateBlendshapeAnimation, UpdateBodyAnimation, ApplyBodyTransforms
    // These methods remain unchanged as they are specific to the animation logic of this component.
    private void UpdateBlendshapeAnimation() { currentBlendshapes = LerpBlendshapes(currentBlendshapes, targetBlendshapes, blendshapeTransitionSpeed * Time.deltaTime); ApplyBlendshapesToRenderer(currentBlendshapes); }
    private void UpdateBodyAnimation() { float s = bodyAnimationSpeed * Time.deltaTime; currentBodyAnimation.headRotation = Vector3.Lerp(currentBodyAnimation.headRotation, targetBodyAnimation.headRotation, s); currentBodyAnimation.spineRotation = Vector3.Lerp(currentBodyAnimation.spineRotation, targetBodyAnimation.spineRotation, s); currentBodyAnimation.leftShoulderRotation = Vector3.Lerp(currentBodyAnimation.leftShoulderRotation, targetBodyAnimation.leftShoulderRotation, s); currentBodyAnimation.rightShoulderRotation = Vector3.Lerp(currentBodyAnimation.rightShoulderRotation, targetBodyAnimation.rightShoulderRotation, s); currentBodyAnimation.leftArmRotation = Vector3.Lerp(currentBodyAnimation.leftArmRotation, targetBodyAnimation.leftArmRotation, s); currentBodyAnimation.rightArmRotation = Vector3.Lerp(currentBodyAnimation.rightArmRotation, targetBodyAnimation.rightArmRotation, s); currentBodyAnimation.bodyPosition = Vector3.Lerp(currentBodyAnimation.bodyPosition, targetBodyAnimation.bodyPosition, s); ApplyBodyTransforms(); }
    private BlendshapeWeights LerpBlendshapes(BlendshapeWeights f, BlendshapeWeights t, float time) { var r = new BlendshapeWeights(); var fields = typeof(BlendshapeWeights).GetFields(); for (int i = 0; i < fields.Length; i++) { fields[i].SetValue(r, Mathf.Lerp((float)fields[i].GetValue(f), (float)fields[i].GetValue(t), time)); } return r; }
    private void ApplyBlendshapesToRenderer(BlendshapeWeights w) { foreach (var f in w.GetType().GetFields()) SetBlendshapeWeight(f.Name, (float)f.GetValue(w)); }
    private void SetBlendshapeWeight(string n, float w) { if (blendshapeIndices.TryGetValue(n, out int i)) targetRenderer.SetBlendShapeWeight(i, w * 100f); }
    private void ApplyBodyTransforms() { if (headTransform) headTransform.localRotation = originalRotations[headTransform] * Quaternion.Euler(currentBodyAnimation.headRotation); if (spineTransform) { spineTransform.localRotation = originalRotations[spineTransform] * Quaternion.Euler(currentBodyAnimation.spineRotation); spineTransform.localPosition = originalPositions[spineTransform] + currentBodyAnimation.bodyPosition; } if (leftShoulderTransform) leftShoulderTransform.localRotation = originalRotations[leftShoulderTransform] * Quaternion.Euler(currentBodyAnimation.leftShoulderRotation); if (rightShoulderTransform) rightShoulderTransform.localRotation = originalRotations[rightShoulderTransform] * Quaternion.Euler(currentBodyAnimation.rightShoulderRotation); if (leftArmTransform) leftArmTransform.localRotation = originalRotations[leftArmTransform] * Quaternion.Euler(currentBodyAnimation.leftArmRotation); if (rightArmTransform) rightArmTransform.localRotation = originalRotations[rightArmTransform] * Quaternion.Euler(currentBodyAnimation.rightArmRotation); }
    #endregion

    #region Server Communication & Data Conversion
    // These methods (AnalyzeAudioSegment, SendAudioToServer, ConvertToWav, GetRootMeanSquare) remain unchanged.
    private IEnumerator AnalyzeAudioSegment(float[] audioData, int frequency, int channels, Action<EmotionResult> callback) { byte[] wavData = ConvertToWav(audioData, frequency, channels); yield return StartCoroutine(SendAudioToServer(wavData, callback)); }
    private float GetRootMeanSquare(float[] s) { float sum = 0; for (int i = 0; i < s.Length; i++) sum += s[i] * s[i]; return Mathf.Sqrt(sum / s.Length); }
    private IEnumerator SendAudioToServer(byte[] wavData, Action<EmotionResult> callback) { string url = serverUrl + analyzeEndpoint; Log($"Sending {wavData.Length} bytes to {url}"); WWWForm form = new WWWForm(); form.AddBinaryData("audio", wavData, "audio.wav", "audio/wav"); using (UnityWebRequest req = UnityWebRequest.Post(url, form)) { yield return req.SendWebRequest(); if (req.result == UnityWebRequest.Result.Success) { try { EmotionResult res = JsonUtility.FromJson<EmotionResult>(req.downloadHandler.text); callback?.Invoke(res); } catch (Exception e) { Log($"Error parsing server response: {e.Message}", LogType.Error); callback?.Invoke(null); } } else { Log($"Server request failed: {req.error}\nResponse: {req.downloadHandler.text}", LogType.Error); callback?.Invoke(null); } } }
    private byte[] ConvertToWav(float[] d, int f, int c) { using (var s = new MemoryStream()) using (var w = new BinaryWriter(s)) { int sc = d.Length; int br = f * c * 2; w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + sc * 2); w.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)c); w.Write(f); w.Write(br); w.Write((short)(c * 2)); w.Write((short)16); w.Write(Encoding.ASCII.GetBytes("data")); w.Write(sc * 2); for (int i = 0; i < sc; i++) w.Write((short)(d[i] * 32767f)); return s.ToArray(); } }
    #endregion

    #region Utility
    private void Log(string message, LogType logType = LogType.Log)
    {
        if (!debugMode) return;
        string prefix = "[AudioEmotionRecognizer] ";
        switch (logType)
        {
            case LogType.Warning: Debug.LogWarning(prefix + message); break;
            case LogType.Error: Debug.LogError(prefix + message); break;
            default: Debug.Log(prefix + message); break;
        }
    }
    #endregion
}
