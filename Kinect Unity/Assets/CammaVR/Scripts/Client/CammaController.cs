// Copyright (c) 2017 Maxime Coutté-Péroumal Corne

using UnityEngine;
using System.Threading;
using System.IO.Ports;

/**
 * This class allows a Unity program to continually check for messages from a
 * camma device.
 *
 * It creates a Thread that communicates with the camma port and continually
 * polls the messages on the wire.
 * That Thread puts all the messages inside a Queue, and this CammaController
 * class polls that queue by means of invoking CammaThread.GetCammaMessage().
 *
 * The camma device must send its messages separated by a newline character.
 * Neither the CammaController nor the CammaThread perform any validation
 * on the integrity of the message. It's up to the one that makes sense of the
 * data.
 */
public class CammaController : MonoBehaviour
{
    public GameObject messageListener;
    public int reconnectionDelay = 1000;
    public int maxUnreadMessages = 1;
    // Constants used to mark the start and end of a connection. There is no
    // way you can generate clashing messages from your camma device, as I
    // compare the references of these strings, no their contents. So if you
    // send these same strings from the camma device, upon reconstruction they
    // will have different reference ids.
    public const string CAMMA_DEVICE_CONNECTED = "__Connected__";
    public const string CAMMA_DEVICE_DISCONNECTED = "__Disconnected__";

    // Internal reference to the Thread and the object that runs in it.
    protected Thread thread;
    protected CammaThreadLines cammaThread;


    // ------------------------------------------------------------------------
    // Invoked whenever the CammaController gameobject is activated.
    // It creates a new thread that tries to connect to the camma device
    // and start reading from it.
    // ------------------------------------------------------------------------
	public string CammaFindServer() {
		SerialPort tmp;
		foreach (string str in SerialPort.GetPortNames()) {
			tmp = new SerialPort (str);
			if (tmp.IsOpen == false) {

				tmp.PortName = str;

				try {
					//open serial port
					print(str);
					tmp.Open ();
					tmp.BaudRate = 1843200;
					tmp.ReadTimeout = 200;
					string s = tmp.ReadLine();
					if (s != "") {
						print ("success " + str); 
						tmp.Close();
						return str;
					} else {
						print(s);
						tmp.Close ();
					}
				} catch {

				}
			}
		}
		return "";
	}

    void OnEnable()
	{
		string portName = CammaFindServer();
        int baudRate = 1843200;
		cammaThread = new CammaThreadLines (portName, 
				baudRate, 
				reconnectionDelay,
				maxUnreadMessages);
			thread = new Thread (new ThreadStart (cammaThread.RunForever));
			thread.Start ();
		}


    // ------------------------------------------------------------------------
    // Invoked whenever the CammaController gameobject is deactivated.
    // It stops and destroys the thread that was reading from the camma device.
    // ------------------------------------------------------------------------
    void OnDisable()
    {
        // If there is a user-defined tear-down function, execute it before
        // closing the underlying COM port.
        if (userDefinedTearDownFunction != null)
            userDefinedTearDownFunction();

        // The cammaThread reference should never be null at this point,
        // unless an Exception happened in the OnEnable(), in which case I've
        // no idea what face Unity will make.
        if (cammaThread != null)
        {
            cammaThread.RequestStop();
            cammaThread = null;
        }

        // This reference shouldn't be null at this point anyway.
        if (thread != null)
        {
            thread.Join();
            thread = null;
        }
    }

    // ------------------------------------------------------------------------
    // Polls messages from the queue that the CammaThread object keeps. Once a
    // message has been polled it is removed from the queue. There are some
    // special messages that mark the start/end of the communication with the
    // device.
    // ------------------------------------------------------------------------
    void Update()
    {
        // If the user prefers to poll the messages instead of receiving them
        // via SendMessage, then the message listener should be null.
        if (messageListener == null)
            return;

        // Read the next message from the queue
        string message = (string)cammaThread.ReadMessage();
        if (message == null)
            return;

        // Check if the message is plain data or a connect/disconnect event.
        if (ReferenceEquals(message, CAMMA_DEVICE_CONNECTED))
            messageListener.SendMessage("OnConnectionEvent", true);
        else if (ReferenceEquals(message, CAMMA_DEVICE_DISCONNECTED))
            messageListener.SendMessage("OnConnectionEvent", false);
        else
            messageListener.SendMessage("OnMessageArrived", message);
    }

    // ------------------------------------------------------------------------
    // Returns a new unread message from the cammma device. You only need to
    // call this if you don't provide a message listener.
    // ------------------------------------------------------------------------
    public string ReadCammaMessage()
    {
        // Read the next message from the queue
        return (string)cammaThread.ReadMessage();
    }

    // ------------------------------------------------------------------------
    // Puts a message in the outgoing queue. The thread object will send the
    // message to the camma device when it considers it's appropriate.
    // ------------------------------------------------------------------------
    public void SendCammaMessage(string message)
    {
        cammaThread.SendMessage(message);
    }

    // ------------------------------------------------------------------------
    // Executes a user-defined function before Unity closes the COM port, so
    // the user can send some tear-down message to the hardware reliably.
    // ------------------------------------------------------------------------
    public delegate void TearDownFunction();
    private TearDownFunction userDefinedTearDownFunction;
    public void SetTearDownFunction(TearDownFunction userFunction)
    {
        this.userDefinedTearDownFunction = userFunction;
    }
		}