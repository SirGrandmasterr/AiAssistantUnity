using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Data structures for emotion analysis, blendshapes, and body animation remain the same.
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
    public float eyeBlinkLeft = 0f;
    public float eyeBlinkRight = 0f;
    public float eyesLookUp = 0f;
    public float eyesLookDown = 0f;
    public float eyeSquintLeft = 0f;
    public float eyeSquintRight = 0f;
    public float eyeWideLeft = 0f;
    public float eyeWideRight = 0f;

    // Eyebrow expressions
    public float browDownLeft = 0f;
    public float browDownRight = 0f;
    public float browInnerUp = 0f;
    public float browOuterUpLeft = 0f;
    public float browOuterUpRight = 0f;

    // Mouth expressions
    public float mouthFrownLeft = 0f;
    public float mouthFrownRight = 0f;
    public float mouthSmileLeft = 0f;
    public float mouthSmileRight = 0f;
    public float mouthPucker = 0f;
    public float mouthFunnel = 0f;
    public float mouthDimpleLeft = 0f;
    public float mouthDimpleRight = 0f;
    public float mouthStretchLeft = 0f;
    public float mouthStretchRight = 0f;
    public float mouthRollLower = 0f;
    public float mouthRollUpper = 0f;
    public float mouthShrugLower = 0f;
    public float mouthShrugUpper = 0f;
    public float mouthPressLeft = 0f;
    public float mouthPressRight = 0f;
    public float mouthUpperUpLeft = 0f;
    public float mouthUpperUpRight = 0f;
    public float mouthLowerDownLeft = 0f;
    public float mouthLowerDownRight = 0f;
    public float mouthLeft = 0f;
    public float mouthRight = 0f;

    // Cheek expressions
    public float cheekPuff = 0f;
    public float cheekSquintLeft = 0f;
    public float cheekSquintRight = 0f;

    // Nose expressions
    public float noseSneerLeft = 0f;
    public float noseSneerRight = 0f;

    // Jaw expressions
    public float jawForward = 0f;
    public float jawLeft = 0f;
    public float jawRight = 0f;
    public float jawOpen = 0f;

    // Tongue
    public float tongueOut = 0f;
}

[System.Serializable]
public class BodyAnimation
{
    public Vector3 headRotation = Vector3.zero;
    public Vector3 spineRotation = Vector3.zero;
    public Vector3 leftShoulderRotation = Vector3.zero;
    public Vector3 rightShoulderRotation = Vector3.zero;
    public Vector3 leftArmRotation = Vector3.zero;
    public Vector3 rightArmRotation = Vector3.zero;
    public Vector3 bodyPosition = Vector3.zero;
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
/// Analyzes a real-time audio stream from an AudioSource for emotional content
/// and translates it into character blendshape and body animations.
/// </summary>
public class AudioEmotionRecognizer : MonoBehaviour
{
    [Header("Input Settings")]
    [Tooltip("The AudioSource that provides the real-time audio stream (e.g., from WebRTC).")]
    [SerializeField] public AudioSource streamingAudioSource;
    [Tooltip("Start analysis automatically when the scene loads.")]
    [SerializeField] private bool analyzeOnStart = true;

    [Header("Analysis Settings")]
    [Tooltip("The frequency at which to sample the audio stream for analysis.")]
    [SerializeField] private float analysisInterval = 1.0f;
    [Tooltip("The volume threshold required to trigger an analysis.")]
    [SerializeField] private float audioGainThreshold = 0.01f;
    [Tooltip("The size of the sample window for FFT analysis.")]
    [SerializeField] private int sampleWindowSize = 1024;

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
    [SerializeField] private Transform headTransform;
    [SerializeField] private Transform spineTransform;
    [SerializeField] private Transform leftShoulderTransform;
    [SerializeField] private Transform rightShoulderTransform;
    [SerializeField] private Transform leftArmTransform;
    [SerializeField] private Transform rightArmTransform;
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

    // Internal state
    public bool isAnalyzing = false;
    private Coroutine analysisCoroutine;
    private float[] audioSamples;

    private BlendshapeWeights currentBlendshapes = new BlendshapeWeights();
    private BlendshapeWeights targetBlendshapes = new BlendshapeWeights();
    private BodyAnimation currentBodyAnimation = new BodyAnimation();
    private BodyAnimation targetBodyAnimation = new BodyAnimation();
    private Dictionary<string, int> blendshapeIndices = new Dictionary<string, int>();

