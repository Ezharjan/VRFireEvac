using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{

    public GameObject toast = null;
    public float liftHeight = 0f;


    private GameObject player = null;


    // Start is called before the first frame update
    void Start()
    {
        try
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }
        catch (Exception e)
        {
            Debug.LogError("Exception happened:" + e);
        }
    }

    // Update is called once per frame
    void Update()
    {
        InteractionHandler();
    }





    private void InteractionHandler()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isSceneExist("DownStairs"))
            {
                Debug.Log("Target scene loaded.");
            }
            else
            {
                //Inform the player with 3 seconds toastImage.
                Toast(3);
            }
        }
        if (Input.GetKeyDown(KeyCode.I))
        {
            if (Elevator(liftHeight))
            {
                DoWhenElevated();
            }
            else
            {
                //Inform the player with 3 seconds toastImage.
                Toast(3);
            }
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Quit();
        }
    }

    private void DoWhenElevated()
    {
        //TODO: Do something...
        Debug.Log("Do something here!");
    }


    private bool Elevator(float elevatHeight)
    {

        try
        {
            player.transform.position = new Vector3
            (player.transform.position.x, elevatHeight, player.transform.position.z);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("Exception : " + e);
            return false;
        }
    }


    public bool isSceneExist(string sceneName)
    {
        try
        {
            SceneManager.LoadScene(sceneName);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("Exception : " + e);
            return false;
        }
    }


    public void ButtonOne()
    {
        Debug.Log("Button one clicked");
    }

    public void ButtonTwo()
    {
        Debug.Log("Button two clicked");
    }



    /// //////////////////////////
    /// //////////////////////////
    /// <summary>
    /// /// Common Utils
    /// </summary>
    /// //////////////////////////
    /// //////////////////////////


    public void Toast(int length)
    {
        toast.SetActive(true);
        toastTime = length;
        StartCoroutine(Timer());
    }
    int toastTime = 3;
    IEnumerator Timer()
    {
        while (toastTime >= 0)
        {
            yield return new WaitForSeconds(1);
            toastTime--;
            if (toastTime <= 0)
            {
                toast.SetActive(false);
                StopCoroutine(Timer());
            }
        }
    }


    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
	Application.Quit();
#endif
    }
}
