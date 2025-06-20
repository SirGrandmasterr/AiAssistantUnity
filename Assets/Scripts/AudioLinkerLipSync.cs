using UnityEngine;
using uLipSync; // Add reference to uLipSync namespace

/// <summary>
/// This component acts as the bridge between the WebRtcProvider singleton
/// and the avatar's local AudioSource and uLipSync components.
/// It must be placed on the same GameObject as the uLipSync and AudioSource components.
/// </summary>
//[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(uLipSync.uLipSync))]
public class AvatarAudioLinkerLipSync : MonoBehaviour
{
    public AudioEmotionRecognizer audioEmotionRecognizer;
    void Start()
    {
        // Find the local components on this avatar GameObject.
        AudioSource avatarAudioSource = GetComponentInParent<AudioSource>();
        uLipSync.uLipSync lipSyncComponent = GetComponent<uLipSync.uLipSync>();
        
        // Find the persistent WebRtcProvider instance.
        WebRtcProvider rtcProvider = WebRtcProvider.Instance;

        if (rtcProvider != null)
        {
            // Use the new public method to link this avatar's components
            // to the provider. The provider will handle everything else.
            Debug.Log("AvatarAudioLinker is linking local components to the WebRtcProvider.");
            rtcProvider.LinkAvatarComponents(avatarAudioSource, lipSyncComponent, audioEmotionRecognizer);
        }
        else
        {
            Debug.LogError("Could not find WebRtcProvider instance in the scene. " +
                           "Ensure the Initializer scene with the provider was loaded first.");
        }
    }
}