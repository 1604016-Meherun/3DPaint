using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class ControllerManager : MonoBehaviour
{
    private double time;


    public InputDevice rightController;
    public InputDevice leftController;


    [SerializeField]
    private XRDirectInteractor leftInteractor;


    [SerializeField]
    private XRDirectInteractor rightInteractor;

    void Start()
    {
        InitializeInputDevices();
    }

    public void InitializeInputDevices()
    {
        // Initialize both controllers regardless of validity
        InitializeInputDevice(XRNode.RightHand, ref rightController);
        InitializeInputDevice(XRNode.LeftHand, ref leftController);
        Debug.Log("Devices initialized.");
    }

    private void InitializeInputDevice(XRNode node, ref InputDevice inputDevice)
    {
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(node, devices);

        if (devices.Count > 0)
        {
            inputDevice = devices[0];
        }
        else
        {
            Debug.LogWarning("No input device found for " + node.ToString());
        }
    }

    public bool IsLeftControllerGrabbing()
    {
        return leftInteractor.selectTarget != null;
    }


    public bool IsRightControllerGrabbing()
    {
        return rightInteractor.selectTarget != null;
    }

    public bool IsLeftControllerGrabbing(Transform target)
    {
        if (leftInteractor.selectTarget != null)
        {
            return leftInteractor.selectTarget.transform == target;
        }
        return false;
    }

    public bool IsRightControllerGrabbing(Transform target)
    {
        if (rightInteractor.selectTarget != null)
        {
            return rightInteractor.selectTarget.transform == target;
        }
        return false;
    }

    public IXRSelectInteractable GetLeftGrabbedObject()
    {
        return leftInteractor.selectTarget;
    }


    public IXRSelectInteractable GetRightGrabbedObject()
    {
        return rightInteractor.selectTarget;
    }

}