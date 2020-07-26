using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class BasicServer : MonoBehaviour
{
    // network variables
    private Socket listener;
    private Socket clientSock;
    private Thread socketThread;
    private bool clientConnected = false;

    // messaging variables
    private byte[] bytes;
    private string msg = "";
    private int totalBytes = -1;
    private string obs;
    private string serverDebug = "";
    private List<string> actionQueue = new List<string>();
    private List<string> obsQueue = new List<string>();
    private bool serverSending = false;

    // Start is called before the first frame update
    void Start()
    {
        socketThread = new Thread(ServerCode);
        socketThread.IsBackground = true;
        socketThread.Start();
    }

	//private string getIPAddress()
 //   {
 //       IPHostEntry host;
 //       string localIP = "";
 //       host = Dns.GetHostEntry(Dns.GetHostName());
 //       foreach (IPAddress ip in host.AddressList)
 //       {
 //           if (ip.AddressFamily == AddressFamily.InterNetwork)
 //           {
 //               localIP = ip.ToString();
 //           }

 //       }
 //       return localIP;
 //   }

    private void ServerCode()
    {
        // start a local server for communication with the agent
        IPHostEntry iphost = Dns.GetHostEntry(Dns.GetHostName());
        IPAddress ipAddr = iphost.AddressList[0];
        IPEndPoint localEndpt = new IPEndPoint(ipAddr, 6566);
        listener = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        try
        {
            listener.Bind(localEndpt);
            serverDebug = "Server bound to local endpoint.\n";
            listener.Listen(5);
            serverDebug += "Server listening on port 6566.\n";
            clientSock = listener.Accept();
            serverDebug += "Server accepted incoming connection.";
            clientConnected = true;
            //sendObs = true;

            bytes = new byte[1024];
            //lastPos = Vector3.zero;

            while (true)
            {
                // Also, check for outgoing messages
                if (serverSending)
                {
                    if (obsQueue.Count > 0)
                    {
                        serverDebug += "Attempting to send command to client...\n";
                        // if there is an observation in the queue, go ahead and send it
                        clientSock.Send(UTF8Encoding.UTF8.GetBytes(obsQueue[0]));
                        obsQueue.RemoveAt(0);
                        serverDebug += "Sent message to client\n";
                        serverSending = false;
                    }
                }
                // NO NEED TO WORRY ABOUT RECEIVING MESSAGES FROM THE HL
                // IF NEEDED, UNCOMMENT THIS BELOW
                //else
                //{
                //    // poll for new messages
                //    serverDebug += "Attempting to receive action from client\n";
                //    totalBytes = clientSock.Receive(bytes);
                //    // serverDebug += "Received action from client\n";

                //    // sanity check for BIG packets
                //    if (totalBytes >= 1024)
                //    {
                //        serverDebug += "Unusually large packet received at " + totalBytes.ToString() + " bytes.\n";
                //    }
                //    else if (totalBytes != -1)
                //    {
                //        // received this message from the agent client
                //        msg = Encoding.UTF8.GetString(bytes, 0, totalBytes);

                //        // debug the incoming messages
                //        serverDebug += "Received msg: " + msg + "\n";

                //        // TODO - add the action to the queue and set the flag if its not already set
                //        actionQueue.Add(msg);
                //        serverSending = true;
                //    }
                //    totalBytes = -1;

                //    //Thread.Sleep(1);
                //}
            }
        }
        catch (System.Exception e)
        {
            serverDebug += e.ToString();
        }
    }

    private void OnDisable()
    {
        // shut down our socket connection
        // send the server ending message to the agent
        
        if (clientConnected)
        {
            listener.Send(UTF8Encoding.UTF8.GetBytes("server shutting down"));
            listener.Close();
        }
		if (socketThread != null)
			socketThread.Abort();
	}

    public void SendAgentCommand(string s)
    {
        obsQueue.Add(s);
        serverSending = true;
        Debug.Log("Sent message: '" + s + "' to client.");
    }

	public void ShutdownConnection()
	{
		if (clientConnected) {
			listener.Send(UTF8Encoding.UTF8.GetBytes("server shutting down"));
			listener.Close();
		}
		if (socketThread != null)
			socketThread.Abort();
	}

	private void Update()
	{
		if (serverDebug != "") {
			Debug.Log(serverDebug);
			serverDebug = "";
		}
	}
}
