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
    private string scenario;

    /// <summary>
    /// Awake is called when the script instance is being loaded.
    /// We use it to set up the initial, default emotional state.
    /// </summary>
    void Awake()
    {
        if (PlayerPrefs.HasKey("Scenario"))
        {
            scenario = PlayerPrefs.GetString("Scenario");
        }
        else
        {
            PlayerPrefs.SetString("Scenario", "scenario1");
        }
        InitializeDefaultState();
        
        
    }

    /// <summary>
    /// Sets up a default emotional state for the avatar.
    /// This is called on Awake but can be called again to reset the state.
    /// </summary>
    public void InitializeDefaultState()
    {
        if (scenario == "scenario1")
        {
            currentState = new EmotionalState
            {
                Emotions = new Dictionary<string, int>
                {
                    { "Joy", 15 },
                    { "Trust", 50 },
                    { "Fear", 0 },
                    { "Surprise", 50 },
                    { "Sadness", 5 },
                    { "Disgust", 15 },
                    { "Anger", 80 },
                    { "Anticipation", 20 }
                },
                Triggers = new List<EmotionalTrigger>
                {
                    new EmotionalTrigger
                    {
                        Id = 1,
                        Description = "Markus did not clean the ammunition storage as instructed.",
                        TargetEmotion = "Anger",
                        Intensity = 80
                    },
                    new EmotionalTrigger
                    {
                        Id = 2,
                        Description = "The ammunition storage appears to be in disarray.",
                        TargetEmotion = "Surprise",
                        Intensity = 50
                    }
                }
            };
            
        }else 
        {
            currentState = new EmotionalState
            {
                Emotions = new Dictionary<string, int>
                {
                    { "Joy", 80 },
                    { "Trust", 70 },
                    { "Fear", 0 },
                    { "Surprise", 90 },
                    { "Sadness", 0 },
                    { "Disgust", 0 },
                    { "Anger", 0 },
                    { "Anticipation", 50 }
                },
                Triggers = new List<EmotionalTrigger>
                {
                    new EmotionalTrigger
                    {
                        Id = 1,
                        Description = "Informed of an upcoming promotion",
                        TargetEmotion = "Joy",
                        Intensity = 80
                    },
                    new EmotionalTrigger
                    {
                        Id = 1,
                        Description = "Upcoming promotion is welcome, but unexpected",
                        TargetEmotion = "Surprise",
                        Intensity = 90
                    },
                }
            };
        }

        Debug.Log("EmotionMeter initialized with "+scenario + " state.");
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
