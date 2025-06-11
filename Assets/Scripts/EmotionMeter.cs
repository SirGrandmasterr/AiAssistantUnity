using UnityEngine;
using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Represents a specific event or memory that causes an emotion.
/// </summary>
[System.Serializable]
public struct EmotionalTrigger
{ 
    public int Id { get; set; }
    public string Description { get; set; }
    public string TargetEmotion { get; set; }
    public int Intensity { get; set; }
}

/// <summary>
/// Represents the complete, self-contained emotional context of the Avatar.
/// </summary>
[System.Serializable]
public struct EmotionalState
{
    public Dictionary<string, int> Emotions { get; set; }
    public List<EmotionalTrigger> Triggers { get; set; }
}


/// <summary>
/// A Unity MonoBehaviour that maintains and manages an LLM-driven avatar's emotional state.
/// Attach this component to your Avatar's GameObject.
/// </summary>
public class EmotionMeter : MonoBehaviour
{
    // The current emotional state of the avatar. It's private to ensure
    // that it's only modified through the provided public methods.
    private EmotionalState currentState;

    /// <summary>
    /// Awake is called when the script instance is being loaded.
    /// We use it to set up the initial, default emotional state.
    /// </summary>
    void Awake()
    {
        InitializeDefaultState();
    }

    /// <summary>
    /// Sets up a default emotional state for the avatar.
    /// This is called on Awake but can be called again to reset the state.
    /// </summary>
    public void InitializeDefaultState()
    {
        currentState = new EmotionalState
        {
            Emotions = new Dictionary<string, int>
            {
                { "Joy", 50 },
                { "Trust", 60 },
                { "Fear", 10 },
                { "Surprise", 0 },
                { "Sadness", 0 },
                { "Disgust", 0 },
                { "Anger", 0 },
                { "Anticipation", 30 }
            },
            Triggers = new List<EmotionalTrigger>
            {
                new EmotionalTrigger
                {
                    Id = 1,
                    Description = "Want to get to know the visitor",
                    TargetEmotion = "Anticipation",
                    Intensity = 30
                }
            }
        };

        Debug.Log("EmotionMeter initialized with default state.");
    }

    /// <summary>
    /// Public method to safely get the current emotional state.
    /// Other scripts can call this to read the avatar's emotions.
    /// </summary>
    /// <returns>The current EmotionalState struct.</returns>
    public EmotionalState GetEmotionalState()
    {
        return currentState;
    }

    /// <summary>
    /// Public method to update the emotional state.
    /// This should be called after receiving an updated state from the LLM.
    /// </summary>
    /// <param name="newState">The new emotional state to apply.</param>
    public void UpdateEmotionalState(EmotionalState newState)
    {
        currentState = newState;
        Debug.Log("EmotionalState has been updated.");
        
        // Optional: You could log the new dominant emotion for easier debugging.
        // LogDominantEmotion();
    }

    /// <summary>
    /// A helper method for debugging that prints the current dominant emotion.
    /// </summary>
    public void LogDominantEmotion()
    {
        if (currentState.Emotions == null || currentState.Emotions.Count == 0)
        {
            Debug.Log("No emotions present in the current state.");
            return;
        }

        string dominantEmotion = "None";
        float maxIntensity = 0f;

        foreach (var emotion in currentState.Emotions)
        {
            if (emotion.Value > maxIntensity)
            {
                maxIntensity = emotion.Value;
                dominantEmotion = emotion.Key;
            }
        }

        Debug.Log($"Current dominant emotion: {dominantEmotion} with intensity {maxIntensity}");
    }
}
