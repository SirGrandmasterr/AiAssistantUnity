using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections;

// Data structures for emotion history and statistics.
// It's good practice to keep them in the same file as the manager
// if they are primarily used by it.

[System.Serializable]
public class EmotionHistoryEntry
{
    public string emotion;
    public float confidence;
    public float timestamp;
    public float sessionTime; // Time since session started
    public int sequenceId; // Unique ID for this detection
}

[System.Serializable]
public class EmotionStatistics
{
    public Dictionary<string, int> emotionCounts = new Dictionary<string, int>();
    public Dictionary<string, float> totalConfidence = new Dictionary<string, float>();
    public Dictionary<string, float> averageConfidence = new Dictionary<string, float>();
    public Dictionary<string, float> maxConfidence = new Dictionary<string, float>();
    public Dictionary<string, float> totalDuration = new Dictionary<string, float>();
    public int totalDetections = 0;
    public float sessionDuration = 0f;
    public string dominantEmotion = "";
    public float dominantEmotionPercentage = 0f;
}

[System.Serializable]
public class ConversationEmotionSummary
{
    public float sessionStartTime;
    public float sessionEndTime;
    public int totalEmotions;
    public string primaryEmotion;
    public float primaryEmotionConfidence;
    public List<string> emotionProgression;
    public Dictionary<string, float> emotionPercentages;
}


/// <summary>
/// A persistent singleton that manages emotion statistics and history across scene changes.
/// </summary>
public class EmotionStatisticsManager : MonoBehaviour
{
    public static EmotionStatisticsManager Instance { get; private set; }

    [Header("History Settings")]
    [Tooltip("The maximum number of emotion entries to keep in history.")]
    [SerializeField] private int maxHistoryEntries = 1000;
    [Tooltip("If true, logs detailed information for each emotion recorded.")]
    [SerializeField] private bool logDetailedHistory = true;

    [Header("Display Settings")]
    [Tooltip("The interval in seconds to automatically log the statistics to the console.")]
    [SerializeField] private float historyDisplayInterval = 10.0f;
    [Tooltip("Enable or disable the automatic logging of statistics.")]
    [SerializeField] private bool enableAutomaticLogging = true;

    // Public events for UI or other systems to subscribe to
    public event Action<EmotionHistoryEntry> OnEmotionHistoryUpdate;
    public event Action<EmotionStatistics> OnEmotionStatisticsUpdate;

    // Private fields for managing state
    private List<EmotionHistoryEntry> emotionHistory = new List<EmotionHistoryEntry>();
    private EmotionStatistics currentStatistics = new EmotionStatistics();
    private float sessionStartTime;
    private int emotionSequenceId = 0;
    private Coroutine historyDisplayCoroutine;

    // Supported emotions for consistent tracking
    private readonly string[] supportedEmotions = { "happy", "sad", "angry", "fear", "surprise", "disgust", "neutral" };

    // Emotion normalization mapping for server variants
    private readonly Dictionary<string, string> emotionNormalization = new Dictionary<string, string>
    {
        { "fearful", "fear" }, { "afraid", "fear" }, { "scared", "fear" },
        { "joyful", "happy" }, { "pleased", "happy" }, { "glad", "happy" }, { "cheerful", "happy" },
        { "upset", "sad" }, { "depressed", "sad" }, { "disappointed", "sad" },
        { "furious", "angry" }, { "mad", "angry" }, { "irritated", "angry" },
        { "shocked", "surprise" }, { "amazed", "surprise" }, { "astonished", "surprise" },
        { "revolted", "disgust" }, { "repulsed", "disgust" }, { "sickened", "disgust" },
        { "calm", "neutral" }, { "peaceful", "neutral" }, { "relaxed", "neutral" },
        {"angry", "angry"}, {"happy", "happy"}, {"sad", "sad"}, {"fear", "fear"},
        {"surprise", "surprise"}, {"disgust", "disgust"}, {"neutral", "neutral"}
    };

    private void Awake()
    {
        // Singleton pattern implementation
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeEmotionHistory();
            Debug.Log("EmotionStatisticsManager initialized and marked as persistent.");
        }
        else
        {
            Debug.LogWarning("Another instance of EmotionStatisticsManager already exists. Destroying this one.");
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        // Update total session duration
        if (currentStatistics != null)
        {
            currentStatistics.sessionDuration = Time.time - sessionStartTime;
        }

        // Update duration for the current emotion
        if (emotionHistory.Count > 0)
        {
            var lastEmotion = emotionHistory[emotionHistory.Count - 1].emotion;
            if (currentStatistics.totalDuration.ContainsKey(lastEmotion))
            {
                currentStatistics.totalDuration[lastEmotion] += Time.deltaTime;
            }
        }
    }

