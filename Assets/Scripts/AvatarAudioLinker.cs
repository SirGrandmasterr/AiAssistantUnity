using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AvatarAudioLinker : MonoBehaviour
{
    void Start()
    {
        // Find the AudioSource component on this avatar GameObject.
        AudioSource avatarAudioSource = GetComponent<AudioSource>();

        // Find the persistent WebRtcProvider instance.
        WebRtcProvider rtcProvider = WebRtcProvider.Instance;

        if (rtcProvider != null)
        {
            // Use the public method to link this avatar's AudioSource
            // to the provider.
           // rtcProvider.SetAudioSource(avatarAudioSource);
        }
        else
        {
            Debug.LogError("Could not find WebRtcProvider instance in the scene. " +
                           "Ensure the Initializer scene was loaded first.");
        }
    }
}