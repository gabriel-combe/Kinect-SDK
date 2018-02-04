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
public class CammaThread : CammaThreadLines
{

    public CammaThread(string portName,
                        int baudRate,
                        int delayBeforeReconnecting,
                        int maxUnreadMessages)
        : base(portName, baudRate, delayBeforeReconnecting, maxUnreadMessages)
    {
    }
}
