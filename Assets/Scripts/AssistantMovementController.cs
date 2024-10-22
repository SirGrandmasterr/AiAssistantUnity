using System.Collections.Generic;
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
    private Vector3 _prevTarget;
    private Quaternion _prevRot;
    private int _prevState;



    private Queue<MovementQueueStruct> _movementqueue;
    public int movementState = 0;
    public int nextMovementState = 1;
    private GameObject picture;
    public Transform player;

    private GazeObject playerGaze;
    private Vector3 _heightOffset;

    private void Awake()
    {
        _movementqueue = new Queue<MovementQueueStruct>();
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
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Location"))
        {
            location = other.gameObject.name;
            print(" Entering " + other.GameObject().name);
        }
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
                agent.stoppingDistance = 2.2f;
                agent.SetDestination(player.position);
                if (Vector3.Distance(centrePoint.position, player.position) > 2)
                {
                    animator.Walk();
                }

                if (agent.remainingDistance <= 5)
                {
                    agent.speed = 1.5f;
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
            case 3: //Moving to a specific location or object
                agent.stoppingDistance = 0.5f;
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
                    if (_movementqueue.Count > 0)
                    {
                        var next = _movementqueue.Dequeue();
                        if (next.State == 3)
                        {
                            print("Setting Destination to "+ next.Obj.name);
                            agent.SetDestination(next.Obj.transform.position);
                        }
                        else
                        {
                            movementState = next.State;
                        }
                    }
                    
                }
                break;
        }
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
                
                Vector3 playerPosition = new Vector3(player.position.x, this.transform.position.y, player.position.z);
                transform.LookAt(playerPosition);
                break;
            }
            case true when playerGaze.Valid:
                //Assistant is following and will look where the player looks at.
                
                var gazePoint = new Vector3(playerGaze.ObjectOfInterest.transform.position.x, transform.position.y,
                    playerGaze.ObjectOfInterest.transform.position.z);
                transform.LookAt(gazePoint);
                head.LookAt(playerGaze.ObjectOfInterest.transform);
                


                break;
        }
    }
    
    private bool IsInside ( Collider c , Vector3 point )
    {
        Vector3 closest = c.ClosestPoint(point);
        // Because closest=point if point is inside - not clear from docs I feel
        return closest == point;
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
        _prevState = movementState;
        if (movementState == 1)
        {
            Idle();
            agent.SetDestination(agent.transform.position);
        } else if (movementState == 3)
        {
            _prevTarget = agent.destination;
            agent.SetDestination(agent.transform.position);
            Idle();
        }
        _prevRot = transform.rotation;
        _facePlayer = true;
    }

    public void DisableConversationBodyLanguage()
    {
        //transform.position = _prevPos;
        switch (_prevState)
        {
            case 0:
                break;
            case 1:
                movementState = _prevState;
                break;
            case 2:
                break;
            case 3:
                agent.SetDestination(_prevTarget);
                movementState = _prevState;
                break;
                
        }
        transform.rotation = _prevRot;
        _facePlayer = false;
    }

    public void UpdatePlayerGaze(GazeObject gaze)
    {
        playerGaze = gaze;
    }


    private struct MovementQueueStruct
    {
        public int State;
        public GameObject Obj;
    }
    public void Idle()
    {
        if (movementState == 3)
        {
            var mvmt = new MovementQueueStruct();
            mvmt.State = 0;
            _movementqueue.Enqueue(mvmt);
            return;
        }
        movementState = 0;
    }
    public void Patrol()
    {
        if (movementState == 3)
        {
            var mvmt = new MovementQueueStruct();
            mvmt.State = 1;
            _movementqueue.Enqueue(mvmt);
            return;
        }
        movementState = 1;
    }

    public void FollowVisitor()
    {
        if (movementState == 3)
        {
            var mvmt = new MovementQueueStruct();
            mvmt.State = 2;
            _movementqueue.Enqueue(mvmt);
            return;
        }
        movementState = 2;
    }

    public void Walk(GameObject destination)
    {
        if (movementState == 3)
        {
            var mvmt = new MovementQueueStruct();
            mvmt.State = 3;
            mvmt.Obj = destination;
            _movementqueue.Enqueue(mvmt);
            return;
        }
        agent.SetDestination(destination.transform.position);
        movementState = 3;
    }

    public void WalkForce(GameObject destination)
    {
        agent.SetDestination(destination.transform.position);
        movementState = 3;
    }

    public void WalkToPlayer()
    {
        if (movementState == 3)
        {
            var mvmt = new MovementQueueStruct();
            mvmt.State = 2;
            mvmt.Obj = player.GameObject();
            _movementqueue.Enqueue(mvmt);
            return;
        }
        agent.SetDestination(player.position);
        movementState = 3;
    }

    public void WalkToLocation(string sublocationName, int followupMovement)
    {
        var locationsObj = GameObject.Find("Sublocations");
        var found = false;
        //check smallest GameObject first
        foreach (Transform child in locationsObj.transform){
            if (child.name == sublocationName){
                Walk(child.GameObject());
                found = true;
            }
        }

        if (!found)
        {
            var picturesObj = GameObject.Find("Paintings");
            foreach (Transform child in picturesObj.transform){
                if (child.name == sublocationName){
                    Walk(child.GameObject());
                    found = true;
                }
            }
        }
        
        if (!found)
        {
            var picturesObj = GameObject.Find("Display Cases");
            foreach (Transform child in picturesObj.transform){
                if (child.name == sublocationName){
                    Walk(child.GameObject());
                    found = true;
                }
            }
        }
       
    }
}