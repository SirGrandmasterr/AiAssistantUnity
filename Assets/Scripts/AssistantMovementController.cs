using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

public class AssistantMovementController : MonoBehaviour
{
    public NavMeshAgent agent;
    public AssistantAnimationStateController animator;
    public Transform centrePoint;
    public float range = 50.0f;
    public string location;

    private Transform head;

    //ConversationBodyLanguage
    private bool _facePlayer;
    private Vector3 _prevPos;
    private Quaternion _prevRot;


    public int movementState = 0;
    private GameObject picture;
    public Transform player;

    private GazeObject playerGaze;
    private Vector3 _heightOffset;

    private void Awake()
    {
        _heightOffset = new Vector3(0f, 1.7f, 0);
        var transforms = GetComponentsInChildren<Transform>();

        foreach (var t in transforms)
        {
            if (t.name != "Head") continue;
            Debug.Log(t, t.gameObject);
            head = t;
        }

        picture = GameObject.Find("Floor_Display_Case 1 (1)");
        agent.speed = 0.8f;
        agent.angularSpeed = 180f;
        centrePoint = agent.transform;
        movementState = 0;
        _facePlayer = false;
    }

    // Update is called once per frame
    private void Update()
    {
        switch (movementState)
        {
            case 0: //idle
                animator.Idle();


                break;
            case 1: //patrolling
                agent.stoppingDistance = 0.5f;
                if (agent.remainingDistance <= agent.stoppingDistance)
                {
                    animator.Idle();
                    Vector3 point;
                    if (RandomPoint(centrePoint.position, range, out point))
                    {
                        agent.SetDestination(point);
                        animator.Walk();
                        print("Setting doneChill False");
                    }
                }

                break;
            case 2: //following player
                agent.stoppingDistance = 2.0f;
                agent.SetDestination(player.position);
                if (Vector3.Distance(centrePoint.position, player.position) > 2)
                {
                    animator.Walk();
                }

                if (agent.remainingDistance <= 5)
                {
                    agent.speed = 0.8f;
                    animator.Walk();
                }
                else
                {
                    agent.speed = 4.5f;
                    animator.Run(); //TODO
                }

                if (agent.remainingDistance <= agent.stoppingDistance)
                {
                    animator.Idle();
                }

                break;
        }
        /*// If we're patrolling, move to new random point when
        if (agent.remainingDistance <= agent.stoppingDistance)
        {
            animator.StopWalk();
            Vector3 point;
            if (RandomPoint(centrePoint.position, range, out point))
            {
                agent.SetDestination(point);
                animator.Walk();
                print("Setting doneChill False");
            }
        }*/
    }

    private void LateUpdate()
    {
        if (Vector3.Distance(centrePoint.position, player.position) < 4.5f)
        {
            //Assistant merely looks at player, but does not turn around.
            head.LookAt(player.position + _heightOffset);
        }

        switch (_facePlayer)
        {
            case true when !playerGaze.Valid:
            {
                //Assistant is not following
                Debug.Log(
                    "I should be facing the player. We are in Conversation, but the Player looks at me or no specific Artwork. ");
                Vector3 playerPosition = new Vector3(player.position.x, this.transform.position.y, player.position.z);
                transform.LookAt(playerPosition);
                break;
            }
            case true when playerGaze.Valid:
                //Assistant is following and will look where the player looks at.
                Debug.Log(
                    "I should be facing the player, but as I am following and the player gazes at Art, I should follow his gaze. ");
                var gazePoint = new Vector3(playerGaze.ObjectOfInterest.transform.position.x, transform.position.y,
                    playerGaze.ObjectOfInterest.transform.position.z);
                transform.LookAt(gazePoint);
                head.LookAt(playerGaze.ObjectOfInterest.transform);
                Debug.Log("I am looking at " + playerGaze.ObjectOfInterest.name);


                break;
        }
    }


    private bool RandomPoint(Vector3 center, float range, out Vector3 result)
    {
        var randomPoint = center + Random.insideUnitSphere * range; //random point in a sphere 
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomPoint, out hit, this.range,
                NavMesh.AllAreas)) //documentation: https://docs.unity3d.com/ScriptReference/AI.NavMesh.SamplePosition.html
        {
            //the 1.0f is the max distance from the random point to a point on the navmesh, might want to increase if range is big
            //or add a for loop like in the documentation
            result = hit.position;
            return true;
        }

        result = Vector3.zero;
        return false;
    }

    public void EnableConversationBodyLanguage()
    {
        //_prevPos = transform.position;
        _prevRot = transform.rotation;
        _facePlayer = true;
    }

    public void DisableConversationBodyLanguage()
    {
        //transform.position = _prevPos;
        transform.rotation = _prevRot;
        _facePlayer = false;
    }

    public void UpdatePlayerGaze(GazeObject gaze)
    {
        playerGaze = gaze;
    }
}