    /// <summary>
    /// Records a new emotion result, adding it to the history and updating statistics.
    /// This is the primary entry point for other scripts like AudioEmotionRecognizer.
    /// </summary>
    /// <param name="result">The emotion result to record.</param>
    public void RecordEmotion(EmotionResult result)
    {
        string normalizedEmotion = NormalizeEmotion(result.emotion);

        EmotionHistoryEntry entry = new EmotionHistoryEntry
        {
            emotion = normalizedEmotion,
            confidence = result.confidence,
            timestamp = result.timestamp,
            sessionTime = Time.time - sessionStartTime,
            sequenceId = ++emotionSequenceId
        };

        emotionHistory.Add(entry);

        // Limit history size
        if (emotionHistory.Count > maxHistoryEntries)
        {
            emotionHistory.RemoveAt(0);
        }

        // Update statistics with the new entry
        UpdateStatistics(entry);

        // Trigger event for any listeners
        OnEmotionHistoryUpdate?.Invoke(entry);

        if (logDetailedHistory)
        {
            string originalEmotionLog = result.emotion != normalizedEmotion ? $" (was: {result.emotion})" : "";
            Debug.Log($"[EMOTION HISTORY] #{entry.sequenceId} - {entry.emotion.ToUpper()}{originalEmotionLog} (Confidence: {entry.confidence:F2}, Session Time: {entry.sessionTime:F1}s)");
        }
    }

    /// <summary>
    /// Initializes or resets the emotion history and statistics.
    /// </summary>
    [ContextMenu("Clear Emotion History")]
    public void InitializeEmotionHistory()
    {
        sessionStartTime = Time.time;
        emotionHistory.Clear();
        currentStatistics = new EmotionStatistics();
        emotionSequenceId = 0;

        // Initialize dictionaries for all supported emotions
        foreach (string emotion in supportedEmotions)
        {
            currentStatistics.emotionCounts[emotion] = 0;
            currentStatistics.totalConfidence[emotion] = 0f;
            currentStatistics.averageConfidence[emotion] = 0f;
            currentStatistics.maxConfidence[emotion] = 0f;
            currentStatistics.totalDuration[emotion] = 0f;
        }

        // Stop any existing logging coroutine before starting a new one
        if (historyDisplayCoroutine != null)
        {
            StopCoroutine(historyDisplayCoroutine);
        }

        if (enableAutomaticLogging && historyDisplayInterval > 0)
        {
            historyDisplayCoroutine = StartCoroutine(HistoryDisplayCoroutine());
        }

        Debug.Log("Emotion history and statistics have been reset.");
    }

    private void UpdateStatistics(EmotionHistoryEntry entry)
    {
        string emotion = entry.emotion;

        if (!currentStatistics.emotionCounts.ContainsKey(emotion))
        {
            Debug.LogWarning($"Emotion '{emotion}' not found in statistics dictionary. It will be ignored.");
            return;
        }

        currentStatistics.emotionCounts[emotion]++;
        currentStatistics.totalConfidence[emotion] += entry.confidence;
        currentStatistics.averageConfidence[emotion] = currentStatistics.totalConfidence[emotion] / currentStatistics.emotionCounts[emotion];
        currentStatistics.maxConfidence[emotion] = Mathf.Max(currentStatistics.maxConfidence[emotion], entry.confidence);
        currentStatistics.totalDetections++;

        UpdateDominantEmotion();

        // Fire event to notify listeners of the updated statistics
        OnEmotionStatisticsUpdate?.Invoke(currentStatistics);
    }

    private void UpdateDominantEmotion()
    {
        int maxCount = 0;
        string dominantEmotion = "neutral";

        foreach (var kvp in currentStatistics.emotionCounts)
        {
            if (kvp.Value > maxCount)
            {
                maxCount = kvp.Value;
                dominantEmotion = kvp.Key;
            }
        }

        currentStatistics.dominantEmotion = dominantEmotion;
        currentStatistics.dominantEmotionPercentage = currentStatistics.totalDetections > 0 ?
            (float)maxCount / currentStatistics.totalDetections * 100f : 0f;
    }

    public string NormalizeEmotion(string emotion)
    {
        if (string.IsNullOrEmpty(emotion))
            return "neutral";

        emotion = emotion.ToLower().Trim();

        if (emotionNormalization.TryGetValue(emotion, out string normalized))
            return normalized;

        Debug.LogWarning($"Unknown emotion '{emotion}' encountered. Defaulting to neutral.");
        return "neutral";
    }