    private Dictionary<Transform, Vector3> originalPositions = new Dictionary<Transform, Vector3>();
    private Dictionary<Transform, Quaternion> originalRotations = new Dictionary<Transform, Quaternion>();

    public EmotionState emotionState = new EmotionState();
    private List<EmotionResult> recentEmotions = new List<EmotionResult>();


    private void Start()
    {
        Log("=== AUDIO EMOTION RECOGNIZER (STREAMING) START ===");
        if (streamingAudioSource == null)
        {
            Log("No AudioSource assigned. Please assign a streaming AudioSource in the inspector.", LogType.Error);
            this.enabled = false;
            return;
        }

        audioSamples = new float[sampleWindowSize];
        InitializeBlendshapeMapping();
        InitializeBodyAnimation();

        if (analyzeOnStart)
        {
            StartAnalysis();
        }
    }

    private void Update()
    {
        if (!streamingAudioSource)
        {
            Debug.Log("Problem wth AudioSource");
        }
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
    /// Starts the real-time emotion analysis from the assigned AudioSource.
    /// </summary>
    [ContextMenu("Start Analysis")]
    public void StartAnalysis()
    {
        if (isAnalyzing)
        {
            Log("Analysis is already running.", LogType.Warning);
            return;
        }

        if (streamingAudioSource == null)
        {
            Log("Cannot start analysis: AudioSource is not assigned.", LogType.Error);
            return;
        }

        isAnalyzing = true;
        analysisCoroutine = StartCoroutine(StreamAnalysisCoroutine());
        Log("Audio stream analysis started.");
    }

    /// <summary>
    /// Stops the real-time emotion analysis.
    /// </summary>
    [ContextMenu("Stop Analysis")]
    public void StopAnalysis()
    {
        if (!isAnalyzing) return;

        isAnalyzing = false;
        if (analysisCoroutine != null)
        {
            StopCoroutine(analysisCoroutine);
            analysisCoroutine = null;
        }
        Log("Audio stream analysis stopped.");
    }

    private IEnumerator StreamAnalysisCoroutine()
    {
        while (isAnalyzing)
        {
            yield return new WaitForSeconds(analysisInterval);

            if (streamingAudioSource == null || !streamingAudioSource.isPlaying)
            {
                Log("AudioSource is not playing or is null. Skipping analysis.", LogType.Warning);
                continue;
            }

            streamingAudioSource.GetOutputData(audioSamples, 0);

            float currentVolume = GetRootMeanSquare(audioSamples);
            if (currentVolume < audioGainThreshold)
            {
                Log($"Current volume ({currentVolume:F4}) is below threshold ({audioGainThreshold:F4}). Skipping.");
                continue;
            }

            // --- FIX STARTS HERE ---
            // Get frequency and channels safely.
            int frequency;
            int channels;

            if (streamingAudioSource.clip != null)
            {
                // If a clip IS assigned, its properties are more specific and should be preferred.
                frequency = streamingAudioSource.clip.frequency;
                channels = streamingAudioSource.clip.channels;
                Log($"Using AudioSource clip properties: {frequency}Hz, {channels} channels.");
            }
            else
            {
                // If no clip is assigned (common for streaming), use system-wide audio settings.
                frequency = AudioSettings.outputSampleRate;
                // Get the channel count from the current speaker mode.
                channels = (int)AudioSettings.speakerMode;
                Log($"AudioSource has no clip. Using system audio settings: {frequency}Hz, {channels} channels.", LogType.Warning);
            }
            // --- FIX ENDS HERE ---

            Log($"Volume threshold met ({currentVolume:F4}). Analyzing audio chunk.");
            yield return StartCoroutine(AnalyzeAudioSegment(audioSamples, frequency, channels, (result) =>
            {
                if (result != null)
                {
                    result.timestamp = Time.time;
                    Log($"Emotion Detected: {result.emotion} (Confidence: {result.confidence:F2})");
                    OnEmotionDetected?.Invoke(result);
                    ProcessEmotionResult(result);
                }
                else
                {
                    Log("Analysis did not return a valid emotion.", LogType.Warning);
                }
            }));
        }
    }

    private float GetRootMeanSquare(float[] samples)
    {
        float sum = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i] * samples[i]; // sum of squares
        }
        return Mathf.Sqrt(sum / samples.Length); // root of the mean
    }

