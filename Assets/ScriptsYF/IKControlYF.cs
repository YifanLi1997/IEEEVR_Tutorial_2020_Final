using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class IKControlYF : MonoBehaviour
{
    protected Animator animator;

    public bool ikActive = false;
    public Transform lookObj; 

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    private void OnAnimatorIK()
    {
        if (animator){
            if (ikActive){
                if (lookObj != null){
                    for (int i = 0; i <= 1; i++)
                    {
                        animator.SetLookAtWeight(i);
                        animator.SetLookAtPosition(lookObj.position);
                    }
                }
            }
            else{
                for (int i = 1; i >= 0; i--){
                    animator.SetLookAtWeight(i);
                }
            }
        }
    }
}
