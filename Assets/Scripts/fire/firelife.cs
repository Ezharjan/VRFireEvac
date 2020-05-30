using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class firelife : MonoBehaviour
{

    float startsize_min,startsize_max,startcolor_alpha;
    float startmin, startmax;  //粒子系统初始的startsize
    Color startcolor;
    public float reduceTime;  //输入的是灭火的秒数
    ParticleSystem fire;
    ParticleSystem.MainModule main;


    //计算实时FPS
    private float fpsByDeltatime = 0.5f;
    private float passedTime = 0.0f;
    private int frameCount = 0;
    private float realtimeFPS = 30.0f;//预设初值，避免系统运行时前几帧的卡死

    void Start()
    {
        fire = this.gameObject.GetComponent<ParticleSystem>();
        main = fire.main;
        startmin = startsize_min = main.startSize.constantMin;
        startmax = startsize_max = main.startSize.constantMax;
        startcolor = main.startColor.color;
        startcolor.a = main.startColor.color.a;//Alpha的范围是[0,1]
     
    }


    void Update()
    {
        GetFps();
    }
    private void debug()
    {
        //Debug.Log(startsize_min);
        //Debug.Log(startsize_max);
        //Debug.Log(main.startColor.color.a);
    }
    public float GetFps()
    {
        frameCount++;
        passedTime += Time.deltaTime;//这一帧与上一帧之间的时间

        if(passedTime >= fpsByDeltatime)
        {
            realtimeFPS = frameCount / passedTime;//计算实时帧数        
            passedTime = 0.0f;
            frameCount = 0;
        }
    
        return realtimeFPS;
    }

    public void Reduce()
    {
       
        if ((startsize_min <= 0.002) && (startsize_max <= 0.002))
        {
            startsize_min = 0;
            startsize_max = 0;
        }
        else
        {
            startsize_min -= startmin / (reduceTime * realtimeFPS);
            startsize_max -= startmax / (reduceTime * realtimeFPS);
            startcolor.a -= 1 / (reduceTime * realtimeFPS);
    
        }

        main.startSize = new ParticleSystem.MinMaxCurve (startsize_min, startsize_max); //修改粒子大小
        main.startColor = new ParticleSystem.MinMaxGradient(startcolor);  //修改粒子颜色透明度

        if ((startsize_min <= 0) && (startsize_max <= 0))
        {
            fire.Stop();  //将烟雾和灭火喷雾都作为火焰的子物体就能一起停止。 
            this.gameObject.SetActive(false);
            //Destroy(this.gameObject);
        }
    }
}