    private IEnumerator HistoryDisplayCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(historyDisplayInterval);
            DisplayEmotionStatistics();
        }
    }

    [ContextMenu("Display Emotion Statistics")]
    public void DisplayEmotionStatistics()
    {
        if (currentStatistics.totalDetections == 0)
        {
            Debug.Log("No emotion statistics available to display.");
            return;
        }

        var statsBuilder = new System.Text.StringBuilder();
        statsBuilder.AppendLine("=== EMOTION STATISTICS ===");
        statsBuilder.AppendLine($"Session Duration: {currentStatistics.sessionDuration:F1}s | Total Detections: {currentStatistics.totalDetections}");
        statsBuilder.AppendLine($"Dominant Emotion: {currentStatistics.dominantEmotion.ToUpper()} ({currentStatistics.dominantEmotionPercentage:F1}%)");
        statsBuilder.AppendLine("");
        statsBuilder.AppendLine("EMOTION HISTOGRAM:");

        foreach (var kvp in currentStatistics.emotionCounts.OrderByDescending(x => x.Value))
        {
            if (kvp.Value > 0)
            {
                float percentage = (float)kvp.Value / currentStatistics.totalDetections * 100f;
                string bar = GenerateProgressBar(percentage, 20);
                statsBuilder.AppendLine($"{kvp.Key.ToUpper().PadRight(10)} | {bar} {kvp.Value,3} ({percentage:F1}%) | Avg Conf: {currentStatistics.averageConfidence[kvp.Key]:F2} | Max: {currentStatistics.maxConfidence[kvp.Key]:F2}");
            }
        }

        statsBuilder.AppendLine("\nEMOTION PROGRESSION (Last 10):");
        var recentHistory = emotionHistory.TakeLast(10).ToList();
        foreach (var entry in recentHistory)
        {
            statsBuilder.AppendLine($"  {entry.sessionTime:F1}s: {entry.emotion.ToUpper()} (Conf: {entry.confidence:F2})");
        }
        statsBuilder.AppendLine("=== END STATISTICS ===");
        Debug.Log(statsBuilder.ToString());
    }

    [ContextMenu("Display Final Emotion Summary")]
    public void DisplayFinalEmotionSummary()
    {
         if (currentStatistics.totalDetections == 0)
        {
            Debug.Log("No emotion data to summarize.");
            return;
        }

        DisplayEmotionStatistics(); // Show the standard stats first

        ConversationEmotionSummary summary = new ConversationEmotionSummary
        {
            sessionStartTime = this.sessionStartTime,
            sessionEndTime = Time.time,
            totalEmotions = currentStatistics.totalDetections,
            primaryEmotion = currentStatistics.dominantEmotion,
            primaryEmotionConfidence = currentStatistics.averageConfidence[currentStatistics.dominantEmotion],
            emotionProgression = emotionHistory.Select(e => e.emotion).ToList(),
            emotionPercentages = new Dictionary<string, float>()
        };

        foreach (var kvp in currentStatistics.emotionCounts)
        {
            if (kvp.Value > 0)
            {
                summary.emotionPercentages[kvp.Key] = (float)kvp.Value / currentStatistics.totalDetections * 100f;
            }
        }
        
        var summaryBuilder = new System.Text.StringBuilder();
        summaryBuilder.AppendLine("=== FINAL CONVERSATION SUMMARY ===");
        summaryBuilder.AppendLine($"  Primary Emotion: {summary.primaryEmotion.ToUpper()} ({summary.primaryEmotionConfidence:F2} avg confidence)");
        summaryBuilder.AppendLine($"  Session Duration: {(summary.sessionEndTime - summary.sessionStartTime):F1} seconds");
        summaryBuilder.AppendLine($"  Total Emotions Detected: {summary.totalEmotions}");
        if(summary.emotionPercentages.Values.Count > 0)
        {
            summaryBuilder.AppendLine($"  Emotional Consistency: {(summary.emotionPercentages.Values.Max()):F1}% dominant");
        }
        summaryBuilder.AppendLine("=== END SUMMARY ===");

        Debug.Log(summaryBuilder.ToString());
    }

    public ConversationEmotionSummary GetFinalEmotionSummary()
    { 
        if (currentStatistics.totalDetections == 0)
        {
            return new ConversationEmotionSummary();
        }
        ConversationEmotionSummary summary = new ConversationEmotionSummary
        {
            sessionStartTime = this.sessionStartTime,
            sessionEndTime = Time.time,
            totalEmotions = currentStatistics.totalDetections,
            primaryEmotion = currentStatistics.dominantEmotion,
            primaryEmotionConfidence = currentStatistics.averageConfidence[currentStatistics.dominantEmotion],
            emotionProgression = emotionHistory.Select(e => e.emotion).ToList(),
            emotionPercentages = new Dictionary<string, float>()
        };

        foreach (var kvp in currentStatistics.emotionCounts)
        {
            if (kvp.Value > 0)
            {
                summary.emotionPercentages[kvp.Key] = (float)kvp.Value / currentStatistics.totalDetections * 100f;
            }
        }

        return summary;
    }

    private string GenerateProgressBar(float percentage, int width)
    {
        int filledWidth = Mathf.RoundToInt(percentage / 100f * width);
        return new string('█', filledWidth) + new string('░', width - filledWidth);
    }

    // Public getters for other scripts
    public EmotionStatistics GetCurrentStatistics() => currentStatistics;
    public List<EmotionHistoryEntry> GetEmotionHistory() => new List<EmotionHistoryEntry>(emotionHistory);

    private void OnDestroy()
    {
        // When the game is closed, display a final summary.
        if (Instance == this)
        {
            Debug.Log("EmotionStatisticsManager is being destroyed. Displaying final summary.");
            DisplayFinalEmotionSummary();
        }
    }
}
