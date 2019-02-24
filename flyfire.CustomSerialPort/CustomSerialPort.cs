using RJCP.IO.Ports;
using System;
using System.Collections.Generic;
using System.Threading;

namespace flyfire.IO.Ports
{
    /// <summary>
    /// 一个增强的自定义串口类，实现协议无关的数据帧完整接收功能，支持跨平台使用
    /// 使用SerialPortStream基础类库
    /// https://github.com/jcurl/serialportstream
    /// https://www.cnblogs.com/flyfire-cn/p/10356991.html
    /// 
    /// 采用接收超时机制，两次通讯之间需有一定的间隔时间
    /// 如间隔时间太短，则需要拆包
    /// Author:赫山老妖（flyfire.cn）
    /// https://github.com/flyfire-cn
    /// </summary>
    public class CustomSerialPort
    {
        public event CustomSerialPortReceivedEventHandle ReceivedEvent;

        protected SerialPortStream sp = null;

        public string PortName
        {
            get { return sp.PortName; }
            set { sp.PortName = value; }
        }

        public int BaudRate {
            get { return sp.BaudRate; }
            set { sp.BaudRate = value; }
        }

        public Parity Parity
        {
            get { return sp.Parity; }
            set { sp.Parity = value; }
        }

        public int DataBits
        {
            get { return sp.DataBits; }
            set { sp.DataBits = value; }
        }

        public StopBits StopBits
        {
            get { return sp.StopBits; }
            set { sp.StopBits = value; }
        }

        public bool IsOpen { get { return sp.IsOpen; } }

        public bool DtrEnable
        {
            set { sp.DtrEnable = value; }
            get { return sp.DtrEnable; }
        }

        public bool RtsEnable
        {
            set { sp.RtsEnable = value; }
            get { return sp.RtsEnable; }
        }

        /// <summary>
        /// 是否使用接收超时机制
        /// 默认为真
        /// 接收到数据后计时，计时期间收到数据，累加数据，重新开始计时。超时后返回接收到的数据。
        /// </summary>
        public bool ReceiveTimeoutEnable { get; set; } = true;

        /// <summary>
        /// 读取接收数据未完成之前的超时时间
        /// 默认128ms
        /// </summary>
        public int ReceiveTimeout { get; set; } = 128;

        /// <summary>
        /// 超时检查线程运行标志
        /// </summary>
        bool TimeoutCheckThreadIsWork = false;

        /// <summary>
        /// 最后接收到数据的时间点
        /// </summary>
        int lastReceiveTick = 0;

        /// <summary>
        /// 接到数据的长度
        /// </summary>
        int receiveDatalen;

        /// <summary>
        /// 接收缓冲区
        /// </summary>
        byte[] recviceBuffer;

        /// <summary>
        /// 接收缓冲区大小
        /// 默认4K
        /// </summary>
        public int BufSize
        {
            get
            {
                if (recviceBuffer == null)
                    return 4096;
                return recviceBuffer.Length;
            }
            set
            {
                recviceBuffer = new byte[value];
            }
        }

        public CustomSerialPort(string portName, int baudRate = 115200, Parity parity = Parity.None, int databits = 8, StopBits stopBits = StopBits.One)
        {
            sp = new SerialPortStream
            {
                PortName = portName,
                BaudRate = baudRate,
                Parity = parity,
                DataBits = databits,
                StopBits = stopBits
            };

            DtrEnable = true;
            RtsEnable = true;
        }

        public static string[] GetPortNames()
        {
            List<string> serailports = new List<string>();
            serailports.AddRange(SerialPortStream.GetPortNames());
            serailports.Sort();
            return serailports.ToArray();
        }

        public bool Open()
        {
            try
            {
                if (recviceBuffer == null)
                {
                    recviceBuffer = new byte[BufSize];
                }
                sp.Open();
                sp.DataReceived += Sp_DataReceived; //new SerialDataReceivedEventHandler(sp_DataReceived);
                return true;
            }
            catch (Exception e)
            {
                throw (e);
            }
        }

