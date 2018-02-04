// Copyright (c) 2017 Maxime Coutté-Péroumal Corne

using UnityEngine;

using System;
using System.IO;
using System.IO.Ports;
using System.Collections;
using System.Threading;

/**
 * This class contains methods that must be run from inside a thread and others
 * that must be invoked from Unity. Both types of methods are clearly marked in
 * the code, although you, the final user of this library, don't need to even
 * open this file unless you are introducing incompatibilities for upcoming
 * versions.
 */
public abstract class AbstractCammaThread
{
    // Parameters passed from CammaController, used for connecting to the
    // camma device as explained in the CammaController documentation.
	private string portName;
    private int baudRate = 115200;
    private int delayBeforeReconnecting;
    private int maxUnreadMessages;

    // Object from the .Net framework used to communicate with camma devices.
    private SerialPort cammaPort;

    // Amount of milliseconds alloted to a single read or connect. An
    // exception is thrown when such operations take more than this time
    // to complete.
    private const int readTimeout = 100;

    // Amount of milliseconds alloted to a single write. An exception is thrown
    // when such operations take more than this time to complete.
    private const int writeTimeout = 100;

    // Internal synchronized queues used to send and receive messages from the
    // camma device. They serve as the point of communication between the
    // Unity thread and the CammaVR thread.
    private Queue inputQueue, outputQueue;

    // Indicates when this thread should stop executing. When CammaController
    // invokes 'RequestStop()' this variable is set.
    private bool stopRequested = false;

    private bool enqueueStatusMessages = false;


    /**************************************************************************
     * Methods intended to be invoked from the Unity thread.
     *************************************************************************/

    // ------------------------------------------------------------------------
    // Constructs the thread object. This object is not a thread actually, but
    // its method 'RunForever' can later be used to create a real Thread.
    // ------------------------------------------------------------------------
    public AbstractCammaThread(string portName,
                                int baudRate,
                                int delayBeforeReconnecting,
                                int maxUnreadMessages,
                                bool enqueueStatusMessages)
    {
        this.portName = portName;
        this.baudRate = baudRate;
        this.delayBeforeReconnecting = delayBeforeReconnecting;
        this.maxUnreadMessages = maxUnreadMessages;
        this.enqueueStatusMessages = enqueueStatusMessages;

        inputQueue = Queue.Synchronized(new Queue());
        outputQueue = Queue.Synchronized(new Queue());
    }

    // ------------------------------------------------------------------------
    // Invoked to indicate to this thread object that it should stop.
    // ------------------------------------------------------------------------
    public void RequestStop()
    {
        lock (this)
        {
            stopRequested = true;
        }
    }

    // ------------------------------------------------------------------------
    // Polls the internal message queue returning the next available message
    // in a generic form. This can be invoked by subclasses to change the
    // type of the returned object.
    // It returns null if no message has arrived since the latest invocation.
    // ------------------------------------------------------------------------
    public object ReadMessage()
    {
        if (inputQueue.Count == 0)
            return null;

        return inputQueue.Dequeue();
    }

    // ------------------------------------------------------------------------
    // Schedules a message to be sent. It writes the message to the
    // output queue, later the method 'RunOnce' reads this queue and sends
    // the message to the camma device.
    // ------------------------------------------------------------------------
    public void SendMessage(object message)
    {
        outputQueue.Enqueue(message);
    }


    /**************************************************************************
     * Methods intended to be invoked from the CammaVR thread (the one
     * created by the CammaController).
     *************************************************************************/