    private IEnumerator AnalyzeAudioSegment(float[] audioData, int frequency, int channels, Action<EmotionResult> callback)
    {
        byte[] wavData = ConvertToWav(audioData, frequency, channels);
        yield return StartCoroutine(SendAudioToServer(wavData, callback));
    }

    private void ProcessEmotionResult(EmotionResult result)
    {
        Log($"Processing emotion: {result.emotion} with confidence {result.confidence}");
        recentEmotions.Add(result);
        if (recentEmotions.Count > 10)
        {
            recentEmotions.RemoveAt(0);
        }
        UpdateEmotionTracking(result);
        ApplyEmotionToBlendshapes(result);
        ApplyEmotionToBody(result);
    }

    #region Initialization
    private void InitializeBlendshapeMapping()
    {
        if (targetRenderer == null) return;
        Mesh sharedMesh = targetRenderer.sharedMesh;
        if (sharedMesh == null) return;
        for (int i = 0; i < sharedMesh.blendShapeCount; i++)
        {
            string shapeName = sharedMesh.GetBlendShapeName(i);
            blendshapeIndices[shapeName] = i;
        }
        Log($"Initialized {blendshapeIndices.Count} blendshapes.");
    }

    private void InitializeBodyAnimation()
    {
        Transform[] bodyParts = { headTransform, spineTransform, leftShoulderTransform,
                                rightShoulderTransform, leftArmTransform, rightArmTransform };
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
    #endregion

    #region Emotion Logic and Application
    private void UpdateEmotionTracking(EmotionResult result)
    {
        if (result.emotion == emotionState.currentEmotion)
        {
            emotionState.consecutiveCount++;
            emotionState.intensity = Mathf.Max(emotionState.intensity, result.confidence);
        }
        else
        {
            emotionState.currentEmotion = result.emotion;
            emotionState.consecutiveCount = 1;
            emotionState.intensity = result.confidence;
            emotionState.duration = 0f;
            emotionState.isPersistent = false;
        }

        if (enablePersistentEmotions && emotionState.consecutiveCount >= consecutiveThreshold && !emotionState.isPersistent)
        {
            emotionState.isPersistent = true;
            emotionState.duration = 0f;
            Log($"Emotion '{result.emotion}' is now persistent.", LogType.Log);
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
        
        switch (emotion.emotion.ToLower())
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
    
    private void ApplyHappyExpression(float intensity)
    {
        targetBlendshapes.mouthSmileLeft = intensity;
        targetBlendshapes.mouthSmileRight = intensity;
        targetBlendshapes.cheekSquintLeft = intensity * 0.9f;
        targetBlendshapes.cheekSquintRight = intensity * 0.9f;
        targetBlendshapes.eyeSquintLeft = intensity * 0.6f;
        targetBlendshapes.eyeSquintRight = intensity * 0.6f;
        targetBlendshapes.browOuterUpLeft = intensity * 0.4f;
        targetBlendshapes.browOuterUpRight = intensity * 0.4f;
    }
    
    private void ApplySadExpression(float intensity)
    {
        targetBlendshapes.mouthFrownLeft = intensity;
        targetBlendshapes.mouthFrownRight = intensity;
        targetBlendshapes.browInnerUp = intensity;
        targetBlendshapes.mouthLowerDownLeft = intensity * 0.5f;
        targetBlendshapes.mouthLowerDownRight = intensity * 0.5f;
    }

    private void ApplyAngryExpression(float intensity)
    {
        targetBlendshapes.browDownLeft = intensity;
        targetBlendshapes.browDownRight = intensity;
        targetBlendshapes.eyeSquintLeft = intensity;
        targetBlendshapes.eyeSquintRight = intensity;
        targetBlendshapes.mouthFrownLeft = intensity * 0.7f;
        targetBlendshapes.mouthFrownRight = intensity * 0.7f;
        targetBlendshapes.noseSneerLeft = intensity * 0.8f;
        targetBlendshapes.noseSneerRight = intensity * 0.8f;
    }
    
    private void ApplyFearExpression(float intensity)
    {
        targetBlendshapes.eyeWideLeft = intensity;
        targetBlendshapes.eyeWideRight = intensity;
        targetBlendshapes.browInnerUp = intensity;
        targetBlendshapes.browOuterUpLeft = intensity * 0.9f;
        targetBlendshapes.browOuterUpRight = intensity * 0.9f;
        targetBlendshapes.mouthStretchLeft = intensity * 0.8f;
        targetBlendshapes.mouthStretchRight = intensity * 0.8f;
        targetBlendshapes.jawOpen = intensity * 0.5f;
    }
    
    private void ApplySurpriseExpression(float intensity)
    {
        targetBlendshapes.eyeWideLeft = intensity;
        targetBlendshapes.eyeWideRight = intensity;
        targetBlendshapes.browInnerUp = intensity;
        targetBlendshapes.jawOpen = intensity;
    }

    private void ApplyDisgustExpression(float intensity)
    {
        targetBlendshapes.noseSneerLeft = intensity;
        targetBlendshapes.noseSneerRight = intensity;
        targetBlendshapes.mouthFrownLeft = intensity;
        targetBlendshapes.mouthFrownRight = intensity;
        targetBlendshapes.eyeSquintLeft = intensity * 0.7f;
        targetBlendshapes.eyeSquintRight = intensity * 0.7f;
    }

    private void ApplyEmotionToBody(EmotionResult emotion)
    {
        if (!enableBodyAnimation) return;
        
        float baseIntensity = emotion.confidence * bodyAnimationIntensity;
        float intensity = emotionState.isPersistent ? baseIntensity * persistentIntensityMultiplier : baseIntensity;
        
        targetBodyAnimation = new BodyAnimation();
        
        switch (emotion.emotion.ToLower())
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

    private void ApplyHappyBody(float i) => targetBodyAnimation.headRotation = new Vector3(-5f * i, 0, 0);
    private void ApplySadBody(float i) { targetBodyAnimation.headRotation = new Vector3(15f * i, 0, 0); targetBodyAnimation.spineRotation = new Vector3(8f * i, 0, 0); }
    private void ApplyAngryBody(float i) { targetBodyAnimation.headRotation = new Vector3(-8f * i, 0, 0); targetBodyAnimation.spineRotation = new Vector3(-5f * i, 0, 0); }
    private void ApplyFearBody(float i) { targetBodyAnimation.headRotation = new Vector3(10f * i, 0, 0); targetBodyAnimation.bodyPosition = new Vector3(0, -0.02f * i, 0.02f * i); }
    private void ApplySurpriseBody(float i) => targetBodyAnimation.headRotation = new Vector3(-10f * i, 0, 0);
    private void ApplyDisgustBody(float i) => targetBodyAnimation.headRotation = new Vector3(5f * i, -10f * i, 5f * i);
    private void ApplyNeutralBody() => targetBodyAnimation = new BodyAnimation();

    #endregion

    #region Animation Updates
    private void UpdateBlendshapeAnimation()
    {
        currentBlendshapes = LerpBlendshapes(currentBlendshapes, targetBlendshapes, blendshapeTransitionSpeed * Time.deltaTime);
        ApplyBlendshapesToRenderer(currentBlendshapes);
    }
    
    private void UpdateBodyAnimation()
    {
        float speed = bodyAnimationSpeed * Time.deltaTime;
        currentBodyAnimation.headRotation = Vector3.Lerp(currentBodyAnimation.headRotation, targetBodyAnimation.headRotation, speed);
        currentBodyAnimation.spineRotation = Vector3.Lerp(currentBodyAnimation.spineRotation, targetBodyAnimation.spineRotation, speed);
        currentBodyAnimation.leftShoulderRotation = Vector3.Lerp(currentBodyAnimation.leftShoulderRotation, targetBodyAnimation.leftShoulderRotation, speed);
        currentBodyAnimation.rightShoulderRotation = Vector3.Lerp(currentBodyAnimation.rightShoulderRotation, targetBodyAnimation.rightShoulderRotation, speed);
        currentBodyAnimation.leftArmRotation = Vector3.Lerp(currentBodyAnimation.leftArmRotation, targetBodyAnimation.leftArmRotation, speed);
        currentBodyAnimation.rightArmRotation = Vector3.Lerp(currentBodyAnimation.rightArmRotation, targetBodyAnimation.rightArmRotation, speed);
        currentBodyAnimation.bodyPosition = Vector3.Lerp(currentBodyAnimation.bodyPosition, targetBodyAnimation.bodyPosition, speed);
        ApplyBodyTransforms();
    }
    
    private BlendshapeWeights LerpBlendshapes(BlendshapeWeights from, BlendshapeWeights to, float t)
    {
        BlendshapeWeights result = new BlendshapeWeights();
        var fromFields = from.GetType().GetFields();
        var toFields = to.GetType().GetFields();
        for (int i = 0; i < fromFields.Length; i++)
        {
            float fromVal = (float)fromFields[i].GetValue(from);
            float toVal = (float)toFields[i].GetValue(to);
            fromFields[i].SetValue(result, Mathf.Lerp(fromVal, toVal, t));
        }
        return result;
    }

    private void ApplyBlendshapesToRenderer(BlendshapeWeights weights)
    {
        foreach (var field in weights.GetType().GetFields())
        {
            SetBlendshapeWeight(field.Name, (float)field.GetValue(weights));
        }
    }

    private void SetBlendshapeWeight(string shapeName, float weight)
    {
        if (blendshapeIndices.TryGetValue(shapeName, out int index))
        {
            targetRenderer.SetBlendShapeWeight(index, weight * 100f);
        }
    }

    private void ApplyBodyTransforms()
    {
        if (headTransform != null) headTransform.localRotation = originalRotations[headTransform] * Quaternion.Euler(currentBodyAnimation.headRotation);
        if (spineTransform != null) { spineTransform.localRotation = originalRotations[spineTransform] * Quaternion.Euler(currentBodyAnimation.spineRotation); spineTransform.localPosition = originalPositions[spineTransform] + currentBodyAnimation.bodyPosition; }
        if (leftShoulderTransform != null) leftShoulderTransform.localRotation = originalRotations[leftShoulderTransform] * Quaternion.Euler(currentBodyAnimation.leftShoulderRotation);
        if (rightShoulderTransform != null) rightShoulderTransform.localRotation = originalRotations[rightShoulderTransform] * Quaternion.Euler(currentBodyAnimation.rightShoulderRotation);
        if (leftArmTransform != null) leftArmTransform.localRotation = originalRotations[leftArmTransform] * Quaternion.Euler(currentBodyAnimation.leftArmRotation);
        if (rightArmTransform != null) rightArmTransform.localRotation = originalRotations[rightArmTransform] * Quaternion.Euler(currentBodyAnimation.rightArmRotation);
    }
    #endregion

    #region Server Communication & Data Conversion
    private IEnumerator SendAudioToServer(byte[] wavData, Action<EmotionResult> callback)
    {
        string url = serverUrl + analyzeEndpoint;
        Log($"Sending {wavData.Length} bytes of audio data to {url}");

        WWWForm form = new WWWForm();
        form.AddBinaryData("audio", wavData, "audio.wav", "audio/wav");

        using (UnityWebRequest request = UnityWebRequest.Post(url, form))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string jsonResponse = request.downloadHandler.text;
                    Log($"Server Response: {jsonResponse}");
                    EmotionResult result = JsonUtility.FromJson<EmotionResult>(jsonResponse);
                    callback?.Invoke(result);
                }
                catch (Exception e)
                {
                    Log($"Error parsing server response: {e.Message}", LogType.Error);
                    callback?.Invoke(null);
                }
            }
            else
            {
                Log($"Server request failed: {request.error}", LogType.Error);
                Log($"Response: {request.downloadHandler.text}");
                callback?.Invoke(null);
            }
        }
    }

    private byte[] ConvertToWav(float[] audioData, int frequency, int channels)
    {
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            int sampleCount = audioData.Length;
            int byteRate = frequency * channels * 2;
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + sampleCount * 2);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(frequency);
            writer.Write(byteRate);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(sampleCount * 2);

            for (int i = 0; i < sampleCount; i++)
            {
                short sample = (short)(audioData[i] * 32767f);
                writer.Write(sample);
            }
            return stream.ToArray();
        }
    }
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

    private void OnDestroy()
    {
        StopAnalysis();
    }
    #endregion
}
