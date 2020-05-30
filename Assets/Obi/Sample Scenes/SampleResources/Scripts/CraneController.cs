using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Obi;

public class CraneController : MonoBehaviour {

	ObiRopeCursor cursor;

	// Use this for initialization
	void Start () {
		cursor = GetComponentInChildren<ObiRopeCursor>();
	}
	
	// Update is called once per frame
	void Update () {
		if (Input.GetKey(KeyCode.W)){
			if (cursor.rope.RestLength > 6.5f)
				cursor.ChangeLength(cursor.rope.RestLength - 1f * Time.deltaTime);
		}

		if (Input.GetKey(KeyCode.S)){
			cursor.ChangeLength(cursor.rope.RestLength + 1f * Time.deltaTime);
		}

		if (Input.GetKey(KeyCode.A)){
			transform.Rotate(0,Time.deltaTime*15f,0);
		}

		if (Input.GetKey(KeyCode.D)){
			transform.Rotate(0,-Time.deltaTime*15f,0);
		}
	}
}
