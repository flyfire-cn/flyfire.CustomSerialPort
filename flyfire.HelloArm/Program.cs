using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Reflection;
using System.Text;
using flyfire.IO.Ports;

namespace flyfire.HelloArm
{
    public class Program
    {
        private static CustomSerialPort csp = null;
        private static string[] serailports;
        private static int curSerialPortOrder = 0;
        private static string selectedComPort = "";
        private static Dictionary<string, CustomSerialPort> servicePorts = new Dictionary<string, CustomSerialPort>();
        private static System.Timers.Timer sendTimer = new System.Timers.Timer();

        private static readonly int baudRate = 115200;

        static void Main(string[] args)
        {
            SetLibPath();
            ShowWelcome();

            GetPortNames();
            ShowPortNames();

            if (serailports.Length == 0)
            {
                Console.WriteLine($"Press any key to exit");
                Console.ReadKey();

                return;
            }
#if RunIsService
            RunService();
#endif

            bool quit = false;
            while (!quit)
            {
                Console.WriteLine("\r\nPlease Input command Key\r\n");
                Console.WriteLine("p:Show SerialPort List");
                Console.WriteLine($"t:Test Uart:\"{selectedComPort}\"");
                Console.WriteLine($"o:Open Uart:\"{selectedComPort}\"");
                Console.WriteLine($"c:Close Uart:\"{selectedComPort}\"");
                Console.WriteLine("n:select next serial port");
                Console.WriteLine("q:exit app");
                Console.WriteLine();
                var key = Console.ReadKey().KeyChar;
                Console.WriteLine();

                switch (key)
                {
                    case (Char)27:
                    case 'q':
                    case 'Q':
                        quit = true;
                        break;
                    case 's':
                        ShowWelcome();
                        break;
                    case 'p':
                        ShowPortNames();
                        break;
                    case 'n':
                        SelectSerialPort();
                        break;
                    case 't':
                        TestUart(selectedComPort);
                        break;
                    case 'w':
                        TestWinUart(selectedComPort);
                        break;
                    case 'o':
                        OpenUart(selectedComPort);
                        break;
                    case 'c':
                        CloseUart();
                        break;
                }
            }
        }

        private static void RunService()
        {
            Console.WriteLine("\r\nRun Mode:Service");
            if (serailports.Length > 0)
            {
                foreach (var name in serailports)
                {
                    if (name.Contains("0"))
                        continue;
                    CustomSerialPort csp = new CustomSerialPort(name, baudRate);
                    csp.ReceivedEvent += Csp_ReceivedEvent;// Csp_DataReceived;
                    try
                    {
                        csp.Open();
                        servicePorts.Add(name, csp);
                        Console.WriteLine($"Service Open Uart [{name}] Succful!");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"RunService Open Uart [{name}] Exception:{ex}");
                    }
                }
                OpenUartTestTimer();
            }
        }

