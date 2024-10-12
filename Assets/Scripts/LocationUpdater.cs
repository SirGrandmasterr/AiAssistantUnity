using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class LocationUpdater : MonoBehaviour
{
    // Start is called before the first frame update
    public AssistantMovementController mvmt;
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Location"))
        {
            mvmt.location = other.GameObject().name;
        }
    }
    // Update is called once per frame
    
}
