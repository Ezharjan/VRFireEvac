using System.Collections;
using UnityEngine.Video;
using UnityEngine;
using System.IO;

/*作者：艾孜尔江·艾尔斯兰*/
/*VideoPlayer能够播放的视频格式为你的设备内置播放器能够播放的格式 
（通常为 .mov, .mpg, .mpeg, .mp4, .avi, .asf等格式）。
*/

public class PlayDownloadVideo : MonoBehaviour
{
    string urlPath = "http://127.0.0.1:3000/myvideo.mp4";//资源网络路径 目前写死 
    string rootPath = "";//存放根路径，一定要在游戏启动时再获取
    string file_SaveUrl;//资源本地存放具体文件路径  
    FileInfo file;
    private bool down;
    public VideoPlayer _videoPlayer;
    private string urlPrefix = "file://";



    private void Awake()
    {
        _videoPlayer = GetComponent<VideoPlayer>();
    }
    private void Start()
    {
        rootPath = Application.temporaryCachePath;//游戏启动时存放获取根路径
        file_SaveUrl = rootPath + "/tmpVideo.mp4"; //保存在本地路径  记得加上文件后缀名  
        file = new FileInfo(file_SaveUrl);
        Debug.Log("path:" + file_SaveUrl);
        DirectoryInfo mydir = new DirectoryInfo(file_SaveUrl);
        if (File.Exists(file_SaveUrl))//判断一下本地是否有了该文件  如果有就不需下载  
        {
            PlayVideo();
        }
        else
        {
            StartCoroutine(DownFile(urlPath));
        }
    }
    IEnumerator DownFile(string url)
    {

        WWW www = new WWW(url);
        down = false;
        yield return www;
        down = true;
        if (www.isDone)
        {
            byte[] bytes = www.bytes;
            CreatFile(bytes);
            PlayVideo();
        }
    }
    void CreatFile(byte[] bytes)
    {
        Stream stream;
        stream = file.Create();
        stream.Write(bytes, 0, bytes.Length);
        Debug.Log("下载完成");
        stream.Close();
        stream.Dispose();
    }

    public void PlayVideo()
    {
        StartCoroutine(DelayPlayVideo(1));
    }

    IEnumerator DelayPlayVideo(float time)
    {
        _videoPlayer.source = VideoSource.Url;
        _videoPlayer.url = urlPrefix + file_SaveUrl;//注意:播放组件需要带上"file://"前缀！！！
        _videoPlayer.Prepare();
        _videoPlayer.playOnAwake = false;
        while (!_videoPlayer.isPrepared)
        {
            Debug.Log("Preparing Video");
            yield return null;
        }
        _videoPlayer.Play();
    }



    /// <summary>
    /// 删除指定文件目录下的所有文件
    /// </summary>
    /// <param name="fullPath">文件路径</param>
    public bool DeleteAllFiles(string fullPath)
    {
        //获取指定路径下面的所有资源文件  然后进行删除
        if (Directory.Exists(fullPath))
        {
            DirectoryInfo direction = new DirectoryInfo(fullPath);
            FileInfo[] files = direction.GetFiles("*", SearchOption.AllDirectories);

            Debug.Log(files.Length);

            for (int i = 0; i < files.Length; i++)
            {
                if (files[i].Name.EndsWith(".meta"))
                {
                    continue;
                }
                string FilePath = fullPath + "/" + files[i].Name;
                print(FilePath);
                File.Delete(FilePath);
            }
            return true;
        }
        return false;
    }


    void OnApplicationQuit()
    {
        bool deleted = DeleteAllFiles(rootPath);
        if (deleted)
        {
            Debug.Log("Temperory files deleted!");
        }
    }

}