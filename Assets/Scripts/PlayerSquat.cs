using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerSquat : MonoBehaviour
{

    // private GameObject playerCamera;
    private int squatSpeed = 5;
    private int squatDownLimit = -50;
    private int squatUpLimit = 30;
    private int playerHeight = 0;
    private Rigidbody playerRigidbody;
    private bool isSquat = false;

    void Awake()
    {
        // playerRigidbody = GetComponent<Rigidbody>();
    }

    void Update()
    {
        Interaction();
    }

    void Interaction()
    {
        if (Input.GetKey(KeyCode.DownArrow) && playerHeight > squatDownLimit)
        {
            playerHeight--;
            this.gameObject.transform.Translate(Vector3.down * Time.deltaTime * squatSpeed);
            Debug.Log(this.gameObject.transform.position.y);
            isSquat = true;
            // Debug.Log($"Down going ... {playerHeight}");
        }
        else if (Input.GetKey(KeyCode.UpArrow) && playerHeight < squatUpLimit)
        {
            playerHeight++;
            this.gameObject.transform.Translate(Vector3.up * Time.deltaTime * squatSpeed);
            Debug.Log(this.gameObject.transform.position.y);
            isSquat = false;
            // Debug.Log($"Up going ... {playerHeight}");
        }
        else if (Input.GetKeyDown(KeyCode.P) && !isSquat)
        {
            // playerRigidbody.velocity += new Vector3(0, 5, 0);
            // playerRigidbody.AddForce(Vector3.up * 50);
            isSquat = false;
        }
        else if (Input.GetKeyDown(KeyCode.O) && isSquat)
        {
            // playerRigidbody.velocity += new Vector3(0, 5, 0);
            // playerRigidbody.AddForce(Vector3.down * 50);
            isSquat = true;
        }

    }


}
