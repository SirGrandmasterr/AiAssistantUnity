using UnityEngine;

public class AssistantAnimationStateController : MonoBehaviour
{
    private static readonly int IsWalking = Animator.StringToHash("isWalking");
    private Animator animator;
    private static readonly int IdleWalkRun = Animator.StringToHash("IdleWalkRun");


    // Start is called before the first frame update
    private void Start()
    {
        animator = GetComponent<Animator>();
        Debug.Log(animator);
    }

    // Update is called once per frame
    private void Update()
    {
    }
    public void Idle()
    {
        animator.SetInteger(IdleWalkRun, 0);   
    }
    public void Walk()
    {
        animator.SetInteger(IdleWalkRun, 1);
    }

   
    public void Run()
    {
        animator.SetInteger(IdleWalkRun, 2);
    }
    
}