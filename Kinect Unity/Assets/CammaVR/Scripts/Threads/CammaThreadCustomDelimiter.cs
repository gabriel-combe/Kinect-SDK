// Copyright (c) 2017 Maxime Coutté-Péroumal Corne

using UnityEngine;

using System.IO.Ports;

/**
 * This class contains methods that must be run from inside a thread and others
 * that must be invoked from Unity. Both types of methods are clearly marked in
 * the code, although you, the final user of this library, don't need to even
 * open this file unless you are introducing incompatibilities for upcoming
 * versions.
 * 
 * For method comments, refer to the base class.
 */
public class CammaThreadBinaryDelimited : AbstractCammaThread
{
    // Messages to/from the camma port should be delimited using this separator.
    private byte separator;
    // Buffer where a single message must fit
    private byte[] buffer = new byte[1024];
    private int bufferUsed = 0;
    
    public CammaThreadBinaryDelimited(string portName,
                                       int baudRate,
                                       int delayBeforeReconnecting,
                                       int maxUnreadMessages,
                                       byte separator)
        : base(portName, baudRate, delayBeforeReconnecting, maxUnreadMessages, false)
    {
        this.separator = separator;
    }

    // ------------------------------------------------------------------------
    // Must include the separator already (as it shold have been passed to
    // the SendMessage method).
    // ------------------------------------------------------------------------
    protected override void SendToWire(object message, SerialPort cammaPort)
    {
        byte[] binaryMessage = (byte[])message;
        cammaPort.Write(binaryMessage, 0, binaryMessage.Length);
    }

	protected override object ReadFromWire(SerialPort cammaPort)
    {
        // Try to fill the internal buffer
        bufferUsed += cammaPort.Read(buffer, bufferUsed, buffer.Length - bufferUsed);

        // Search for the separator in the buffer
        int index = System.Array.FindIndex<byte>(buffer, 0, bufferUsed, IsSeparator);
        if (index == -1)
            return null;

        byte[] returnBuffer = new byte[index];
        System.Array.Copy(buffer, returnBuffer, index);

        // Shift the buffer so next time the unused bytes start at 0 (safe even
        // if there is overlap)
        System.Array.Copy(buffer, index + 1, buffer, 0, bufferUsed - index);
        bufferUsed -= index + 1;

        return returnBuffer;
    }

    private bool IsSeparator(byte aByte)
    {
        return aByte == separator;
    }
}
