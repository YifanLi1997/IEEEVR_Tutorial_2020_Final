using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class VHLocomotionYF : MonoBehaviour
{
    Animator animator;
    IKControlYF iKControl;
    public bool isWalking = false;

    public Transform pointA;
    public Transform pointB;
    private bool towardsB = true;
    public float walkSpeed = 1f;
    public float proximity = 0.1f;
    private Vector3 startPos;
    private Vector3 targetPos;
    private NavMeshAgent navMeshAgent;

    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        iKControl = GetComponent<IKControlYF>();

        startPos = new Vector3(pointA.position.x, transform.position.y, pointA.position.z);
        targetPos = new Vector3(pointB.position.x, transform.position.y, pointB.position.z);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            if (!isWalking)
            {
                iKControl.ikActive = false;
                animator.Play("Walking");
                isWalking = true;
                navMeshAgent.isStopped = false;
                navMeshAgent.SetDestination(targetPos);
            }
            else {
                iKControl.ikActive = true;
                isWalking = false;
                animator.Play("Idle");
                navMeshAgent.isStopped = true;
            }
            
        }

        if (isWalking)
        {
            float dist = Vector3.Distance(transform.position, targetPos);
            if (dist < proximity)
            {
                if (towardsB)
                {
                    towardsB = false;
                    targetPos = new Vector3(pointA.position.x, transform.position.y, pointA.position.z);
                    navMeshAgent.SetDestination(targetPos);
                }
                else
                {
                    towardsB = true;
                    targetPos = new Vector3(pointB.position.x, transform.position.y, pointB.position.z);
                    navMeshAgent.SetDestination(targetPos);
                }
            }
        }
        else
        {
            iKControl.ikActive = true;
            isWalking = false;
            animator.Play("Idle");
            navMeshAgent.isStopped = true;
        }

    }
}
