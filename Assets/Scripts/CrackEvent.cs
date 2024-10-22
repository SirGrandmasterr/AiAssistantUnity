using Unity.VisualScripting;
using UnityEngine;
using Whisper.Ears;

public class CrackEvent : MonoBehaviour
{
    public AudioSource cracker;
    public Ears assistantEars;
    
    public AssetLocationUpdater locationUpdater;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Crushable"))
        {
            cracker.Play();
            print(other.gameObject.name);
            other.gameObject.tag = "CanBeRepaired";
            other.gameObject.transform.parent.GameObject().tag = "CanBeRepaired";
            other.gameObject.SetActive(false);
            assistantEars.HearCrashingSound(other.transform.parent.GameObject(), locationUpdater.location);
        }
    }

    
}