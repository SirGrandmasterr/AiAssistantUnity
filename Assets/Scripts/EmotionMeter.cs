using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Required for ToDictionary

/// <summary>
/// Defines the types of emotions the assistant can experience.
/// </summary>
public enum EmotionType
{
    Stress,
    Joy,
    Curiosity,
    Fear,
    Anger,
    Sadness,
    Boredom,
    // Add more emotions as needed
}

/// <summary>
/// Manages the emotional state of the AI assistant.
/// It allows adjusting emotions within defined bounds and provides access to current emotion values.
/// This class implements a Singleton pattern for easy access.
/// </summary>
public class EmotionMeter : MonoBehaviour
{
    // Singleton instance
    public static EmotionMeter Instance { get; private set; }

    // Dictionary to store the current value of each emotion.
    // Emotions are typically represented as a float value, often between 0 and 100.
    private Dictionary<EmotionType, float> emotions = new Dictionary<EmotionType, float>();

    // Default values for emotions. These can be configured in the Inspector.
    [System.Serializable]
    public struct EmotionDefault
    {
        public EmotionType emotion;
        [Range(0f, 100f)]
        public float defaultValue;
    }
    public struct EmotionEntry
    {
        public string emotion;
        public float value;

        public EmotionEntry(string k, float v)
        {
            emotion= k;
            value = v;
        }
    }
    public List<EmotionDefault> defaultEmotionValues = new List<EmotionDefault>
    {
        new EmotionDefault { emotion = EmotionType.Stress, defaultValue = 10f },
        new EmotionDefault { emotion = EmotionType.Joy, defaultValue = 20f },
        new EmotionDefault { emotion = EmotionType.Curiosity, defaultValue = 20f },
        new EmotionDefault { emotion = EmotionType.Fear, defaultValue = 5f },
        new EmotionDefault { emotion = EmotionType.Anger, defaultValue = 5f },
        new EmotionDefault { emotion = EmotionType.Sadness, defaultValue = 10f },
        new EmotionDefault { emotion = EmotionType.Boredom, defaultValue = 20f }
    };

    void Awake()
    {
        // Implement Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Optional: if you want emotions to persist across scenes
            InitializeEmotions();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Initializes all defined emotions with their default values.
    /// </summary>
    private void InitializeEmotions()
    {
        // Initialize from the defaultEmotionValues list
        foreach (var emotionDefault in defaultEmotionValues)
        {
            emotions[emotionDefault.emotion] = emotionDefault.defaultValue;
        }

        // Ensure all emotions in the enum are initialized, even if not in defaultEmotionValues
        foreach (EmotionType emotionType in System.Enum.GetValues(typeof(EmotionType)))
        {
            if (!emotions.ContainsKey(emotionType))
            {
                emotions[emotionType] = 50f; // A generic default if not specified
                Debug.LogWarning($"EmotionType '{emotionType}' was not found in defaultEmotionValues. Initialized to 50.");
            }
        }
        LogCurrentEmotions("Initialized");
    }

    /// <summary>
    /// Adjusts the value of a specified emotion by a given amount.
    /// The adjustment is constrained by lower and upper bounds.
    /// </summary>
    /// <param name="emotion">The emotion to adjust.</param>
    /// <param name="amount">The amount to add (can be negative to subtract).</param>
    /// <param name="lowerBound">The minimum value the emotion can reach after adjustment.</param>
    /// <param name="upperBound">The maximum value the emotion can reach after adjustment.</param>
    public void AdjustEmotion(EmotionType emotion, float amount, float lowerBound = 0f, float upperBound = 100f)
    {
        if (emotions.TryGetValue(emotion, out float currentValue))
        {
            float newValue = currentValue + amount;
            // Clamp the new value to the overall 0-100 range first
            newValue = Mathf.Clamp(newValue, 0f, 100f);
            // Then, clamp it to the context-specific lower/upper bounds for this adjustment
            newValue = Mathf.Clamp(newValue, lowerBound, upperBound);

            if (currentValue != newValue)
            {
                emotions[emotion] = newValue;
                Debug.Log($"Adjusted {emotion} by {amount}. New value: {newValue} (Bounds: {lowerBound}-{upperBound})");
                // LogCurrentEmotions($"After adjusting {emotion}"); // Optional: for detailed logging
            }
            else
            {
                Debug.Log($"Attempted to adjust {emotion} by {amount}, but value {currentValue} remained unchanged due to bounds ({lowerBound}-{upperBound}) or no actual change.");
            }
        }
        else
        {
            Debug.LogWarning($"EmotionMeter: Attempted to adjust uninitialized emotion '{emotion}'.");
        }
    }

    /// <summary>
    /// Sets the value of a specified emotion directly.
    /// The value is clamped between 0 and 100.
    /// </summary>
    /// <param name="emotion">The emotion to set.</param>
    /// <param name="value">The new value for the emotion.</param>
    public void SetEmotion(EmotionType emotion, float value)
    {
        emotions[emotion] = Mathf.Clamp(value, 0f, 100f);
        Debug.Log($"Set {emotion} to {emotions[emotion]}.");
        // LogCurrentEmotions($"After setting {emotion}"); // Optional: for detailed logging
    }


    /// <summary>
    /// Gets the current value of a specified emotion.
    /// </summary>
    /// <param name="emotion">The emotion to query.</param>
    /// <returns>The current value of the emotion, or 0f if not found (should not happen if initialized correctly).</returns>
    public float GetEmotionValue(EmotionType emotion)
    {
        if (emotions.TryGetValue(emotion, out float value))
        {
            return value;
        }
        Debug.LogWarning($"EmotionMeter: Emotion '{emotion}' not found. Returning 0.");
        return 0f;
    }

    /// <summary>
    /// Gets a dictionary of all current emotion values, with string keys for serialization.
    /// </summary>
    /// <returns>A dictionary where keys are emotion names (string) and values are their current levels (float).</returns>
    public Dictionary<string, float> GetAllEmotionsAsStringKeys()
    {
        // Using System.Linq to convert Enum keys to strings for the dictionary
        return emotions.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value);
    }
    
    public List<EmotionEntry> GetAllEmotionsAsSerializableList()
    {
        List<EmotionEntry> serializableList = new List<EmotionEntry>();
        foreach (var kvp in emotions)
        {
            serializableList.Add(new EmotionEntry(kvp.Key.ToString(), kvp.Value));
        }
        return serializableList;
        // Or using LINQ:
        // return emotions.Select(kvp => new EmotionEntry(kvp.Key.ToString(), kvp.Value)).ToList();
    }

    /// <summary>
    /// Logs the current state of all emotions.
    /// </summary>
    /// <param name="context">A string to provide context for the log message (e.g., "After Player Interaction").</param>
    public void LogCurrentEmotions(string context = "")
    {
        string logMessage = $"EmotionMeter State ({context}): ";
        foreach (var entry in emotions)
        {
            logMessage += $"{entry.Key}: {entry.Value:F1} | ";
        }
        Debug.Log(logMessage.TrimEnd(' ', '|'));
    }

    // Example usage (can be called from other scripts):
    // EmotionMeter.Instance.AdjustEmotion(EmotionType.Stress, 5, 20, 80);
    // float currentJoy = EmotionMeter.Instance.GetEmotionValue(EmotionType.Joy);
}
