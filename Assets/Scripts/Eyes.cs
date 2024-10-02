using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

public class Eyes : MonoBehaviour
{
    [FormerlySerializedAs("Brain")] public Brain brain;

    [FormerlySerializedAs("Player")] public GameObject player;
    public GameObject playerVision;

    [SerializeField] private LayerMask artlayer;

    public Transform head;

    private Vector3 _heightOffset;
    // Start is called before the first frame update

    public float conversationTimer; // how long conversation is considered to be ongoing after losing eye contact


    private float ConversationTimeout = 3f; // after breaking eye contact, conversation mode stays active for this many more seconds. 
    private float ConversationEngageDistance = 2.5f;
    private float ConversationUpkeepDistance = 3f;

    private Vector3 debug_hit;

    private void Awake()
    {
        _heightOffset = new Vector3(0f, 1.7f, 0);
        var transforms = GetComponentsInChildren<Transform>();

        foreach (var t in transforms)
        {
            if (t.name != "Head") continue;
            head = t;
        }

        StartCoroutine(UpdatePlayerVisibility());
        StartCoroutine(UpdateVisibleObjectsOfInterest());
    }

    // Update is called once per frame
    void Update()
    {
    }

    private float CheckAssistantPlayerDistance()
    {
        return Vector3.Distance(transform.position + _heightOffset, player.transform.position + _heightOffset);
    }

    private float CheckPlayerVisionAngle()
    {
        return Vector3.Angle(head.right, player.transform.forward);
    }


    // o----(Assistant)----o T
    //     \85°      95°/    | Distance
    //      \          /     |
    //        \       /      |
    //          \   /        |
    //          Player       |
    //                       _     
    // For Conversationmode, two things need to happen: 
    // 1 => There needs to be a minimum distance to the assistant.
    // 2 => There needs to be eye contact.
    // OR => There needs to be a minimum distance to the assistant & both of you stare at a piece of Art.
    private bool CheckInConversation()
    {
        var val = CheckPlayerVisionAngle();
        var dist = CheckAssistantPlayerDistance();

        if (!brain.PlayerInConversation)
        {
            return (val is <= 95f and >= 85f) && (dist < ConversationEngageDistance);
        }

        return (dist < ConversationUpkeepDistance);

    }

    private IEnumerator UpdatePlayerVisibility()
    {
        while (true)
        {
            //Check Visibility not every frame, but every 0.2 seconds. Same for Conversation state. 
            yield return new WaitForSeconds(0.2f);
            CheckVisibility();
            if (brain.PlayerVisible)
            {
                brain.UpdatePlayerGaze(CheckPlayerGaze(), true);
               // if (brain.PlayerGaze.ObjectOfInterest != null) Debug.Log(brain.PlayerGaze.ObjectOfInterest.name);
                if (CheckInConversation())
                {
                    
                    conversationTimer = ConversationTimeout; // Refresh Conversation-stop timer, to allow looking away for a few seconds while still staying in conversation. 
                    brain.UpdateConversationStatus(true);
                }
            }
            else brain.UpdatePlayerGaze(new GazeObject() { Valid = false }, false);
            

            if (!brain.PlayerInConversation || !(conversationTimer > 0))
                continue; //If player is in conversation state, tick down the timer to when he stops.
            conversationTimer -= 0.2f;
            if (conversationTimer <= 0)
            {
                brain.UpdateConversationStatus(false);
            }
        }
    }


    private IEnumerator UpdateVisibleObjectsOfInterest()
    {
        while (true)
        {
            //Update Asset Array not every frame, but every 0.2 seconds. 
            yield return new WaitForSeconds(0.2f);
            brain.AssetsInView = CheckVisibleObjectsOfInterest();
        }
    }


    private bool CheckVisibility()
    {
        if (!Physics.Raycast(transform.position + _heightOffset,
                Vector3.Normalize((player.transform.position + _heightOffset) - (transform.position + _heightOffset)),
                out var hit, Mathf.Infinity))
        {
            return false;
        }
        brain.UpdateVisibility(hit.transform.name == "Capsule");
        return true;
    }

    private String[] CheckVisibleObjectsOfInterest()
    {
        var assetsInView = new List<string>();
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 8f, artlayer);
        //print("Checking.");
        foreach (var hitCollider in hitColliders)
        {
            //print(hitCollider.transform.parent.name);
            assetsInView.Add(hitCollider.transform.parent.name);
        }

        return assetsInView.ToArray();
    }

    private void OnDrawGizmos()
    {
        //Gizmos.color = Color.yellow;
        //Gizmos.DrawSphere(debug_hit, 0.5f);
    }

    private GazeObject CheckPlayerGaze()
    {
        
        if (!Physics.Raycast(playerVision.transform.position,playerVision.transform.forward,
                out var hit, Mathf.Infinity)) return new GazeObject() { Valid = false };
        debug_hit = hit.point;
        if (!(Vector3.Distance(player.transform.position, hit.point) <= 8f))
        {
            return new GazeObject(){Valid = false};
        }
        var hitColliders = Physics.OverlapSphere(hit.point, 0.2f, artlayer);
        if (hitColliders.Length <= 0)
        {
            Debug.Log("No Hitcolliders.");
            return new GazeObject() { Valid = false };
        }

        Collider closest;
        closest = hitColliders[0];
        var distance = Vector3.Distance(closest.transform.position, hit.point);
        foreach (var collider1 in hitColliders)
        {
            var newDistance = Vector3.Distance(collider1.transform.position, hit.point);
            if (!(newDistance <= distance)) continue;
            distance = newDistance;
            closest = collider1;
        }
        Debug.Log(closest.transform.parent.name);
        return new GazeObject() { Valid = true, ObjectOfInterest = closest.GameObject().transform.parent.GameObject(), };
    } 
    
}

public struct GazeObject
{
    public bool Valid;
    [CanBeNull] public GameObject ObjectOfInterest;
    
}