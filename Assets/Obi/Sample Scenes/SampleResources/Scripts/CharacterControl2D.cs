using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterControl2D : MonoBehaviour {

	public float speed = 10;
	public float jumpPower = 2;

	private Rigidbody rigidbody;
	
	public void Awake(){
		rigidbody = GetComponent<Rigidbody>();
	}

	
	void FixedUpdate () {

		rigidbody.AddForce(new Vector3(Input.GetAxis("Horizontal")*speed,0,0));

		if (Input.GetButtonDown("Jump")){
			rigidbody.AddForce(Vector3.up * jumpPower,ForceMode.VelocityChange);
		}
	}
}
