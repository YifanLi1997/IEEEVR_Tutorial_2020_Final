using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using UnityEngine;

public class BasicClient : MonoBehaviour
{
    // network variables
    public string serverIP;
    private Socket listener;
    private Socket serverSock;
    private Thread socketThread;
    private bool clientConnected = false;

    // messaging variables
    private byte[] bytes;
    private string msg = "";
    private int totalBytes = -1;
    private string obs;
    private string clientDebug = "";
    private List<string> actionQueue = new List<string>();
    //private List<string> obsQueue = new List<string>();
    //private bool serverSending = false;

    // here are the links to other scripts
    public WavingControl waveScript;
    public LocomotionControl locoScript;
	// add in the blinking/emotion/talking script links here
	public VHSpeech speechScript;
	public VHExpression expressionScript;
    public WindSensorNetwork windSensor;

    //public float windValue = 0.0f;

    // Start is called before the first frame update
    void Start()
    {
        // set up a connection to the server
        socketThread = new Thread(ClientCode);
        socketThread.IsBackground = true;
        socketThread.Start();
    }

    private void ClientCode()
    {
		IPHostEntry myHost = Dns.GetHostEntry(Dns.GetHostName());
		IPAddress ipAddr = myHost.AddressList[0];
        IPEndPoint nonlocalEndpt = new IPEndPoint(ipAddr, 6566);
        listener = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        try
        {
            listener.Connect(nonlocalEndpt);
            //listener.Bind(nonlocalEndpt);
            clientDebug = "Client connected to remote endpoint.\n";
            //listener.Listen(5);
            clientDebug += "Server listening on port 6566.\n";
            //serverSock = listener.Accept();
            clientDebug += "Server accepted incoming connection.";
            clientConnected = true;
            //sendObs = true;
            bytes = new byte[1024];

            // main loop of the thread. collect commands from the server
            while (true)
            {
                // Also, check for outgoing messages
                //if (serverSending)
                //{
                //    if (obsQueue.Count > 0)
                //    {
                //        serverDebug += "Attempting to send command to client...\n";
                //        // if there is an observation in the queue, go ahead and send it
                //        clientSock.Send(UTF8Encoding.UTF8.GetBytes(obsQueue[0]));
                //        obsQueue.RemoveAt(0);
                //        serverDebug += "Sent message to client\n";
                //        serverSending = false;
                //    }
                //}
                // poll for new messages
                clientDebug += "Attempting to receive command from server\n";
                totalBytes = listener.Receive(bytes);
                // serverDebug += "Received action from client\n";

                // sanity check for BIG packets
                if (totalBytes >= 1024)
                {
                    clientDebug += "Unusually large packet received at " + totalBytes.ToString() + " bytes.\n";
                }
                else if (totalBytes != -1)
                {
                    // received this message from the agent client
                    msg = Encoding.UTF8.GetString(bytes, 0, totalBytes);

                    // debug the incoming messages
                    clientDebug += "Received msg: " + msg + "\n";

                    // add the action to the queue and set the flag if its not already set
                    actionQueue.Add(msg);
                    //serverSending = true;

                }
                totalBytes = -1;

                //Thread.Sleep(1);
            }
        }
        catch (System.Exception e)
        {
            clientDebug += e.ToString();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if(actionQueue.Count > 0)
        {
            // add in all cases for the server commands here
            // connect appropriate scripts to the logic
            if(actionQueue[0] == "Walk")
            {
                locoScript.WalkingSignal();
            }
            else if (actionQueue[0] == "Wave")
            {
                waveScript.WaveSignal();
            }
            else if (actionQueue[0] == "SayHello")
            {
				speechScript.sayHello();
            }
            else if (actionQueue[0] == "SayHowAreYou")
            {
				speechScript.howRU();
            }
			else if (actionQueue[0] == "SayIntro") {
				speechScript.introduce();
			}
			else if (actionQueue[0] == "Smile") {
				expressionScript.smileFunction();
			}
			else if (actionQueue[0] == "Greet") {
				speechScript.sayHello();
				expressionScript.smileFunction();
			}
			else if (actionQueue[0] == "Frown") {
				expressionScript.toFrown();
			}
            else if (actionQueue[0].Contains("Wind"))
            {
                windSensor.windSpeed = float.Parse(actionQueue[0].Split()[1]);
            }
            else
            {
                Debug.Log("Invalid command received from the server: '" + actionQueue[0] + "'...");

            }
            actionQueue.RemoveAt(0);
        }
        if(clientDebug != "")
        {
            Debug.Log(clientDebug);
            clientDebug = "";
        }
    }
}

