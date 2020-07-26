using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class lookAtScript : MonoBehaviour
{
    public GameObject Head;
    public GameObject Target;
   
    void Start()
    {
        
    }

    void Update()
    {
        Head.transform.LookAt(Target.transform);
    }
}
