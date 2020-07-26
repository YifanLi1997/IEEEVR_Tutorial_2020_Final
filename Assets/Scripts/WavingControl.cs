using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WavingControl : MonoBehaviour
{
    public Animator remyAnim;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.W))
        {
            // play the waving animation
            remyAnim.Play("Waving");
            Debug.Log("User pressed 'w'");
        }
    }

    public void WaveSignal()
    {
        // play the waving animation
        remyAnim.Play("Waving");
        Debug.Log("User pressed 'w'");
    }
}
