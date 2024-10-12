using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Whisper.Ears;

public class RepairEvent : MonoBehaviour
{
    
    public AssetLocationUpdater locationUpdater;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("CanBeRepaired"))
        {
            
            print(other.gameObject.name);
            other.gameObject.SetActive(true);
            
        }
    }

    
}