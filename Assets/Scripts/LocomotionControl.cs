using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class LocomotionControl : MonoBehaviour
{
    private Animator remyAnim;
    private bool isWalking = false;

    public Transform pointA;
    public Transform pointB;
    private bool towardB = true;
    public float walkSpeed = 1f;
    public float proximity = 0.1f;
    private Vector3 targetPos;

    private NavMeshAgent remyAgent;

    private IKControl IkScript;

    // Start is called before the first frame update
    void Start()
    {
        remyAnim = GetComponent<Animator>();
        remyAgent = GetComponent<NavMeshAgent>();

        // move the POIs up to the height of our character for simplicity
        pointA.position = new Vector3(pointA.position.x, transform.position.y, pointA.position.z);
        pointB.position = new Vector3(pointB.position.x, transform.position.y, pointB.position.z);
        targetPos = pointB.position;

        //Linking to IK script to change the state of the IKActive Boolean
        IkScript = this.gameObject.GetComponent<IKControl>();
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.G))
        {
            if (!isWalking)
            {
                // play looping walking animation
                IkScript.ikActive = false;
                remyAnim.Play("Walking");
                isWalking = true;
                remyAgent.isStopped = false;
                remyAgent.SetDestination(targetPos);
            }
            else
            {
                // stop the walking behavior and animation
                IkScript.ikActive = true;
                isWalking = false;
                remyAnim.Play("Idle");
                remyAgent.isStopped = true;
            }
        }

        if(isWalking)
        {
            // check proximity to current target
            float dist = Vector3.Distance(targetPos, transform.position);
            if(dist < proximity)
            {
                // we have reached the target position, update new target
                if(towardB)
                {
                    towardB = false;
                    targetPos = pointA.position;
                    remyAgent.SetDestination(targetPos);
                }
                else
                {
                    towardB = true;
                    targetPos = pointB.position;
                    remyAgent.SetDestination(targetPos);
                    WalkingSignal();
                }
            }

            // look in the proper direction
            //transform.LookAt(targetPos);
            // move the VH forward smoothly
            //transform.Translate(0, 0, walkSpeed * Time.deltaTime);
        }
    }

    public void WalkingSignal()
    {
        if (!isWalking)
        {
            // play looping walking animation
            IkScript.ikActive = false;
            remyAnim.Play("Walking");
            isWalking = true;
            remyAgent.isStopped = false;
            remyAgent.SetDestination(targetPos);
        }
        else
        {
            // stop the walking behavior and animation
            IkScript.ikActive = true;
            isWalking = false;
            remyAnim.Play("Idle");
            remyAgent.isStopped = true;
        }
        
    }
}
