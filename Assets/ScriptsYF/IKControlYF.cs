using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class IKControlYF : MonoBehaviour
{
    protected Animator animator;

    public bool ikActive = false;
    public Transform lookObj; 

    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnAnimatorIK()
    {
        if (animator)
        {
            //Debug.Log("animator");
            if (ikActive)
            {
                //Debug.Log("ikActive");
                if (lookObj != null)
                {
                    //Debug.Log("lookObj");
                    for (int i = 0; i <= 1; i++)
                    {
                        animator.SetLookAtWeight(i);
                        animator.SetLookAtPosition(lookObj.position);
                    }
                }
            }
            else
            {
                for (int i = 1; i >= 0; i++)
                {
                    animator.SetLookAtWeight(i);
                }
            }
        }
    }
}
