using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RogoDigital.Lipsync;
public class VHSpeech : MonoBehaviour
{
    private LipSync ls;
    public LipSyncData[] lsAudio;
    void Start()
    {
        ls = GameObject.Find("remy").GetComponent<LipSync>();
    }


    void Update()
    {
        if (!ls) {
            ls = GameObject.Find("Remy").GetComponent<LipSync>();
        }
    }

    public void sayHello()
    {
        ls.Play(lsAudio[0]);
    }

    public void howRU() {
        ls.Play(lsAudio[1]);
    }

    public void introduce() {
        ls.Play(lsAudio[2]);
    }
}
