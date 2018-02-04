// Copyright (c) 2017 Maxime Coutté-Péroumal Corne

using UnityEngine;
using System.Collections;

/**
 * Sample for reading using polling by yourself. In case you are fond of that.
 */
public class CammaVR_control : MonoBehaviour
{
	string[] sep = new string[] {","};
	public CammaController serialController;

	// Initialization
	void Start()
	{
		serialController = GameObject.Find("CammaController").GetComponent<CammaController>();
	}

	// Executed each frame
	void Update()
	{
		string message = serialController.ReadCammaMessage();
		string[] values = message.Split (sep, System.StringSplitOptions.RemoveEmptyEntries);

			transform.localEulerAngles = new Vector3 (float.Parse (values [0]) * -1, float.Parse (values [1]), float.Parse (values [2]));
}
}