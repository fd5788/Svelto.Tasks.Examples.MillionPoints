using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class fpsGrabber : MonoBehaviour {

    [SerializeField]
    private Text _text;

    // Use this for initialization
    void Start () {
        _text.text = "fps: ";
	}
	
	// Update is called once per frame
	void Update () {
        float fps = 1.0f / Time.deltaTime;
        _text.text = "fps: " + fps.ToString("F2");
	}
}
