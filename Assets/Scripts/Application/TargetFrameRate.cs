using System;
using UnityEngine;

public class TargetFrameRate : MonoBehaviour
{
    private void Awake()
    {
        Application.targetFrameRate = 60;
    }
}