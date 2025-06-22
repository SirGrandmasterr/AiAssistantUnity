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
        Debug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(currentState, Newtonsoft.Json.Formatting.Indented));
    }

    /// <summary>
    /// Sets up a default emotional state for the avatar.
    /// This is called on Awake but can be called again to reset the state.
    /// </summary>
    public void InitializeDefaultState()
    {
        if (scenario == "angry")
        {
            currentState = new EmotionalState
            {
                Emotions = new Dictionary<string, int>
                {
                    { "Joy", 0 },
                    { "Trust", 40 },
                    { "Fear", 5 },
                    { "Surprise", 10 },
                    { "Sadness", 25 },
                    { "Disgust", 80 },
                    { "Anger", 90 },
                    { "Anticipation", 15 }
                },
                Triggers = new List<EmotionalTrigger>
                {
                    new EmotionalTrigger
                    {
                        Id = 1,
                        Description =
                            "Overheard patrons discussing a piece of art purely as a financial asset and tax write-off.",
                        TargetEmotion = "Anger",
                        Intensity = 90
                    },
                    new EmotionalTrigger
                    {
                        Id = 2,
                        Description =
                            "The commodification of art, reducing its cultural and emotional value to a monetary one.",
                        TargetEmotion = "Disgust",
                        Intensity = 80
                    }
                }
            };
        }
        else if (scenario == "sad")
        {
            currentState = new EmotionalState
            {
                Emotions = new Dictionary<string, int>
                {
                    { "Joy", 10 },
                    { "Trust", 55 },
                    { "Fear", 0 },
                    { "Surprise", 30 },
                    { "Sadness", 95 },
                    { "Disgust", 0 },
                    { "Anger", 0 },
                    { "Anticipation", 20 }
                },
                Triggers = new List<EmotionalTrigger>
                {
                    new EmotionalTrigger
                    {
                        Id = 1,
                        Description =
                            "Seeing a landscape painting that unexpectedly and vividly recalled memories of a recently deceased grandfather.",
                        TargetEmotion = "Sadness",
                        Intensity = 95
                    },
                    new EmotionalTrigger
                    {
                        Id = 2,
                        Description = "The sudden and involuntary nature of the grief-filled memory.",
                        TargetEmotion = "Surprise",
                        Intensity = 30
                    }
                }
            };
        }
        else if (scenario == "joyful")
        {
            currentState = new EmotionalState
            {
                Emotions = new Dictionary<string, int>
                {
                    { "Joy", 98 },
                    { "Trust", 75 },
                    { "Fear", 0 },
                    { "Surprise", 90 },
                    { "Sadness", 0 },
                    { "Disgust", 0 },
                    { "Anger", 0 },
                    { "Anticipation", 95 }
                },
                Triggers = new List<EmotionalTrigger>
                {
                    new EmotionalTrigger
                    {
                        Id = 1,
                        Description =
                            "Unexpectedly seeing 'The Sunken Cathedral', a painting you wrote your thesis on and never expected to see in person.",
                        TargetEmotion = "Joy",
                        Intensity = 98
                    },
                    new EmotionalTrigger
                    {
                        Id = 2,
                        Description = "The sheer improbability and luck of the encounter.",
                        TargetEmotion = "Surprise",
                        Intensity = 90
                    }
                }
            };
        }

        else if (scenario == "surprised")
        {
            currentState = new EmotionalState
            {
                Emotions = new Dictionary<string, int>
                {
                    { "Joy", 10 }, // A thrill of discovery
                    { "Trust", 30 }, // Unsure who to trust with the information
                    { "Fear", 70 }, // Fear of being wrong, or of the implications of being right
                    { "Surprise", 95 }, // Primary state is intellectual shock
                    { "Sadness", 5 },
                    { "Disgust", 10 },
                    { "Anger", 5 },
                    { "Anticipation", 85 } // High anticipation of what this discovery means
                },
                Triggers = new List<EmotionalTrigger>
                {
                    new EmotionalTrigger
                    {
                        Id = 1,
                        Description =
                            "Noticed an anachronistic pigment (Phthalo Green) in a famous 18th-century painting, implying it's a forgery.",
                        TargetEmotion = "Surprise",
                        Intensity = 95
                    },
                    new EmotionalTrigger
                    {
                        Id = 2,
                        Description =
                            "The intellectual shock and professional fear associated with uncovering a potential major art fraud.",
                        TargetEmotion = "Fear",
                        Intensity = 70
                    },
                    new EmotionalTrigger
                    {
                        Id = 3,
                        Description = "Urgency and curiosity about confirming the discovery and its consequences.",
                        TargetEmotion = "Anticipation",
                        Intensity = 85
                    }
                }
            };
        }

        Debug.Log("EmotionMeter initialized with " + scenario + " state.");
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


/*


 */