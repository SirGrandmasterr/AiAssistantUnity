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
        if (Input.GetKeyDown("1"))
        {
            print("Pressed 1");
            Idle();
        }
        if (Input.GetKeyDown("2"))
        {
            print("Pressed 2");
            Walk();
        }
        if (Input.GetKeyDown("1"))
        {
            print("Pressed 3");
            Run();
        }
    }
    public void Idle()
    {
        animator.ResetTrigger("WalkTrigger");
        animator.ResetTrigger("RunTrigger");
        animator.SetTrigger("IdleTrigger");
         
    }
    public void Walk()
    {   animator.ResetTrigger("IdleTrigger");
        animator.ResetTrigger("RunTrigger");
        animator.SetTrigger("WalkTrigger");
    }

   
    public void Run()
    {
        animator.ResetTrigger("WalkTrigger");
        animator.ResetTrigger("IdleTrigger");
        animator.SetTrigger("RunTrigger");
    }
    
}