        private static void Csp_ReceivedEvent(object sender, byte[] bytes)
        {
            try
            {
                CustomSerialPort sps = (CustomSerialPort)sender;
                string msg = Encoding.ASCII.GetString(bytes).Replace("\r", "").Replace("\n", "");
                string echo = $"{sps.PortName} Receive Data:[{msg}].Item already filtered crlf.";
                Console.WriteLine(echo);
                if(!echo.Contains($"{sps.PortName}"))
                    sps.WriteLine(msg);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// 配置依赖库环境变量
        /// 程序中设置对当前程序无效，需重启程序方能生效，仅用于测试
        /// </summary>
        private static void SetLibPath(string libPathVariable = "LD_LIBRARY_PATH", string lib_path = "lib\\serialportstream", string os = "unix")
        {
            try
            {
                if (!Environment.OSVersion.Platform.ToString().Contains(os, StringComparison.OrdinalIgnoreCase))
                    return;

                var path = Environment.GetEnvironmentVariable(libPathVariable);
                if (path == null || path == string.Empty)
                {
                    path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, lib_path);
                    if (Directory.Exists(path))
                    {
                        Console.WriteLine($"Set Environment Variable LD_LIBRARY_PATH={path}");
                        Environment.SetEnvironmentVariable(libPathVariable, path, EnvironmentVariableTarget.User);
                    }
                    else
                        Console.WriteLine("The support library that the program depends on does not exist");
                }
                path = Environment.GetEnvironmentVariable(libPathVariable);
                Console.WriteLine($"LD_LIBRARY_PATH={path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Set Environment Variable Exception:{ex}");
            }
        }

        /// <summary>
        /// 循环切换选择串口
        /// </summary>
        private static void SelectSerialPort()
        {
            if (curSerialPortOrder < serailports.Length)
            {
                CloseUart();
                curSerialPortOrder++;
                curSerialPortOrder %= serailports.Length;
                selectedComPort = serailports[curSerialPortOrder];
                Console.WriteLine($"current selected serial port:{selectedComPort}");
            }
        }

        private static void ShowPortNames()
        {
            Console.WriteLine($"This computer has {serailports.Length} serial ports.");
            foreach (string serial in serailports)
                Console.WriteLine($"serial port:{serial}");
        }

        private static void CloseUart()
        {
            if (csp != null)
            {
                if (csp.IsOpen)
                    csp.Close();
                csp.ReceivedEvent -= Csp_ReceivedEvent;//Csp_DataReceived;

                csp = null;
                Console.WriteLine($"close serial port:{selectedComPort} succful!");
            }

#if RunIsService
            foreach (var csp in servicePorts.Values)
            {
                if (csp.IsOpen)
                {
                    csp.Close();
                }
            }
#endif
            if (sendTimer != null && sendTimer.Enabled)
            {
                sendTimer.Stop();
                sendTimer.Elapsed -= SendTimer_Elapsed;
            }
            msgIndex = 0;
        }

        private static void OpenUart(string portName)
        {
            CloseUart();
            try
            {
                csp = new CustomSerialPort(portName);
                csp.BaudRate = baudRate;
                csp.ReceivedEvent += Csp_ReceivedEvent;
                csp.Open();
                OpenUartTestTimer();
                Console.WriteLine($"open serial port:{portName} succful!baudRate:{baudRate}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Open Uart Exception:{ex}");
            }
        }

        private static void OpenUartTestTimer()
        {
            sendTimer.Interval = 5000;
            sendTimer.Elapsed += SendTimer_Elapsed;
            sendTimer.Start();
        }

        static int msgIndex = 0;
        private static void SendTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (csp != null && csp.IsOpen)
            {
                msgIndex++;
                string sendMsg = $"{selectedComPort} send msg:{msgIndex:d4}\t{DateTime.Now:yyyy/MM/dd HH:mm:ss.fff}\r\n";
                csp.Write(sendMsg);
            }

#if RunIsService
            if (servicePorts.Count > 0)
                msgIndex++;
            foreach (var sps in servicePorts.Values)
            {
                if (sps.IsOpen)
                {
                    string sendMsg = $"{sps.PortName} send msg:{msgIndex:d4}\t{DateTime.Now:yyyy/MM/dd HH:mm:ss.fff}\r\n";
                    sps.Write(sendMsg);
                }
            }
#endif
        }

        private static void TestWinUart(string portName)
        {
            SerialPort sp;

            try
            {
                sp = new SerialPort()
                {
                    PortName = portName,
                    BaudRate = baudRate
                };
                sp.Open();
                string msg;
                msg = "Hello Uart";
                sp.WriteLine(msg);
                Console.WriteLine(msg);
                msg = "Byebye Uart";
                sp.WriteLine(msg);
                sp.Close();
                sp.Dispose();
                sp = null;
                Console.WriteLine(msg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Open Uart Exception:{ex}");
            }
        }

        private static void TestUart(string portName)
        {
            CustomSerialPort sp;

            try
            {
                sp = new CustomSerialPort(portName, baudRate);
                sp.Open();
                string msg;
                msg = "Hello Uart";
                sp.WriteLine(msg);
                Console.WriteLine(msg);
                msg = "Byebye Uart";
                sp.WriteLine(msg);
                sp.Close();
                sp.Dispose();
                sp = null;
                Console.WriteLine(msg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Open Uart Exception:{ex}");
            }
        }

        private static void ShowWelcome()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var Version = version.ToString();
            var buildDateTime = System.IO.File.GetLastWriteTime(new Program().GetType().Assembly.Location).ToString();

            Console.WriteLine($"Hello {Environment.OSVersion.Platform}!");
            Console.WriteLine($"This is .netcore application.Version:{Version}\r\n");
            Console.WriteLine($"System info:{Environment.OSVersion}");
            Console.WriteLine($"Environment.Version:{Environment.Version}");
            Console.WriteLine($"Environment Directory:{Environment.CurrentDirectory}");
            Console.WriteLine($"Application Directory:{AppDomain.CurrentDomain.BaseDirectory}");
            Console.WriteLine();
            Console.WriteLine("Now time:{0:yyyy/MM/dd HH:mm:ss.fff}\r\n", DateTime.Now);
        }

        /// <summary>
        /// Get PortNames
        /// </summary>
        private static void GetPortNames()
        {
            serailports = CustomSerialPort.GetPortNames();

            if (serailports.Length > curSerialPortOrder)
                selectedComPort = serailports[curSerialPortOrder];
        }
    }
}
