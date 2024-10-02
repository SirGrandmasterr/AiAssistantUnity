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
   private void OnTriggerEnter(Collider other)
   {
      if (other.gameObject.CompareTag("Crushable"))
      {
         cracker.Play();
         print(other.gameObject.name);
         other.gameObject.SetActive(false);
      }
      
   }
}
