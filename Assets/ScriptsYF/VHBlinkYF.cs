using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VHBlinkYF : MonoBehaviour
{
    public GameObject virtualHuman;
    private SkinnedMeshRenderer skinnedMesh;

    public float openLim = 5f;
    public float closeLim = 0.05f;
    public int blendShapeVal = 100;
    public bool isOpen = true;

    private float openTimer = 0;

    // Start is called before the first frame update
    void Start()
    {
        openLim = Random.Range(4f,8f);
        skinnedMesh = virtualHuman.GetComponentInChildren<SkinnedMeshRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        if (isOpen)
        {
            openTimer += Time.deltaTime;
            if (openTimer > openLim)
            {
                isOpen = false;
                openTimer = 0;
            }
        }
        else {
            StartCoroutine(Blink());
        }
    }

    IEnumerator Blink() {
        StartCoroutine(CloseEye());
        yield return new WaitForSeconds(closeLim);
        StartCoroutine(OpenEye());
        openLim = Random.Range(4f,8f);
        isOpen = true;
    }

    IEnumerator CloseEye() {
        for (int i = 0; i <= blendShapeVal; i += blendShapeVal/4)
        {
            skinnedMesh.SetBlendShapeWeight(0, i);
            skinnedMesh.SetBlendShapeWeight(1, i);
            yield return null;
        }
    }

    IEnumerator OpenEye(){
        for (int i = blendShapeVal; i >= 0; i -= blendShapeVal/4)
        {
            skinnedMesh.SetBlendShapeWeight(0, i);
            skinnedMesh.SetBlendShapeWeight(1, i);
            yield return null;
        }
    }
}
