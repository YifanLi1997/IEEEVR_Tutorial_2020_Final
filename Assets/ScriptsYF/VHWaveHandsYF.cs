using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VHWaveHandsYF : MonoBehaviour
{

    Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.W))
        {
            animator.Play("Waving"); // bug: the transition is not smooth if played in this way
        }
    }
}
