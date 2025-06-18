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
        if (scenario == "scenario1")
        {
            currentState = new EmotionalState
            {
                Emotions = new Dictionary<string, int>  //SURPRISE scenario
                {
                    { "Joy", 75 }, // A significant surge of joy from newfound hope
                    { "Trust", 60 }, // Developing trust in Maya and the discovery
                    { "Fear", 0 },
                    { "Surprise", 95 }, // Overwhelming surprise at the revelation
                    { "Sadness", 10 }, // Residual sadness from his previous struggles, now fading
                    { "Disgust", 0 },
                    { "Anger", 0 },
                    { "Anticipation", 80 } // High anticipation for the implications of this discovery
                },
                Triggers = new List<EmotionalTrigger>
                {
                    new EmotionalTrigger
                    {
                        Id = 1,
                        Description = "Maya, a representative from a historical preservation society, identified one of your old, discarded paintings as remarkably similar to the lost early work of a renowned, reclusive master, and provided photographic evidence linking it to the cottage's attic.",
                        TargetEmotion = "Surprise",
                        Intensity = 95
                    },
                    new EmotionalTrigger
                    {
                        Id = 2,
                        Description = "The sudden and complete reversal of your self-perception and career prospects, transforming a 'failed' painting into a potentially priceless and historically significant artwork.",
                        TargetEmotion = "Joy",
                        Intensity = 75
                    },
                    new EmotionalTrigger
                    {
                        Id = 3,
                        Description = "The unexpected connection between his newly inherited, dilapidated cottage and a renowned artist's past, adding profound meaning to his new home.",
                        TargetEmotion = "Anticipation",
                        Intensity = 80
                    }
                }
            };
            
        }else if (scenario == "scenario2")
        {
            currentState = new EmotionalState
            {
                Emotions = new Dictionary<string, int> //JOY scenario
                {
                    { "Joy", 95 }, // Overwhelmed by the monumental support
                    { "Trust", 85 }, // Trust in Liam and the community
                    { "Fear", 5 }, // Minor residual anxieties about past setbacks, but largely overridden
                    { "Surprise", 90 }, // Highly surprised by the extent of the support
                    { "Sadness", 0 },
                    { "Disgust", 0 },
                    { "Anger", 0 },
                    { "Anticipation", 70 } // Anticipation for the garden and community center's future
                },
                Triggers = new List<EmotionalTrigger>
                {
                    new EmotionalTrigger
                    {
                        Id = 1,
                        Description = "Liam, a casual acquaintance, revealed he's secured pro-bono architectural work, material donations, and a team of volunteers not just to finish the community garden, but to build a solar-powered community center on the lot as well, far exceeding all previous hopes and efforts.",
                        TargetEmotion = "Joy",
                        Intensity = 95
                    },
                    new EmotionalTrigger
                    {
                        Id = 2,
                        Description = "The unexpected and vast scale of support from Liam and his network for her long-standing passion project.",
                        TargetEmotion = "Surprise",
                        Intensity = 70
                    },
                    new EmotionalTrigger
                    {
                        Id = 3,
                        Description = "A newfound sense of trust and belief in the community's support for her vision.",
                        TargetEmotion = "Trust",
                        Intensity = 85
                    }
                }
            };
        }else if (scenario == "scenario3")
        {
            currentState = new EmotionalState
            {
                Emotions = new Dictionary<string, int> //FEAR scenario
                {
                    { "Joy", 0 },
                    { "Trust", 10 }, // Severely diminished trust in the stranger
                    { "Fear", 95 }, // Overwhelming fear
                    { "Surprise", 80 }, // Highly surprised by the stranger's knowledge
                    { "Sadness", 15 }, // Residual sadness from the cat's disappearance
                    { "Disgust", 0 },
                    { "Anger", 20 }, // Some anger at the intrusion and implied threat
                    { "Anticipation", 70 } // Anxious anticipation of what the stranger will do next
                },
                Triggers = new List<EmotionalTrigger> 
                {
                    new EmotionalTrigger
                    {
                        Id = 1,
                        Description = "A stranger, Sarah, who claimed to be a lost hiker, casually revealed knowledge of Mark's missing cat, 'Shadow,' a detail she should not have known, immediately after entering his home.",
                        TargetEmotion = "Fear",
                        Intensity = 95
                    },
                    new EmotionalTrigger
                    {
                        Id = 2,
                        Description = "The unexpected revelation by Sarah, a supposed stranger, about his cat's name, coupled with her unsettling tone.",
                        TargetEmotion = "Surprise",
                        Intensity = 80
                    },
                    new EmotionalTrigger
                    {
                        Id = 3,
                        Description = "The immediate and profound betrayal of trust when Sarah revealed her sinister knowledge, indicating she is not who she claims to be.",
                        TargetEmotion = "Trust",
                        Intensity = -75 // Represents a significant drop in trust
                    }
                }
            };
        }else if (scenario == "scenario4")
        {
            currentState = new EmotionalState
            {
                Emotions = new Dictionary<string, int> //ANGER scenario
                {
                    { "Joy", 0 },
                    { "Trust", 5 }, // Trust in David is almost completely eroded
                    { "Fear", 10 }, // Minor fear of the scandal not being exposed
                    { "Surprise", 70 }, // Surprised by David's betrayal
                    { "Sadness", 20 }, // Sadness over the potential failure of her work and betrayal
                    { "Disgust", 50 }, // Disgust at David's actions and the implied corruption
                    { "Anger", 90 }, // Intense anger at David's betrayal and sabotage
                    { "Anticipation", 40 } // Anticipation for confronting David or finding another way to publish
                },
                Triggers = new List<EmotionalTrigger>
                {
                    new EmotionalTrigger
                    {
                        Id = 1,
                        Description = "Aisha discovered her long-time editor and mentor, David, subtly sabotaging her highly anticipated corruption expose, suggesting she 'take a break' and downplaying her irrefutable evidence, clearly betraying her trust and the story.",
                        TargetEmotion = "Anger",
                        Intensity = 90
                    },
                    new EmotionalTrigger
                    {
                        Id = 2,
                        Description = "The profound betrayal by her trusted editor, whom she had implicitly relied upon for her groundbreaking investigative work.",
                        TargetEmotion = "Trust",
                        Intensity = -90 // Represents a significant drop in trust
                    },
                    new EmotionalTrigger
                    {
                        Id = 3,
                        Description = "The unexpected realization that David, her mentor, was actively working against you and the publication of your meticulously gathered evidence.",
                        TargetEmotion = "Surprise",
                        Intensity = 70
                    }
                }
            };

        }

        Debug.Log("EmotionMeter initialized with "+ scenario + " state.");
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