        public void Close()
        {
            if (sp != null && sp.IsOpen)
            {
                sp.DataReceived -= Sp_DataReceived;//new SerialDataReceivedEventHandler(sp_DataReceived);
                sp.Close();
                if (ReceiveTimeoutEnable)
                {
                    Thread.Sleep(ReceiveTimeout);
                    ReceiveTimeoutEnable = false;
                }
            }
        }

        public void Dispose()
        {
            if (sp != null)
                sp.Dispose();
        }

        protected void Sp_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int canReadBytesLen = 0;
            if (ReceiveTimeoutEnable)
            {
                while (sp.BytesToRead > 0)
                {
                    canReadBytesLen = sp.BytesToRead;
                    if (receiveDatalen + canReadBytesLen > BufSize)
                    {
                        receiveDatalen = 0;
                        throw new Exception("Serial port receives buffer overflow!");
                    }
                    var receiveLen = sp.Read(recviceBuffer, receiveDatalen, canReadBytesLen);
                    if (receiveLen != canReadBytesLen)
                    {
                        receiveDatalen = 0;
                        throw new Exception("Serial port receives exception!");
                    }
                    //Array.Copy(recviceBuffer, 0, receivedBytes, receiveDatalen, receiveLen);
                    receiveDatalen += receiveLen;
                    lastReceiveTick = Environment.TickCount;
                    if (!TimeoutCheckThreadIsWork)
                    {
                        TimeoutCheckThreadIsWork = true;
                        Thread thread = new Thread(ReceiveTimeoutCheckFunc)
                        {
                            Name = "ComReceiveTimeoutCheckThread"
                        };
                        thread.Start();
                    }
                }
            }
            else
            {
                if (ReceivedEvent != null)
                {
                    // 获取字节长度
                    int bytesNum = sp.BytesToRead;
                    if (bytesNum == 0)
                        return;
                    // 创建字节数组
                    byte[] resultBuffer = new byte[bytesNum];

                    int i = 0;
                    while (i < bytesNum)
                    {
                        // 读取数据到缓冲区
                        int j = sp.Read(recviceBuffer, i, bytesNum - i);
                        i += j;
                    }
                    Array.Copy(recviceBuffer, 0, resultBuffer, 0, i);
                    ReceivedEvent(this, resultBuffer);
                    //System.Diagnostics.Debug.WriteLine("len " + i.ToString() + " " + ByteToHexStr(resultBuffer));
                }
                //Array.Clear (receivedBytes,0,receivedBytes.Length );
                receiveDatalen = 0;
            }
        }

        /// <summary>
        /// 超时返回数据处理线程方法
        /// </summary>
        protected void ReceiveTimeoutCheckFunc()
        {
            while (TimeoutCheckThreadIsWork)
            {
                if (Environment.TickCount - lastReceiveTick > ReceiveTimeout)
                {
                    if (ReceivedEvent != null)
                    {
                        byte[] returnBytes = new byte[receiveDatalen];
                        Array.Copy(recviceBuffer, 0, returnBytes, 0, receiveDatalen);
                        ReceivedEvent(this, returnBytes);
                    }
                    //Array.Clear (receivedBytes,0,receivedBytes.Length );
                    receiveDatalen = 0;
                    TimeoutCheckThreadIsWork = false;
                }
                else
                    Thread.Sleep(16);
            }
        }

        public void Write(byte[] buffer)
        {
            if (IsOpen)
                sp.Write(buffer, 0, buffer.Length);

            System.Diagnostics.Debug.WriteLine(ByteToHexStr(buffer));
        }

        public void Write(string text)
        {
            if (IsOpen)
                sp.Write(text);
        }

        public void WriteLine(string text)
        {
            if (IsOpen)
                sp.WriteLine(text);
        }

        public static string ByteToHexStr(byte[] bytes)
        {
            string returnStr = "";
            if (bytes != null)
            {
                for (int i = 0; i < bytes.Length; i++)
                {
                    returnStr += bytes[i].ToString("X2") + " ";
                }
            }
            return returnStr;
        }
    }

    /// <summary>
    /// 串口接收事件
    /// </summary>
    /// <param name="bytes">接收到的数据</param>
    public delegate void CustomSerialPortReceivedEventHandle(object sender, byte[] bytes);

}
