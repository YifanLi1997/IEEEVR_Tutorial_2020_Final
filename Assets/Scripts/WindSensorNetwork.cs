using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.IO.Ports;
using System.Threading;

public class WindSensorNetwork : MonoBehaviour
{
    public float windSpeed = 0.0f;
    public bool isServer = false;

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
        // Server
        if (GameObject.Find("NetworkManager").GetComponent<BasicServer>().enabled &&
            !GameObject.Find("NetworkManager").GetComponent<BasicClient>().enabled)
        {
            isServer = true;

            stream.Open();

            serialReadThread = new Thread(new ThreadStart(serialReadLine));
            serialReadThread.IsBackground = true;

            serialReadThread.Start();
        }
        else if (!GameObject.Find("NetworkManager").GetComponent<BasicServer>().enabled &&
             GameObject.Find("NetworkManager").GetComponent<BasicClient>().enabled)
        {
            isServer = false;
        }
    }

    void Update()
    {
        if (isServer)
        {
            string windValue = "Wind ";
            if (windSpeed > 1.0f && !isWind)
            {
                GameObject.Find("NetworkManager").GetComponent<BasicServer>().SendAgentCommand(windValue + windSpeed.ToString());
                isWind = true;
            }

            if (windSpeed < 1.0f && isWind)
            {
                GameObject.Find("NetworkManager").GetComponent<BasicServer>().SendAgentCommand(windValue + "0");
                isWind = false;
            }
        }

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
