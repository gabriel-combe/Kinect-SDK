// Copyright (c) 2017 Maxime Coutté-Péroumal Corne

using UnityEngine;
using System.Threading;

/**
 * While 'cammaController' only allows reading/sending text data that is
 * terminated by new-lines, this class allows reading/sending messages 
 * using a binary protocol where each message is separated from the next by 
 * a 1-char delimiter.
 */
public class CammaControllerCustomDelimiter : MonoBehaviour
{
    [Tooltip("Port name with which the CammaPort object will be created.")]
    public string portName = "COM3";

    [Tooltip("Baud rate that the cammma device is using to transmit data.")]
    public int baudRate = 9600;

    [Tooltip("Reference to an scene object that will receive the events of connection, " +
             "disconnection and the messages from the camma device.")]
    public GameObject messageListener;

    [Tooltip("After an error in the camma communication, or an unsuccessful " +
             "connect, how many milliseconds we should wait.")]
    public int reconnectionDelay = 1000;

    [Tooltip("Maximum number of unread data messages in the queue. " +
             "New messages will be discarded.")]
    public int maxUnreadMessages = 1;

    [Tooltip("Maximum number of unread data messages in the queue. " +
             "New messages will be discarded.")]
    public byte separator = 90;

    // Internal reference to the Thread and the object that runs in it.
    protected Thread thread;
    protected CammaThreadBinaryDelimited cammaThread;


    // ------------------------------------------------------------------------
    // Invoked whenever the CammaController gameobject is activated.
    // It creates a new thread that tries to connect to the camma device
    // and start reading from it.
    // ------------------------------------------------------------------------
    void OnEnable()
    {
        cammaThread = new CammaThreadBinaryDelimited(portName,
                                                       baudRate,
                                                       reconnectionDelay,
                                                       maxUnreadMessages,
                                                       separator);
        thread = new Thread(new ThreadStart(cammaThread.RunForever));
        thread.Start();
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
        byte[] message = ReadCammaMessage();
        if (message == null)
            return;

        // Check if the message is plain data or a connect/disconnect event.
        messageListener.SendMessage("OnMessageArrived", message);
    }

    // ------------------------------------------------------------------------
    // Returns a new unread message from the camma device. You only need to
    // call this if you don't provide a message listener.
    // ------------------------------------------------------------------------
    public byte[] ReadCammaMessage()
    {
        // Read the next message from the queue
        return (byte[]) cammaThread.ReadMessage();
    }

    // ------------------------------------------------------------------------
    // Puts a message in the outgoing queue. The thread object will send the
    // message to the camma device when it considers it's appropriate.
    // ------------------------------------------------------------------------
    public void SendCammaMessage(byte[] message)
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
