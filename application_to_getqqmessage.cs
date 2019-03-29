using System;
using System.Text;
using System.Windows.Automation;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Collections.Generic;
using System.Net.Http;
using System.Collections.Specialized;


public abstract class Win32Native
{
    [DllImport("user32.dll")]
    public static extern bool SendMessage(IntPtr hwnd, int wMsg, int wParam, int lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(LPENUMWINDOWSPROC lpEnumFunc, int lParam);

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClassName(int hWnd, StringBuilder buf, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowText(int hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowTextLength(int hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(IntPtr hWnd);

    private delegate bool LPENUMWINDOWSPROC(int hwnd, int lParam);

    public const int NULL = 0;
    private const int MAXBYTE = 255;
    private const int WM_CLOSE = 0x10;

    private static bool ScanTargetWindow(int hwnd, int lParam)
    {
    	//包含了QQ程序中非聊天窗口的名字，他们的窗口class均是TXGuiFoundation
        string[] windowsname = { "QQ", "勋章墙", "TXMenuWindow", "增加时长", "腾讯网迷你版", "**的 Android手机", "QQ精选", "验证消息", "系统设置", "好友动态"};
        StringBuilder buf = new StringBuilder(MAXBYTE);
        if (GetClassName(hwnd, buf, MAXBYTE) && buf.ToString() == "TXGuiFoundation")//找到QQ窗口
        {
            if (GetWindowTextLength(hwnd) > 0 && GetWindowText(hwnd, buf, MAXBYTE))//获取窗口名称
            {
                string str = buf.ToString();
                if (Array.IndexOf(windowsname, str) == -1)//找对话窗口
                {
                    if (!(Program.hWndcollection.Contains(hwnd)))//如果不存在就加进去，上次就打开的不加入
                    {
                        Program.usercollection.Add(str);//添加对应标题
                        Program.hWndcollection.Add(hwnd);//添加对应句柄
                    }
                }
            }
        }
        return true;
    }
    public static void GetWindow()
    {
        EnumWindows(ScanTargetWindow, NULL);
    }
    public static void CloseWindow(IntPtr hwnd)
    {
        SendMessage(hwnd, WM_CLOSE, 0, 0);
    }
}

public class Msg
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
    private const int KEY_ENTER = 0x0D;
    private const int KEY_DOEN = 0x0100;
    private const int KEY_UP = 0x0101;
    private const int CLIP_BOARD = 0x302;
    public static string[] FindUserMessage(AutomationElement element)
    {
        string[] message = { "", "", "" };
        if (element != null && element.Current.IsEnabled)
        {
            ValuePattern vpTextEdit = element.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
            if (vpTextEdit != null)
            {
                string value = vpTextEdit.Current.Value;
                try//有些非文本信息并没有相关内容，如文件、公告、表情、全体消息
                {
                    string[] sArray = Regex.Split(value, "\r ", RegexOptions.IgnoreCase);
                    message[0] = sArray[sArray.Length - 2];//获取消息内容
                    message[1] = sArray[sArray.Length - 3].Split(' ')[1];//获取对方昵称
                    message[2] = sArray[sArray.Length - 3].Split(' ')[2];//获取消息时间
                }
                catch
                {
                    return message;
                }
                return message;
            }
            else
            {
                return message;
            }
        }
        else
        {
            return message;
        }
    }
    public static bool SendMsg(string contents, IntPtr hwnd)//向窗口发送内容
    {
        if(contents.Contains("文件"))//复制对应文件
        {
            string filepath = contents.Split('+')[1];
            filepath = @"C:\Users\Public\SmartFile\" + filepath;
            StringCollection strcoll = new StringCollection();
            strcoll.Add(filepath);
            Clipboard.SetFileDropList(strcoll);
        }
        else
        {
            Clipboard.SetText(contents);//发送文本
            Thread.Sleep(500);
        }
        Win32Native.SendMessage(hwnd, CLIP_BOARD, 0, 0);//从剪贴板粘贴
        Win32Native.SendMessage(hwnd, KEY_DOEN, KEY_ENTER, 0);//模拟回车，窗口不必最前，不使用sendkey
        Win32Native.SendMessage(hwnd, KEY_UP, KEY_ENTER, 0);
        return true;
    }
}

public class Program
{
    public static List<int> hWndcollection = new List<int>();//句柄
    public static List<string> usercollection = new List<string>();//窗口名字
    public static List<AutomationElement> elementcollection = new List<AutomationElement>();//元素
    public static List<string[]> leastmsgcollection = new List<string[]>();//最新的信息
    public static List<DateTime> leasttimecollection = new List<DateTime>();//信息时间
    public static DateTime lasttime = DateTime.Now;
    //这个数组包含了自己在各个群中的昵称，因为你不会回复自己的消息
    public static string[] myself = { "南京邮电大学育人勤助协会(3208465473)", "Smart小育(3208465473)", "南京邮电大学育人勤助协会" };
    private static void Main(string[] args)
    {
        Console.Title = string.Empty;
        Init();
        Thread scan = new Thread(ListenWindows_Message);
        scan.SetApartmentState(ApartmentState.STA);
        scan.Start();
    }
    private static void Do(string message, IntPtr hwnd)//向服务器后台请求相应的回复
    {
        HttpClient get = new HttpClient();
        string result = "";
        try
        {
            var task = get.GetStringAsync("http://*.*.*.*:6000/?what=" + message);//同步http请求
            result = task.Result;
        }
        catch
        {
            result = "小育连不上网了";
        }
        if(result != "donotaction")
        {
            Msg.SendMsg(result, hwnd);
        }
    }
    private static void Init()
    {
        for(int i = 0; i< hWndcollection.Count; i++)//扫描所有打开的窗口无动作时间，第一次运行无。
        {
            TimeSpan timeSpan = DateTime.Now - leasttimecollection[i];
            if (timeSpan.TotalMinutes > 2)
            {
                Win32Native.CloseWindow((IntPtr)hWndcollection[i]);//超过2分钟无消息变化就关闭窗口
                hWndcollection.RemoveAt(i);//没有变化就删除对应的那些东西
                elementcollection.RemoveAt(i);
                leastmsgcollection.RemoveAt(i);
                leasttimecollection.RemoveAt(i);
                usercollection.RemoveAt(i);
                i--;
            }
        }
        string[] msg = { "*/@#$%^%$#@~!@#", "", "" };
        //复制list
        List<int> hWndcollection_saved = new List<int>();
        hWndcollection.ForEach(i => hWndcollection_saved.Add(i));//深拷贝上一次的句柄句柄
        Console.WriteLine("Scanning the target QQ windows...");
        Win32Native.GetWindow();//扫描获取所有的窗口句柄，如果上一次扫描到的窗口还没关闭，就直接用
        for (int i = 0; i < hWndcollection.Count; i++)
        {
            bool isfind = false;
            for (int j = 0; j < hWndcollection_saved.Count; j++)
            {
                if (hWndcollection[i] == hWndcollection_saved[j])
                {
                    isfind = true;
                    break;
                }
            }
            if(isfind == false)//如果之前没有就加进去
            {
                elementcollection.Add(null);
                leastmsgcollection.Add(msg);
                leasttimecollection.Add(DateTime.Now);
            }
            Console.WriteLine(hWndcollection[i].ToString() + usercollection[i] + " " + leasttimecollection[i]);//调试用
        }
        for (int i = 0; i < hWndcollection.Count; i++)
        {
            if(elementcollection[i] != null)
            {
                continue;//如果上一轮有这个窗口，就不找对应的文本输入元素
            }
            try//两种方式，第一种通过树形结构找，先利用inspect分析，比较快
            {
                AutomationElement temp = AutomationElement.FromHandle((IntPtr)hWndcollection[i]);
                AutomationElement _1 = TreeWalker.ControlViewWalker.GetFirstChild(temp);
                AutomationElement _3 = TreeWalker.ControlViewWalker.GetNextSibling(
                                                TreeWalker.ControlViewWalker.GetNextSibling(_1));
                AutomationElement _3_1 = TreeWalker.ControlViewWalker.GetFirstChild(_3);
                AutomationElement _3_1_1_1 = TreeWalker.ControlViewWalker.GetFirstChild(
                                                TreeWalker.ControlViewWalker.GetFirstChild(_3_1));
                AutomationElement _3_1_1_1_1 = TreeWalker.ControlViewWalker.GetFirstChild(_3_1_1_1);
                AutomationElement _3_1_1_1_2_1 = TreeWalker.ControlViewWalker.GetFirstChild(
                                                TreeWalker.ControlViewWalker.GetNextSibling(_3_1_1_1_1));
                AutomationElement _3_1_1_1_2_1_2 = TreeWalker.ControlViewWalker.GetNextSibling(
                                                TreeWalker.ControlViewWalker.GetFirstChild(_3_1_1_1_2_1));
                AutomationElement _3_1_1_1_2_1_2_1 = TreeWalker.ControlViewWalker.GetFirstChild(_3_1_1_1_2_1_2);
                AutomationElement _3_1_1_1_2_1_2_1_2 = TreeWalker.ControlViewWalker.GetNextSibling(
                                                TreeWalker.ControlViewWalker.GetFirstChild(_3_1_1_1_2_1_2_1));
                AutomationElement _3_1_1_1_2_1_2_1_2_1_1 = TreeWalker.ControlViewWalker.GetFirstChild(
                                                TreeWalker.ControlViewWalker.GetFirstChild(_3_1_1_1_2_1_2_1_2));
                AutomationElement _3_1_1_1_2_1_2_1_2_1_3 = TreeWalker.ControlViewWalker.GetNextSibling(
                                                TreeWalker.ControlViewWalker.GetNextSibling(_3_1_1_1_2_1_2_1_2_1_1));
                AutomationElement _3_1_1_1_2_1_2_1_2_1_3_1 = TreeWalker.ControlViewWalker.GetFirstChild(_3_1_1_1_2_1_2_1_2_1_3);
                elementcollection[i] = _3_1_1_1_2_1_2_1_2_1_3_1;
            }
            catch//第二种，直接通过名字遍历窗口的所用元素，很慢
            {
                PropertyCondition nameconditon = new PropertyCondition(AutomationElement.NameProperty, "消息");
                elementcollection[i] = elementcollection[i].FindFirst(TreeScope.Descendants, nameconditon);//找到对应消息空间
            }

        }
    }
    private static void ListenWindows_Message()
    {
        TimeSpan timespan;
        string[] msg = { "*/@#$%^%$#@~!@#", "",""};//初始消息，随便写
        while (true)
        {
            timespan = DateTime.Now - lasttime;
            if(timespan.TotalSeconds > 5)//5秒扫描一次窗口个数
            {
                lasttime = DateTime.Now;
                Init();
            }
            Thread.Sleep(200);//延时200ms
            for (int i = 0; i < hWndcollection.Count; i++)//扫描当前轮儿，窗口有没有新信息
            {
                if (!Win32Native.IsWindow((IntPtr)hWndcollection[i]))//窗口已关闭，删除对应资源
                {
                    hWndcollection.RemoveAt(i);
                    elementcollection.RemoveAt(i);
                    leastmsgcollection.RemoveAt(i);
                    leasttimecollection.RemoveAt(i);
                    usercollection.RemoveAt(i);
                    i--;
                    continue;
                }
                msg = Msg.FindUserMessage(elementcollection[i]);//获取消息
                if (Array.IndexOf(myself, msg[1]) == -1)//如果消息不是自己发的
                {
                    if (msg[0] != leastmsgcollection[i][0])//如果和上次的消息不一样，相同内容不必回复
                    {
                        leastmsgcollection[i] = msg;
                        leasttimecollection[i] = DateTime.Now;//记录这个消息更新的时间
                        Do(msg[0], (IntPtr)hWndcollection[i]);//向接口请求要回复的内容
                    }
                }
            }
        }
    }
}