    // ------------------------------------------------------------------------
    // Enters an almost infinite loop of attempting connection to the camma
    // device, reading messages and sending messages. This loop can be stopped
    // by invoking 'RequestStop'.
    // ------------------------------------------------------------------------
    public void RunForever()
    {
        // This 'try' is for having a log message in case of an unexpected
        // exception.
        try
        {
            while (!IsStopRequested())
            {
                try
                {
                    AttemptConnection();
                    // Enter the semi-infinite loop of reading/writing to the
                    // device.
                    while (!IsStopRequested())
                        RunOnce();
                }
                catch (Exception ioe)
                {
                    // A disconnection happened, or there was a problem
                    // reading/writing to the device. Log the detailed message
                    // to the console and notify the listener.
                    Debug.LogWarning("Exception: " + ioe.Message + " StackTrace: " + ioe.StackTrace);
					Debug.LogWarning("portName" + portName);
                    if (enqueueStatusMessages)
                        inputQueue.Enqueue(CammaController.CAMMA_DEVICE_DISCONNECTED);

                    // As I don't know in which stage the CammaPort threw the
                    // exception I call this method that is very safe in
                    // disregard of the port's status
                    CloseDevice();

                    // Don't attempt to reconnect just yet, wait some
                    // user-defined time. It is OK to sleep here as this is not
                    // Unity's thread, this doesn't affect frame-rate
                    // throughput.
                    Thread.Sleep(delayBeforeReconnecting);
                }
            }

            // Before closing the COM port, give the opportunity for all messages
            // from the output queue to reach the other endpoint.
            while (outputQueue.Count != 0)
            {
                SendToWire(outputQueue.Dequeue(), cammaPort);
            }

            // Attempt to do a final cleanup. This method doesn't fail even if
            // the port is in an invalid status.
            CloseDevice();
        }
        catch (Exception e)
        {
            Debug.LogError("Unknown exception: " + e.Message + " " + e.StackTrace);
        }
    }

    // ------------------------------------------------------------------------
    // Try to connect to the camma device. May throw IO exceptions.
    // ------------------------------------------------------------------------
    private void AttemptConnection()
    {
        cammaPort = new SerialPort(portName, baudRate);
        cammaPort.ReadTimeout = readTimeout;
        cammaPort.WriteTimeout = writeTimeout;
        cammaPort.Open();

        if (enqueueStatusMessages)
            inputQueue.Enqueue(CammaController.CAMMA_DEVICE_CONNECTED);
    }

    // ------------------------------------------------------------------------
    // Release any resource used, and don't fail in the attempt.
    // ------------------------------------------------------------------------
    private void CloseDevice()
    {
        if (cammaPort == null)
            return;

        try
        {
            cammaPort.Close();
        }
        catch (IOException)
        {
            // Nothing to do, not a big deal, don't try to cleanup any further.
        }

        cammaPort = null;
    }

    // ------------------------------------------------------------------------
    // Just checks if 'RequestStop()' has already been called in this object.
    // ------------------------------------------------------------------------
    private bool IsStopRequested()
    {
        lock (this)
        {
            return stopRequested;
        }
    }

    // ------------------------------------------------------------------------
    // A single iteration of the semi-infinite loop. Attempt to read/write to
    // the camma device. If there are more lines in the queue than we may have
    // at a given time, then the newly read lines will be discarded. This is a
    // protection mechanism when the port is faster than the Unity progeram.
    // If not, we may run out of memory if the queue really fills.
    // ------------------------------------------------------------------------
    private void RunOnce()
    {
        try
        {
            // Send a message.
            if (outputQueue.Count != 0)
            {
                SendToWire(outputQueue.Dequeue(), cammaPort);
            }

            // Read a message.
            // If a line was read, and we have not filled our queue, enqueue
            // this line so it eventually reaches the Message Listener.
            // Otherwise, discard the line.
            object inputMessage = ReadFromWire(cammaPort);
            if (inputMessage != null)
            {
                if (inputQueue.Count < maxUnreadMessages)
                {
                    inputQueue.Enqueue(inputMessage);
                }
                else
                {

                }
            }
        }
        catch (TimeoutException)
        {
            // This is normal, not everytime we have a report from the camma device
        }
    }

    // ------------------------------------------------------------------------
    // Sends a message through the cammaPort.
    // ------------------------------------------------------------------------
    protected abstract void SendToWire(object message, SerialPort cammaPort);

    // ------------------------------------------------------------------------
    // Reads and returns a message from the Camma port.
    // ------------------------------------------------------------------------
	protected abstract object ReadFromWire(SerialPort cammaPort);
}
