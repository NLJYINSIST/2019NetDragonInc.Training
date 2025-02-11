﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using MiniJSON;


public class Client : MonoBehaviour
{
    Socket socket;
    Thread thread;
    string myUserName;
    string passWord;
    bool isConnected = false;
    public bool inRoom = false;
    public bool enterIsland = false;

    static Client _instance;
    static public Client Instance { get { return _instance; } }

    // 服务器IP地址、端口号
    readonly IPAddress ip = IPAddress.Parse("127.0.0.1");
    readonly int port = 4000;

    // 存储接受到的消息
    byte[] buffer = new byte[1024];
    List<string> username = new List<string>();
    List<string> msg = new List<string>();

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        // 防止载入新场景时被销毁
        DontDestroyOnLoad(_instance.gameObject);
    }

    void Start()
    {
        
    }

    // （只能在主线程中操作游戏对象）
    void Update()
    {
        // 消息显示
        try
        {
            int len = username.Count;
            if (len>0)
            {
                for(int i=0;i<len;i++)
                    ChatPanel.Instance.ShowInfo(username[i], msg[i]);
                username.Clear();
                msg.Clear();
            }
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }
        
    }

    /// <summary>
    /// 建立客户端，连接到服务器
    /// </summary>
    public void StartClient(string myName, string pswd)
    {
        myUserName = myName;
        passWord = pswd;
        ChatPanel.Instance.SetUserName(myName);
        if (isConnected)
        {
            SendLoginRequest();
            return;
        }
        IPEndPoint ipp = new IPEndPoint(ip, port);
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Debug.Log("建立客户端...");
        try
        {
            // 向服务器发起Socket连接
            socket.Connect(ipp);
            // 开启线程，接收服务器回传的消息
            thread = new Thread(new ThreadStart(ReceiveInfo));
            // 将线程设置为后台线程（会随主线程的关闭而关闭）
            thread.IsBackground = true;
            // 开启线程
            thread.Start();
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
        }
    }

    /// <summary>
    /// 发送消息（json）
    /// </summary>
    public void SendInfo(Dictionary<string, object> dict)
    {
        string s = Json.Serialize(dict) + "#end#";
        //Debug.Log("发送消息：" + s);
        socket.Send(Encoding.UTF8.GetBytes(s));
    }

    /// <summary>
    /// 接收消息
    /// </summary>
    public void ReceiveInfo()
    {
        while (true)
        {
            try
            {
                // 检查socket的当前连接状态
                if (!socket.Connected)
                {
                    Dispose();
                    break;
                }
                // 接收消息
                int length = socket.Receive(buffer);
                if (length > 0)
                {
                    // 解析消息
                    string data = Encoding.UTF8.GetString(buffer, 0, length);
                    ParseData(data);
                }
            }
            catch (Exception e)
            {
                Debug.Log("接收消息时产生错误！" + e.ToString());
            }
            //Thread.Sleep(99);
        }
    }

    /// <summary>
    /// 解析消息（根据通信协议）
    /// </summary>
    void ParseData(string data)
    {
        // 根据"#end#"标识拆分消息
        string[] dataList = Regex.Split(data, "#end#", RegexOptions.IgnoreCase);
        int len = dataList.Length;
        for (int i = 0; i < len; i++)
        {
            if (dataList[i] == "")
                break;
            // 将消息字符串转化为json格式
            var dict = Json.Deserialize(dataList[i]) as Dictionary<string, object>;
            // case: 非指令
            if (!(bool)dict["instruct"])
            {
                string n = (string)dict["name"], m = (string)dict["str"];
                if (n == "Server" && (m == "Login successfully!" || m == "User \"" + myUserName + "\" is already in the room!"))
                {
                    inRoom = true;
                    ChatPanel.Instance.SetUserName(myUserName);
                }
                username.Add(n);
                msg.Add(m);
            }
            // case: 指令
            else
                DoInstruct(dict);
        }
    }

    /// <summary>
    /// 执行指令
    /// </summary>
    void DoInstruct(Dictionary<string, object> dict)
    {
        switch ((string)dict["str"])
        {
            case "Connected":
                isConnected = true;
                username.Add("Server");
                msg.Add("已连接到服务器。");
                SendLoginRequest();
                break;
            case "manipulate":
                if (enterIsland)
                    MultiPlayers.Instance.Manipulate(dict);
                break;
            case "update":
                if (enterIsland)
                    MultiPlayers.Instance.UpdateState(dict);
                break;
        }
    }

    /// <summary>
    /// 发送登录请求
    /// </summary>
    void SendLoginRequest()
    {
        var d = new Dictionary<string, object>
        {
            ["instruct"] = true,
            ["name"] = myUserName,
            ["pswd"] = passWord,
            ["str"] = "connect"
        };
        SendInfo(d);
    }

    /// <summary>
    /// 向服务器传递玩家的状态
    /// </summary>
    public void Upadate(int health, Vector3 p, Vector3 r)
    {
        var d = new Dictionary<string, object>
        {
            ["instruct"] = true,
            ["str"] = "update",
            ["name"] = myUserName,
            ["h"] = health,
            ["pX"] = p.x,
            ["pY"] = p.y,
            ["pZ"] = p.z,
            ["rX"] = r.x,
            ["rY"] = r.y,
            ["rZ"] = r.z
        };
        SendInfo(d);
    }

    /// <summary>
    /// 向服务器传递玩家的操作
    /// </summary>
    public void Manipulate(Vector3 move, bool crouch, bool jump)
    {
        var d = new Dictionary<string, object>
        {
            ["instruct"] = true,
            ["str"]="manipulate",
            ["name"] = myUserName,
            ["moveX"] = move.x,
            ["moveY"] = move.y,
            ["moveZ"] = move.z,
            ["c"] = crouch,
            ["j"] = jump
        };
        SendInfo(d);
    }

    /// <summary>
    /// 获取用户名
    /// </summary>
    public string GetUserName()
    {
        return myUserName;
    }

    /// <summary>
    /// 脚本的生命周期结束时
    /// </summary>
    void OnDestroy()
    {
        Dispose(); //unity中须要显式的清理
    }

    /// <summary>
    /// 清理连接和线程
    /// </summary>
    public void Dispose()
    {
        isConnected = false;
        inRoom = false;
        // 终止线程
        if (thread != null)
        {
            if (thread.ThreadState != ThreadState.Aborted)
            {
                thread.Abort();
            }
            thread = null;
        }
        // 关闭连接
        if (socket != null && socket.Connected)
        {
            socket.Shutdown(SocketShutdown.Both); //禁用收发消息
            socket.Close();
            Debug.Log("与服务器断开了连接！");
        }
    }
}
