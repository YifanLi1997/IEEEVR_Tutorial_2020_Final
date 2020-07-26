using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(Animator))]

public class IKControl : MonoBehaviour
{

    protected Animator animator;

    public bool ikActive = false;
    public bool lookAtBool = false;

    public Transform lookObj = null;


    void Start()
    {
        animator = GetComponent<Animator>();
        lookObj = GameObject.Find("Main Camera").transform;
    }
    private void Update()
    {
        if (!lookObj)
        {
            lookObj = GameObject.Find("Main Camera").transform;
        }
    }
    //a callback for calculating IK
    void OnAnimatorIK()
    {
        if (animator)
        {
            if (ikActive)
            {
                if (lookObj != null)
                {
                    for (int i = 0; i <= 1; i++)
                    {
                        animator.SetLookAtWeight(i);
                        animator.SetLookAtPosition(lookObj.position);
                    }
                }
            }
            else
            {
                for (int i = 1; i <= 0; i--)
                {
                    animator.SetLookAtWeight(i);
                }
            }
        }
    }

}

