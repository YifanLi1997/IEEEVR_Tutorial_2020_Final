using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RogoDigital.Lipsync;

public class VHSpeechYF : MonoBehaviour
{
    private LipSync ls;
    public GameObject virtualHuman;
    public LipSyncData[] lsAudios;

    // Start is called before the first frame update
    void Start()
    {
        ls = virtualHuman.GetComponent<LipSync>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void sayHello() {
        ls.Play(lsAudios[0]);
    }

    public void sayHowRU()
    {
        ls.Play(lsAudios[1]);
    }

    public void sayIntroduction()
    {
        ls.Play(lsAudios[2]);
    }
}
