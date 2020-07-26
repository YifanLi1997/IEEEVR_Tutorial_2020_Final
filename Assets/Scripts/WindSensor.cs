using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.IO.Ports;
using System.Threading;

public class WindSensor : MonoBehaviour
{
    public float windSpeed = 0.0f;
    
    SerialPort stream = new SerialPort("COM3", 9600);
    Thread serialReadThread = null;

    bool isWind = false;

    public void serialReadLine()
    {
        while (true)
        {
            windSpeed = float.Parse(stream.ReadLine());
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        stream.Open();

        serialReadThread = new Thread(new ThreadStart(serialReadLine));
        serialReadThread.IsBackground = true;

        serialReadThread.Start();
    }

    void Update()
    {
        Debug.Log(windSpeed);
    }

    public void OnApplicationQuit()
    {
        if (serialReadThread != null)
        {
            if (serialReadThread.IsAlive)
            {
                serialReadThread.Abort();
            }
        }

        if (stream != null)
        {
            if (stream.IsOpen)
            {
                print("closing serial port");
                stream.Close();
            }

            stream = null;
        }

    }
}
