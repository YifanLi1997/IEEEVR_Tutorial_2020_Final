using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class VHExpression : MonoBehaviour
{

    private SkinnedMeshRenderer smr;
    private int smileLimit = 80;

    public Slider frownSlider;
    void Start()
    {
        smr = GameObject.Find("Body").GetComponent<SkinnedMeshRenderer>();
        frownSlider.minValue = 0;
        frownSlider.maxValue = 100;
    }

    void Update()
    {
        if (!smr) {
            smr = GameObject.Find("Body").GetComponent<SkinnedMeshRenderer>();
        }
    }
    //smile blendshapes are 39 and 40
    IEnumerator toSmile() {
        for (int i = 0; i <= smileLimit; i++) {
            smr.SetBlendShapeWeight(39, i);
            smr.SetBlendShapeWeight(40, i);
            yield return null;
        }
    }
    IEnumerator toNeutral()
    {
        for (int i = smileLimit; i >= 0; i--) {
            smr.SetBlendShapeWeight(39, i);
            smr.SetBlendShapeWeight(40, i);
            yield return null;
        }
    }
    IEnumerator smileRoutine()
    {
        yield return new WaitForSeconds(2.0f);
        StartCoroutine(toSmile());
        yield return new WaitForSeconds(5.0f);
        StartCoroutine(toNeutral());
    }
    public void smileFunction()
    {
        StartCoroutine(smileRoutine());
    }

    //frwon blenshapes 13 and 14
    public void toFrown() {
        smr.SetBlendShapeWeight(13,frownSlider.value);
        smr.SetBlendShapeWeight(14,frownSlider.value);
    }

}
