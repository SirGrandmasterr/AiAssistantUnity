using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Whisper.Ears;

public class CrackEvent : MonoBehaviour
{
    public AudioSource cracker;
    public Ears assistantEars;
    private readonly Vector3 _heightOffset = new Vector3(0f, 1.7f, 0);
    public AssetLocationUpdater locationUpdater;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Crushable"))
        {
            cracker.Play();
            print(other.gameObject.name);
            other.gameObject.SetActive(false);
            assistantEars.HearCrashingSound(other.transform.parent.GameObject(), locationUpdater.location);
        }
    }

    
}