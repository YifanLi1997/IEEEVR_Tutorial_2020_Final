using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class VHExpressionYF : MonoBehaviour
{
    public GameObject virtualHuman;
    private SkinnedMeshRenderer skinnedMesh;

    public int smileLimit = 80;
    public Slider frownSlider;

    // Start is called before the first frame update
    void Start()
    {
        skinnedMesh = virtualHuman.GetComponentInChildren<SkinnedMeshRenderer>();
        frownSlider.minValue = 0;
        frownSlider.maxValue = 100;
    }

    public void Frown() {
        skinnedMesh.SetBlendShapeWeight(13, frownSlider.value);
        skinnedMesh.SetBlendShapeWeight(14, frownSlider.value);
    }

    public void Smile() {
        StartCoroutine(SmileRoutine());
    }

    IEnumerator SmileRoutine()
    {
        StartCoroutine(ToSmile());
        yield return new WaitForSeconds(5.0f);
        StartCoroutine(ToNeutral());
    }

    // if we do not use Coroutine, the frame will only end after the finish of the for loop
    IEnumerator ToNeutral() {
        for (int i = smileLimit; i >= 0; i--)
        {
            skinnedMesh.SetBlendShapeWeight(39, i);
            skinnedMesh.SetBlendShapeWeight(40, i);
            yield return null;
        }
    }

    IEnumerator ToSmile()
    {
        for (int i = 0; i <= smileLimit; i++)
        {
            skinnedMesh.SetBlendShapeWeight(39, i);
            skinnedMesh.SetBlendShapeWeight(40, i);
            yield return null;
        }
    }
}
