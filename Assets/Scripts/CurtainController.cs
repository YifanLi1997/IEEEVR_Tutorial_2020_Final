using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CurtainController : MonoBehaviour
{
    private bool curtainMoving = false;

    const float curtainMovingTriggerValue = 1.0f;
    public WindSensor windSensor;

    // Update is called once per frame
    void Update()
    {
        float windSpeed = windSensor.windSpeed;
        
        if (windSpeed > curtainMovingTriggerValue)
        {
            curtainMoving = true;
            MoveCurtain(windSpeed);
        }
        else
        {
            curtainMoving = false;
            StopCurtain();
        }
    }

    public void MoveCurtain(float windSpeed)
    {
        Vector3 windDirection = new Vector3 (1, 1, 0);  // Cloth externalAcceleration
        transform.GetComponent<Cloth>().externalAcceleration = (windDirection * windSpeed);;
    }

    public void StopCurtain()
    {
        transform.GetComponent<Cloth>().externalAcceleration = new Vector3(0, 0, 0);
    }
}
