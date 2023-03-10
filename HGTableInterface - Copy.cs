using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Reflection;
using System.Configuration;
using System.Security.Cryptography;
using System.Numerics;
using log4net;
using System.Collections;

//[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log4net.config", Watch = false)]

namespace HGTranslator
{
    class HGTableInterface
    {
        public static readonly ILog logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //public ILog logger = LogManager.GetLogger("Logger");
        public static HGTableInterface client;
        public static AssemblyName appName;
        public static byte[] HGTuningData;
        private System.Timers.Timer A2Timer;                                                      // A2 command timer
        private byte[] A2CommandBytes = new byte[BYTES_IN_A2_COMMAND];
        private bool exitClicked                            = false;                              // true if exiting the application
        private static int num_seats                        = Convert.ToInt32(ConfigurationManager.AppSettings.Get("TableSeats")); // number of configured seats
        private const int A2_TIMEOUT_VALUE                  = 10;                                 // timeout value for the A2 command event
        private const int BYTES_IN_A2_COMMAND = 78;
        private const int RECORD_LEN = 16;                                                        // length of a chip record 
        private const int SN_LEN = 12;                                                              // length of a SN record including the 6 leading 0s
        private const int HEADER_SIZE = 75;                                                        // Roulette 4 comm spec 7 A2 header size 
        private Socket[] Sockets = new Socket[(int)SEAT.MAX_SEATS - 1];              // array to hold the tcp sockets
        private Thread[] workerThreads = new Thread[(int)SEAT.MAX_SEATS - 1];              // array to hold the worker threads for each socket
        private string[] seatResponses = new string[(int)SEAT.MAX_SEATS - 1];              // array to hold the response string for each tcp socket
        private ThreadStart[] workerFuncs = new ThreadStart[(int)SEAT.MAX_SEATS - 1];         // array to hold the worker function for each worker thread
        private bool[] workerFlags = new bool[(int)SEAT.MAX_SEATS - 1];                // array to hold the worker thread loop flags
        private Socket Seat12Socket;              // socket for seat 12
        private Thread Seat12Thread;              // thread for seat 12
        private string Seat12Response = "";              // string to hold responses for seat 12
        private ThreadStart Seat12WorkerFunc;         // Worker function for seat 12
        private String Seat12Port = "55012";
        private bool Seat12WorkerFlag = false;                // worker flag for seat 12
        private int numChipsSeen                            = 0;                                  // number of chips seen in the A2 response from the table
        private HashSet<ChipObject> chips                   = new HashSet<ChipObject>();          // used to hold chip information keyed by UID       
        private Dictionary<string, ChipData> chipsDict      = new Dictionary<string, ChipData>(); // structure as follows: { uid, { chipvalue, chiptype }}
        public Dictionary<string, string> chipLocationPrev  = new Dictionary<string, string>();   // structure as follows: { uid, axis }
        private StringBuilder respStr                       = new();
        private Queue GGHostQueue = new();
        private Thread msgSendThread = new(() => MessageSendWorker());
        private bool messageSendWorkerFlag = false;

        private System.Timers.Timer seat1A2Timer;
        private Socket seat1Socket;
        private Thread seat1WorkerThread;
        private string seat1Response = "";
        private ThreadStart seat1Worker;
        private bool seat1WorkerFlag = false;

        private System.Timers.Timer seat2A2Timer;
        private Socket seat2Socket;
        private Thread seat2WorkerThread;
        private string seat2Response = "";
        private ThreadStart seat2Worker;
        private bool seat2WorkerFlag = false;

        public int axisInGroup = 0;
        public List<decimal> seat1AntennaList;
        public List<decimal> seat2AntennaList;
        public List<decimal> seat3AntennaList;
        public List<decimal> seat4AntennaList;
        public List<decimal> seat5AntennaList;
        public List<decimal> seat6AntennaList;
        public List<decimal> seat7AntennaList;
        public List<decimal> seat8AntennaList;
        public List<decimal> seat9AntennaList;
        public List<decimal> floatAntennaList;
        public List<decimal> exchangeAntennaList;
        public List<decimal> axisToIgnoreList;
        public List<int> groupPriorityList;
        public Dictionary<int, int> groupNumberDict;
        public bool FlushingTags = false;

        private readonly string seat1IPAddress = ConfigurationManager.AppSettings.Get("seat1IPAddress");
        private readonly string seat2IPAddress = ConfigurationManager.AppSettings.Get("seat2IPAddress");
        private readonly string seat3IPAddress = ConfigurationManager.AppSettings.Get("seat3IPAddress");
        private readonly string seat4IPAddress = ConfigurationManager.AppSettings.Get("seat4IPAddress");
        private readonly string seat5IPAddress = ConfigurationManager.AppSettings.Get("seat5IPAddress");
        private readonly string seat6IPAddress = ConfigurationManager.AppSettings.Get("seat6IPAddress");
        private readonly string seat7IPAddress = ConfigurationManager.AppSettings.Get("seat7IPAddress");
        private readonly string seat8IPAddress = ConfigurationManager.AppSettings.Get("seat8IPAddress");
        private readonly string seat9IPAddress = ConfigurationManager.AppSettings.Get("seat9IPAddress");

        private readonly string[] seatIPs = new string[]
        {
            ConfigurationManager.AppSettings.Get("seat1IPAddress"),
            ConfigurationManager.AppSettings.Get("seat2IPAddress"),
            ConfigurationManager.AppSettings.Get("seat3IPAddress"),
            ConfigurationManager.AppSettings.Get("seat4IPAddress"),
            ConfigurationManager.AppSettings.Get("seat5IPAddress"),
            ConfigurationManager.AppSettings.Get("seat6IPAddress"),
            ConfigurationManager.AppSettings.Get("seat7IPAddress"),
            ConfigurationManager.AppSettings.Get("seat8IPAddress"),
            ConfigurationManager.AppSettings.Get("seat9IPAddress")
        };

        private readonly string[] seatPorts = new string[]
        {
            "55001",
            "55001",
            "55001",
            "55001",
            "55001",
            "55001",
            "55001",
            "55001",
            "55001"
        };

        private readonly List<int> validAntennasList = new() { 9, 11, 13, 23, 25, 27, 37, 39, 41, 51, 53, 55, 73, 75, 77, 87, 89, 91, 101, 103, 105, 115, 117, 119,
                                                               137, 139, 141, 151, 153, 155, 165, 167, 169, 179, 181, 183, 57, 58, 59, 121, 122, 123, 185, 186, 187, 250, 251, 252, 253, 254, 255 };
        private readonly List<int> seat1AntennasList = new() { 9, 11, 13, 23, 25, 27 };
        private readonly List<int> seat2AntennasList = new() { 37, 39, 41, 51, 53, 55 };
        private readonly List<int> seat3AntennasList = new() { 73, 75, 77, 87, 89, 91 };
        private readonly List<int> seat4AntennasList = new() { 101, 103, 105, 115, 117, 119 };
        private readonly List<int> seat5AntennasList = new() { 137, 139, 141, 151, 153, 155 };
        private readonly List<int> seat6AntennasList = new() { 165, 167, 169, 179, 181, 183 };
        private readonly List<int> floatAntennasList = new() { 57, 58, 59, 121, 122, 123, 185, 186, 187 };
        private readonly List<int> exchangeAntennasList = new() { 250, 251, 252, 253, 254, 255 };

        private readonly Dictionary<int, decimal> validAntennasDict = new()
        {
            { 9, 0.0M },
            { 11, 0.1M },
            { 13, 0.2M },
            { 23, 0.3M },
            { 25, 0.4M },
            { 27, 0.5M },
            { 37, 1.0M },
            { 39, 1.1M },
            { 41, 1.2M },
            { 51, 1.3M },
            { 53, 1.4M },
            { 55, 1.5M },
            { 73, 2.0M },
            { 75, 2.1M },
            { 77, 2.2M },
            { 87, 2.3M },
            { 89, 2.4M },
            { 91, 2.5M },
            { 101, 3.0M },
            { 103, 3.1M },
            { 105, 3.2M },
            { 115, 3.3M },
            { 117, 3.4M },
            { 119, 3.5M },
            { 137, 4.0M },
            { 139, 4.1M },
            { 141, 4.2M },
            { 151, 4.3M },
            { 153, 4.4M },
            { 155, 4.5M },
            { 165, 5.0M },
            { 167, 5.1M },
            { 169, 5.2M },
            { 179, 5.3M },
            { 181, 5.4M },
            { 183, 5.5M },
            { 250, 12M },
            { 251, 12M },
            { 252, 12M },
            { 253, 12M },
            { 254, 12M },
            { 255, 12M },
            { 57, 14.1M },
            { 58, 14.1M },
            { 59, 14.1M },
            { 121, 14.1M },
            { 122, 14.1M },
            { 123, 14.1M },
            { 185, 14.1M },
            { 186, 14.1M },
            { 187, 14.1M }
        };

        private readonly Dictionary<int, decimal> seat2AntennasDict = new()
        {
        };

        private readonly Dictionary<int, decimal> seat3AntennasDict = new()
        {
        };

        private readonly Dictionary<int, decimal> seat4AntennasDict = new()
        {
        };

        private readonly Dictionary<int, decimal> seat5AntennasDict = new()
        {
        };

        private readonly Dictionary<int, decimal> seat6AntennasDict = new()
        {
        };

        private readonly Dictionary<int, decimal> floatAntennasDict = new()
        {
        };

        private static readonly Dictionary<string, string> CHIP_COLORS = new Dictionary<string, string>()
        {
            { "C1", "White" },
            { "C2", "Purple" },
            { "C3", "Light Pink" },
            { "C4", "Green" },
            { "C5", "Yellow" },
            { "C6", "Pink" },
            { "C7", "Blue" },
            { "C8", "Orange" },
            { "C9", "Gray" }
        };

        public enum SEAT
        {
            ONE,
            TWO,
            THREE,
            FOUR,
            FIVE,
            SIX,
            SEVEN,
            EIGHT,
            NINE,
            MAX_SEATS
        }

        // Denomination table
        private readonly double[] denomArray =
        {
            0.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00,
            0.01, 0.10, 1.00, 10.00, 100.00, 1000.00, 10000.00, 100000.00, 1000000.00, 10000000.00,
            0.02, 0.20, 2.00, 20.00, 200.00, 2000.00, 20000.00, 200000.00, 2000000.00, 20000000.00,
            0.00, 0.25, 2.50, 25.00, 250.00, 2500.00, 25000.00, 250000.00, 2500000.00, 25000000.00,
            0.04, 0.40, 4.00, 40.00, 400.00, 4000.00, 40000.00, 400000.00, 4000000.00, 40000000.00,
            0.05, 0.50, 5.00, 50.00, 500.00, 5000.00, 50000.00, 500000.00, 5000000.00, 50000000.00,
            0.06, 0.60, 6.00, 60.00, 600.00, 6000.00, 60000.00, 600000.00, 6000000.00, 60000000.00,
            0.00, 0.75, 7.50, 75.00, 750.00, 7500.00, 75000.00, 750000.00, 7500000.00, 75000000.00,
            0.08, 0.80, 8.00, 80.00, 800.00, 8000.00, 80000.00, 800000.00, 8000000.00, 80000000.00,
            0.09, 0.90, 9.00, 90.00, 900.00, 9000.00, 90000.00, 900000.00, 9000000.00, 90000000.00,
            0.03, 0.30, 3.00, 30.00, 300.00, 3000.00, 30000.00, 300000.00, 3000000.00, 30000000.00,
            0.07, 0.70, 7.00, 70.00, 700.00, 7000.00, 70000.00, 700000.00, 7000000.00, 70000000.00
        };

        private static bool exitSystem = false;

        #region Trap application termination
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

        private delegate bool EventHandler(CtrlType sig);
        private static EventHandler _exithandler;

        enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        /// <summary>
        /// Handler for Application Exit
        /// </summary>
        /// <param name="sig"></param>
        /// <returns></returns>
        private static bool ExitHandler(CtrlType sig)
        {
            Console.WriteLine($"\nExiting application due to {Enum.GetName(typeof(CtrlType), sig)}");
            logger.Debug($"Exiting application due to {Enum.GetName(typeof(CtrlType), sig)}");

            // Release the sockets and shut down the worker threads.
            GGHost.ShutDown();
            client.exitClicked = true;
            client.A2Timer.Enabled = false; // disable the A2 message timer

            if (client.Sockets[(int)SEAT.ONE] != null)
                client.Sockets[(int)SEAT.ONE].Receive(new byte[1000]); // if anything left on the socket

            for (int i = 0; i < num_seats; i++)
            {
                client.workerFlags[i] = false;
            }

            foreach (var sock in client.Sockets)
            {
                if (sock != null)
                {
                    sock.Shutdown(SocketShutdown.Both);
                    sock.Close();
                }
            }

            if (client.Seat12Socket != null)
            {
                client.Seat12Socket.Shutdown(SocketShutdown.Both);
                client.Seat12Socket.Close();
            }

            Thread.Sleep(10000);

            //allow main to run off
            exitSystem = true;

            //shutdown right away so there are no lingering threads
            Environment.Exit(-1);

            return true;
        }
        #endregion

        public HGTableInterface()
        {
            seat1Response = string.Empty;
            seat1WorkerFlag = false;
            seat1Worker = () => Seat1WorkerThread();

            Thread.Sleep(1000);

            seat2Response = string.Empty;
            seat2WorkerFlag = false;
            seat2Worker = () => Seat2WorkerThread();

            //for (int i = 0; i < num_seats; i++)
            //{
            //    seatResponses[i] = string.Empty;
            //    workerFlags[i]   = false;

            //    switch(i)
            //    {
            //        case 0:
            //            workerFuncs[(int)SEAT.ONE] = () => Seat1WorkerThread();
            //            break;

            //        case 1:
            //            workerFuncs[(int)SEAT.TWO] = () => Seat2WorkerThread();
            //            break;

            //        case 2:
            //            workerFuncs[(int)SEAT.THREE] = () => Seat3WorkerThread();
            //            break;

            //        case 3:
            //            workerFuncs[(int)SEAT.FOUR] = () => Seat4WorkerThread();
            //            break;

            //        case 4:
            //            workerFuncs[(int)SEAT.FIVE] = () => Seat5WorkerThread();
            //            break;

            //        case 5:
            //            workerFuncs[(int)SEAT.SIX] = () => Seat6WorkerThread();
            //            break;

            //        case 6:
            //            workerFuncs[(int)SEAT.SEVEN] = () => Seat7WorkerThread();
            //            break;

            //        case 7:
            //            workerFuncs[(int)SEAT.EIGHT] = () => Seat8WorkerThread();
            //            break;

            //        case 8:
            //            workerFuncs[(int)SEAT.NINE] = () => Seat9WorkerThread();
            //            break;

            //        default:
            //            Console.WriteLine($"Invalid Seat Num in HGTableInterface Constructor: {i}");
            //            break;
            //    }
            //}

            Seat12Response = string.Empty;
            Seat12WorkerFlag = false;
            Seat12WorkerFunc = () => Seat12WorkerThread();
        }

        public static int Main(String[] args)
        {
            client = new HGTableInterface();

            try
            {
                log4net.Config.XmlConfigurator.Configure();
                logger.Info("HGTranslator Starting Up....");
                Assembly execAssembly = Assembly.GetCallingAssembly();
                appName = execAssembly.GetName();
                Console.WriteLine($"{appName.Name} Version {appName.Version} Starting Up....");
                _exithandler += new EventHandler(ExitHandler);
                SetConsoleCtrlHandler(_exithandler, true);
                client.ImportChipDB();
                client.A2CommandBytes = client.Assemble0xA2Data();
                client.msgSendThread.Start();
                //client.InitWorkers();
                client.InitSeat1Worker();
                Thread.Sleep(2000);
                client.InitSeat2Worker();

                //for (int i = 0; i < num_seats; i++)
                //{
                //    client.seatResponses[i] = "No Chips Present";
                //}

                client.seat1Response = "No Chips Present";
                //client.seat2Response = "No Chips Present";

                client.Command_0xA2(client.seat1Socket);
                Thread.Sleep(100);
                client.Command_0xA2(client.seat2Socket);
                client.StartSeat1A2TimerTask();
                client.StartSeat2A2TimerTask();

                //long uid = 0x4803ad146c4d;
                //var xord = uid ^ 0x0d0e0f101112;
                //var unxord = xord ^ 0x0d0e0f101112;
                //var xord1 = EncryptUID(uid, 0x0d);
                //var xord2 = EncryptUID(xord1, 0x0d);

                GGHost.Start();

                //used for testing different chip sighting and expiring combinations
#if false
                Thread.Sleep(15000);
                Console.WriteLine("Sending Chip Message to GG Client");
                GGHost.Send("Hello...");
                //StringBuilder srcstr = new();
                //srcstr.Append("A2,00,40,00,02,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,48,03,AD,14,6C,4D,0D,61,00,00,00,00,00,00,00,48,01,B5,A4,6C,FB,0D,59,00,");
                //srcstr.Append("A2005B00010000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000048042883302C00B78F00");
                //client.numChipsSeen = 2;
                //var wrkstr = BuildChipMessage(srcstr);
                //var wrkstr = BuildChipMessage(Convert.FromHexString(srcstr.ToString()));
                //GGHost.Send(wrkstr);
                //Thread.Sleep(7000);
                //GGHost.Send($"Info,Tag:Expired 4803AD146C4D"); // send an expiration message for the previous sighting of this chip
                //GGHost.Send($"Info,Tag:Expired 4801B5A46CFB"); // send an expiration message for the previous sighting of this chip
                //srcstr.Length = 0;
                //srcstr.Append("A2,00,40,00,02,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,48,04,28,A1,7D,0C,0D,61,00,");
                //wrkstr = BuildChipSightedMessage(srcstr);
                //Console.WriteLine(wrkstr);
                //srcstr.Length = 0;
                //srcstr.Append("A2,00,40,00,02,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,48,04,28,A1,7D,0C,0D,61,00,00,00,00,00,00,00,48,04,28,A4,B1,1D,01,59,00,");
                //wrkstr = BuildChipSightedMessage(srcstr);
                //Console.WriteLine(wrkstr);
                //srcstr.Length = 0;
                //srcstr.Append("A2,00,40,00,02,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,48,04,28,A1,7D,0C,0D,61,00,");
                //wrkstr = BuildChipSightedMessage(srcstr);
                //Console.WriteLine(wrkstr);
                //srcstr.Length = 0;
                //srcstr.Append("A2,00,40,00,02,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,48,04,28,A1,7D,0C,0D,61,00,00,00,00,00,00,00,48,04,28,A4,B1,1D,01,59,00,");
                //wrkstr = BuildChipSightedMessage(srcstr);
                //Console.WriteLine(wrkstr);
#else
                //client.InitWorkers();
                //Thread.Sleep(2000);
                //Console.WriteLine("\nPress Any Key to Start Sending A2 Commands");
                //Console.ReadLine();
                //client.seatResponses[(int)SEAT.ONE] = "No Chips Present";
                //client.Command_0xA2();
                //client.StartA2TimerTask();
#endif

                while (!exitSystem)
                    Thread.Sleep(500);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Main Error: {e.Message}");
            }

            return 0;
        }

        private void Start1A2TimerTask()
        {
            client.A2Timer = new System.Timers.Timer(A2_TIMEOUT_VALUE);
            client.A2Timer.Elapsed += A2TimerEventHandler;
            client.A2Timer.AutoReset = false;
            client.A2Timer.Enabled = true;
        }

        private void StartSeat1A2TimerTask()
        {
            client.seat1A2Timer = new System.Timers.Timer(A2_TIMEOUT_VALUE);
            client.seat1A2Timer.Elapsed += Seat1A2TimerEventHandler;
            client.seat1A2Timer.AutoReset = false;
            client.seat1A2Timer.Enabled = true;
        }

        private void StartSeat2A2TimerTask()
        {
            client.seat2A2Timer = new System.Timers.Timer(A2_TIMEOUT_VALUE);
            client.seat2A2Timer.Elapsed += Seat2A2TimerEventHandler;
            client.seat2A2Timer.AutoReset = false;
            client.seat2A2Timer.Enabled = true;
        }

        private static void MessageSendWorker()
        {
            Console.WriteLine("GGHost Message Send Thread Started.");

            try
            {
                client.messageSendWorkerFlag = true;

                while (client.messageSendWorkerFlag)
                {
                    if (client.GGHostQueue.Count > 0)
                    {
                        GGHost.Send((string)client.GGHostQueue.Dequeue());
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"GGHost Message Send Thread Error: {e.Message}\n{e.StackTrace}");
            }
        }

        private void Seat1A2TimerEventHandler(Object source, ElapsedEventArgs args)
        {
            StringBuilder response = new StringBuilder();
            byte[] bytes = new byte[131072];
            int bytesRcvd;
            int expectedByteCount = 0;
            int totalBytesRcvd = 0;
            int chipsSeen = 0;

            if (client.FlushingTags == true) // Don't process chip messages while flushing tags
                return;

            client.seat1A2Timer.Enabled = false; // disable the timer while processing this message

            // Receive the response from the remote device.  
            try
            {
                bytesRcvd = client.seat1Socket.Receive(bytes);
                //Console.WriteLine($"A2 Timer Event Fired, bytes Received from Seat 1 = {bytesRcvd}");

                if (bytesRcvd > 0)
                {
                    //logger.Info($"Recieved {bytesRcvd} byte(s) from Table for Seat {seat + 1}, Response = 0x{string.Format("{0:X2}", bytes[0])}");
                    //Console.WriteLine($"Recieved {bytesRcvd} byte(s) from Table for Seat 1, Response = 0x{string.Format("{0:X2}", bytes[0])}");

                    if (bytes[0] == 0xA2) // A2 response
                    {
                        response.Length = 0; // reset the response holder
                        expectedByteCount = (bytes[1] * 256) + bytes[2]; // get the number of bytes we are expecting in this response
                        chipsSeen = (bytes[3] * 256) + bytes[4];
                        //logger.Info($"Expecting to Recieve {expectedByteCount} bytes");
                        //Console.WriteLine($"Expecting to Recieve for Seat 1 {expectedByteCount} bytes");

                        if (expectedByteCount == bytesRcvd) // if we have the number of bytes we are expecting
                        {
                            for (int i = 0; i < bytesRcvd; i++)
                                response.Append(String.Format("{0:X2}", bytes[i])); // get the bytes from the message
                        }
                        else
                        {
                            //logger.Info($"Recieving Extra Bytes, Still Need {expectedByteCount - bytesRcvd} bytes");
                            //Console.WriteLine($"Receiving Extra Bytes, Still Need {expectedByteCount - bytesRcvd} bytes");

                            for (int i = 0; i < bytesRcvd; i++) // for the bytes that we did receive
                                response.Append(String.Format("{0:X2}", bytes[i])); // save them

                            totalBytesRcvd = bytesRcvd; // track the total bytes received

                            do // receive the rest of the expected bytes
                            {
                                Array.Clear(bytes, 0, bytes.Length); // setup for more bytes to be received
                                bytesRcvd = client.seat1Socket.Receive(bytes); // receive additional bytes
                                                                               //Console.WriteLine($"Received {bytesRcvd} more bytes");
                                totalBytesRcvd += bytesRcvd; // update the total bytes received
                                //Console.WriteLine($"totalbytesRcvd = {totalBytesRcvd}");

                                for (int i = 0; i < bytesRcvd; i++)
                                    response.Append(String.Format("{0:X2}", bytes[i])); // append the bytes to the response string

                            } while (totalBytesRcvd < expectedByteCount);
                        }

                        //client.seatResponses[(int)SEAT.ONE] = BuildChipMessage(client.respStr); // build a response message
                    }
                }

                client.seat1Response = BuildChipMessage(Convert.FromHexString(response.ToString())); // build a response message
                Command_0xA2(client.seat1Socket); // send another A2 command
            }
            catch (Exception e)
            {
                Console.WriteLine($"Seat 1 A2 Timer Event Handler Error: {e.Message}");
                return;
            }

            client.seat1A2Timer.Enabled = true; // enable the timer
        }

        private void Seat2A2TimerEventHandler(Object source, ElapsedEventArgs args)
        {
            StringBuilder response = new StringBuilder();
            byte[] bytes = new byte[131072];
            int bytesRcvd;
            int expectedByteCount = 0;
            int totalBytesRcvd = 0;
            int chipsSeen = 0;

            if (client.FlushingTags == true) // Don't process chip messages while flushing tags
                return;

            client.seat2A2Timer.Enabled = false; // disable the timer while processing this message

            // Receive the response from the remote device.  
            try
            {
                bytesRcvd = client.seat2Socket.Receive(bytes);
                //Console.WriteLine($"A2 Timer Event Fired, bytes Received from Seat 1 = {bytesRcvd}");

                if (bytesRcvd > 0)
                {
                    //logger.Info($"Recieved {bytesRcvd} byte(s) from Table for Seat {seat + 1}, Response = 0x{string.Format("{0:X2}", bytes[0])}");
                    //Console.WriteLine($"Recieved {bytesRcvd} byte(s) from Table for Seat 1, Response = 0x{string.Format("{0:X2}", bytes[0])}");

                    if (bytes[0] == 0xA2) // A2 response
                    {
                        response.Length = 0; // reset the response holder
                        expectedByteCount = (bytes[1] * 256) + bytes[2]; // get the number of bytes we are expecting in this response
                        chipsSeen = (bytes[3] * 256) + bytes[4];
                        //logger.Info($"Expecting to Recieve {expectedByteCount} bytes");
                        //Console.WriteLine($"Expecting to Recieve for Seat 1 {expectedByteCount} bytes");

                        if (expectedByteCount == bytesRcvd) // if we have the number of bytes we are expecting
                        {
                            for (int i = 0; i < bytesRcvd; i++)
                                response.Append(String.Format("{0:X2}", bytes[i])); // get the bytes from the message
                        }
                        else
                        {
                            //logger.Info($"Recieving Extra Bytes, Still Need {expectedByteCount - bytesRcvd} bytes");
                            //Console.WriteLine($"Receiving Extra Bytes, Still Need {expectedByteCount - bytesRcvd} bytes");

                            for (int i = 0; i < bytesRcvd; i++) // for the bytes that we did receive
                                response.Append(String.Format("{0:X2}", bytes[i])); // save them

                            totalBytesRcvd = bytesRcvd; // track the total bytes received

                            do // receive the rest of the expected bytes
                            {
                                Array.Clear(bytes, 0, bytes.Length); // setup for more bytes to be received
                                bytesRcvd = client.seat2Socket.Receive(bytes); // receive additional bytes
                                                                               //Console.WriteLine($"Received {bytesRcvd} more bytes");
                                totalBytesRcvd += bytesRcvd; // update the total bytes received
                                //Console.WriteLine($"totalbytesRcvd = {totalBytesRcvd}");

                                for (int i = 0; i < bytesRcvd; i++)
                                    response.Append(String.Format("{0:X2}", bytes[i])); // append the bytes to the response string

                            } while (totalBytesRcvd < expectedByteCount);
                        }
                    }
                }

                client.seat2Response = BuildChipMessage(Convert.FromHexString(response.ToString())); // build a response message
                Command_0xA2(client.seat2Socket); // send another A2 command
            }
            catch (Exception e)
            {
                Console.WriteLine($"Seat 2 A2 Timer Event Handler Error: {e.Message}");
                return;
            }

            client.seat2A2Timer.Enabled = true; // enable the timer
        }

        private void A2TimerEventHandler(Object source, ElapsedEventArgs args)
        {
            byte[] bytes = new byte[131072];
            int bytesRcvd;
            int expectedByteCount = 0;
            int totalBytesRcvd = 0;

            if (client.FlushingTags == true) // Don't process chip messages while flushing tags
                return;

            client.seat1A2Timer.Enabled = false; // disable the timer while processing this message

            // Receive the response from the remote device.  
            try
            {
                for (int seat = 0; seat < num_seats; seat++)
                {
                    bytesRcvd = client.Sockets[seat].Receive(bytes);
                    //Console.WriteLine($"A2 Timer Event Fired, bytes Received from Client {seat + 1} = {bytesRcvd}");

                    if (bytesRcvd > 0)
                    {
                        //logger.Info($"Recieved {bytesRcvd} byte(s) from Table for Seat {seat + 1}, Response = 0x{string.Format("{0:X2}", bytes[0])}");
                        //Console.WriteLine($"Recieved {bytesRcvd} byte(s) from Table for Seat {seat + 1}, Response = 0x{string.Format("{0:X2}", bytes[0])}");

                        if (bytes[0] == 0xA2) // A2 response
                        {
                            client.respStr.Length = 0; // reset the response holder
                            expectedByteCount = (bytes[1] * 256) + bytes[2]; // get the number of bytes we are expecting in this response
                            numChipsSeen = (bytes[3] * 256) + bytes[4];
                            //logger.Info($"Expecting to Recieve {expectedByteCount} bytes");
                            //Console.WriteLine($"Expecting to Recieve for Seat {seat + 1} {expectedByteCount} bytes");

                            if (expectedByteCount == bytesRcvd) // if we have the number of bytes we are expecting
                            {
                                for (int i = 0; i < bytesRcvd; i++)
                                    client.respStr.Append(String.Format("{0:X2}", bytes[i])); // get the bytes from the message
                            }
                            else
                            {
                                //logger.Info($"Recieving Extra Bytes, Still Need {expectedByteCount - bytesRcvd} bytes");
                                //Console.WriteLine($"Receiving Extra Bytes, Still Need {expectedByteCount - bytesRcvd} bytes");

                                for (int i = 0; i < bytesRcvd; i++) // for the bytes that we did receive
                                    client.respStr.Append(String.Format("{0:X2}", bytes[i])); // save them

                                totalBytesRcvd = bytesRcvd; // track the total bytes received

                                do // receive the rest of the expected bytes
                                {
                                    Array.Clear(bytes, 0, bytes.Length); // setup for more bytes to be received
                                    bytesRcvd = client.Sockets[seat].Receive(bytes); // receive additional bytes
                                                                                     //Console.WriteLine($"Received {bytesRcvd} more bytes");
                                    totalBytesRcvd += bytesRcvd; // update the total bytes received
                                    //Console.WriteLine($"totalbytesRcvd = {totalBytesRcvd}");

                                    for (int i = 0; i < bytesRcvd; i++)
                                        client.respStr.Append(String.Format("{0:X2}", bytes[i])); // append the bytes to the response string

                                } while (totalBytesRcvd < expectedByteCount);
                            }

                            //client.seatResponses[(int)SEAT.ONE] = BuildChipMessage(client.respStr); // build a response message
                        }
                    }

                    client.seatResponses[seat] = BuildChipMessage(Convert.FromHexString(client.respStr.ToString())); // build a response message
                }

                Command_0xA2(); // send another A2 command
            }
            catch (Exception e)
            {
                Console.WriteLine($"A2 Timer Event Handler Error: {e.Message}");
                return;
            }

            client.A2Timer.Enabled = true; // enable the timer
        }

        /// <summary>
        /// Build a MARS formatted chip message from the A2 response
        /// </summary>
        /// <param name="respStr"></param>
        /// <returns></returns>
        private static string BuildChipMessage(byte[] bytes)
        {
            var recpointer = HEADER_SIZE;
            string wrkstr = string.Empty;
            string replystr = string.Empty;
            string[] chipsOnTable = new string[1]; // array of chips currently on the table by uid
            Dictionary<string, string> chipLocationNow = new Dictionary<string, string>(); // structure as follows: { uid, axis }

            //Console.WriteLine($"bytes.Length = {bytes.Length}");

            if (bytes.Length > HEADER_SIZE)
            {
                client.numChipsSeen = (bytes.Length - HEADER_SIZE) / RECORD_LEN;
                //Console.WriteLine($"{client.numChipsSeen} Chip(s) Seen");
            }

            try
            {
                if (client.numChipsSeen > 0) // if there are chips on an antenna
                {
                    Array.Resize(ref chipsOnTable, client.numChipsSeen);

                    for (int i = 0; i < client.numChipsSeen; i++)
                    {
                        byte[] uidbytes = new byte[6];
                        var antIndex = 0;
                        var MARSAntIndex = 99M;
                        var index = 0;

                        for (int j = recpointer + (SN_LEN - 6); j < recpointer + SN_LEN; j++) // Offset to SN bytes.  Don't grab the 6 leading 0s
                            uidbytes[index++] = bytes[j];

                        antIndex = (bytes[recpointer + SN_LEN] * 256) + bytes[recpointer + SN_LEN + 1];

                        // Check to see which seat the antenna index is on and if it should be ignored or if it's group is disabled.
                        if (!client.validAntennasDict.ContainsKey(antIndex))
                            continue;

                        MARSAntIndex = client.validAntennasDict[antIndex];

                        if (client.axisToIgnoreList == null)
                        {
                            Console.WriteLine("Axis Groups Have Not Been Defined!  Send MARS Init to Controller.");
                            continue;
                        }

                        if (client.axisToIgnoreList.Contains(MARSAntIndex)) // check the axis ignore list first
                            continue;

                        var group = Math.Floor(MARSAntIndex); // get the axis group 0 - 9
                        var groupNum = client.groupNumberDict[(int)group]; // get the group number based on active groups in the Axis List
                                                                           //Console.WriteLine($"Processing Chip(s) for Group {groupNum}");

                        if (client.groupPriorityList == null)
                        {
                            Console.WriteLine("Axis Groups Have Not Been Defined!  Send MARS Init to Controller.");
                            continue;
                        }

                        if (client.groupPriorityList.ElementAt(groupNum) == 0) // ignore groups with a priority of 0
                            continue;

                        recpointer += RECORD_LEN;
                        chipsOnTable[i] = Convert.ToHexString(uidbytes); // save the UID pulled from the response
                        //Console.WriteLine($"UID of Chip = {chipsOnTable[i]}");
                        //Console.WriteLine($"Chip on Spot = {antIndex}");

                        if (!client.chipLocationPrev.ContainsKey(chipsOnTable[i])) // if this chip wasn't previously on the table
                        {
                            //Console.WriteLine($"Location Now = {antIndex}");
                            chipLocationNow.Add(chipsOnTable[i], antIndex.ToString()); // add this chip to the current location dict keyed by uid
                            replystr += $"Info,Reply:Axis {MARSAntIndex}, " +
                                        $"Timestamp 7fff, SpecificID {chipsOnTable[i]}, " +
                                        $"ReadAddress 000a, Data: 0000 {getDenomHex(client.chipsDict[chipsOnTable[i]].ChipValue):X4}\r\n";
                        }
                        else
                        {
                            //Console.WriteLine($"Current Loc = {antIndex}");
                            //Console.WriteLine($"Prev Loc = {client.chipLocationPrev[chipsOnTable[i]]}");

                            if (antIndex.ToString() != client.chipLocationPrev[chipsOnTable[i]]) // if the current location is different than the previous location
                            {
                                //Console.WriteLine($"Current Loc different than Previous Loc");
                                //GGHost.Send($"Info,Tag:Expired {chipsOnTable[i]}\r\n"); // send an expiration message for the previous sighting of this chip
                                client.GGHostQueue.Enqueue($"Info,Tag:Expired {chipsOnTable[i]}\r\n"); // send an expiration message for the previous sighting of this chip
                                Console.WriteLine($"Info,Tag:Expired {chipsOnTable[i]}");

                                replystr += $"Info,Reply:Axis {client.validAntennasDict[Convert.ToInt32(antIndex.ToString())]}, " +
                                            $"Timestamp 7fff, SpecificID {chipsOnTable[i]}, " +
                                            $"ReadAddress 000a, Data: 0000 {getDenomHex(client.chipsDict[chipsOnTable[i]].ChipValue):X4}\r\n";
                            }

                            chipLocationNow.Add(chipsOnTable[i], antIndex.ToString()); // add to current location list
                        }
                    }

                    // check to see if any chips that were present previously have been removed from the table
                    foreach (var chipprev in client.chipLocationPrev)
                    {
                        if (!chipLocationNow.ContainsKey(chipprev.Key))
                        {
                            //Console.WriteLine("chipprev not equal to chiplocationnow");
                            //GGHost.Send($"Info,Tag:Expired {chipprev.Key}\r\n"); // send an expiration message for the previous sighting of this chip
                            client.GGHostQueue.Enqueue($"Info,Tag:Expired {chipprev.Key}\r\n"); // send an expiration message for the previous sighting of this chip
                            Console.WriteLine($"Info,Tag:Expired {chipprev.Key}"); // ToDo - send an expiration message upstream
                        }
                    }

                    client.chipLocationPrev = new Dictionary<string, string>(chipLocationNow); // save current view of the chips on the table
                }
                else
                {
                    if (client.chipLocationPrev.Count > 0) // if there were chips on the table previously
                    {
                        //Console.WriteLine("0 Chips Seen, Sending Expirations for Each Chip Removed");
                        foreach (var chip in client.chipLocationPrev)
                        {
                            //GGHost.Send($"Info,Tag:Expired {chip.Key}\r\n"); // send an expiration message for the previous sighting of this chip
                            client.GGHostQueue.Enqueue($"Info,Tag:Expired {chip.Key}\r\n"); // send an expiration message for the previous sighting of this chip
                            Console.WriteLine($"Info,Tag:Expired {chip.Key}"); // no chips on the table now, so send an expiration for each chip that was there
                        }

                        client.chipLocationPrev.Clear(); // clear the memory
                        //replystr = respStr.ToString();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"BuildChipSightedMessage Error: {e.Message}\n{e.StackTrace}");
            }

            return replystr;
        }

        internal void FlushTags()
        {
            if (client.chipLocationPrev.Any())
            {
                //Console.WriteLine($"Flushing {client.chipLocationPrev.Count} Tags!"); // Expire all tags and then re-sight them

                foreach (var chip in client.chipLocationPrev)
                {
                    Console.WriteLine($"Info,Tag:Expired {chip.Key}"); // no chips on the table now, so send an expiration for each chip that was there
                    client.GGHostQueue.Enqueue($"Info,Tag:Expired {chip.Key}\r\n"); // send an expiration message for the previous sighting of this chip
                }

                client.chipLocationPrev.Clear(); // clear the dictionary
            }
 
            client.FlushingTags = false;
        }

        private void InitWorkers()
        {
            byte[] bytes = new byte[1];

            try
            {
                for (int i = 0; i < num_seats; i++) // for each of the main readers
                {
                    client.seatResponses[i] = string.Empty;
                    client.workerThreads[i] = new Thread(workerFuncs[i]);
                    client.workerThreads[i].Start();

                    while (client.Sockets[i] == null) // wait for socket to be initialized
                        Thread.Sleep(25);

                    Thread.Sleep(3000);

                    if (client.Sockets[i].Connected)
                    {
                        Console.WriteLine($"Connected To Seat {i + 1}!");
                        logger.Debug($"Connected To Seat {i + 1}!");
                    }
                    else
                        Console.WriteLine($"Unable to Connect to Seat {i + 1}");

                    Thread.Sleep(1000);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"InitWorkers Error: {e.Message}");
            }
        }

        private void InitSeat1Worker()
        {
            byte[] bytes = new byte[1];

            try
            {
                client.seat1Response = string.Empty;
                client.seat1WorkerThread = new Thread(seat1Worker);
                client.seat1WorkerThread.Start();

                while (client.seat1Socket == null) // wait for socket to be initialized
                    Thread.Sleep(25);

                Thread.Sleep(3000);

                if (client.seat1Socket.Connected)
                {
                    Console.WriteLine($"Connected To Seat 1!");
                    logger.Debug($"Connected To Seat 1!");
                }
                else
                    Console.WriteLine($"Unable to Connect to Seat 1");

                Thread.Sleep(1000);
            }
            catch (Exception e)
            {
                Console.WriteLine($"InitSeat1Worker Error: {e.Message}");
            }
        }

        private void InitSeat2Worker()
        {
            byte[] bytes = new byte[1];

            try
            {
                client.seat2Response = string.Empty;
                client.seat2WorkerThread = new Thread(seat2Worker);
                client.seat2WorkerThread.Start();

                while (client.seat2Socket == null) // wait for socket to be initialized
                    Thread.Sleep(25);

                Thread.Sleep(3000);

                if (client.seat2Socket.Connected)
                {
                    Console.WriteLine($"Connected To Seat 2!");
                    logger.Debug($"Connected To Seat 2!");
                }
                else
                    Console.WriteLine($"Unable to Connect to Seat 2");

                Thread.Sleep(1000);
            }
            catch (Exception e)
            {
                Console.WriteLine($"InitSeat2Worker Error: {e.Message}");
            }
        }

        /// <summary>
        /// Thread to monitor for a message from the table for seat 1
        /// </summary>
        private static void Seat1WorkerThread()
        {
            Console.WriteLine($"Seat 1 Worker Started.");
            logger.Debug($"Seat 1 Worker Started.");

            try
            {
                client.seat1WorkerFlag = true; 
                IPAddress ipAddress = IPAddress.Parse(client.seatIPs[(int)SEAT.ONE]);
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, Convert.ToInt32(client.seatPorts[(int)SEAT.ONE]));
                Console.WriteLine($"Attempting to Connect to Seat 1 at {ipAddress}:{client.seatPorts[(int)SEAT.ONE]}");
                logger.Debug($"Attempting to Connect to Seat 1 at {ipAddress}:{client.seatPorts[(int)SEAT.ONE]}");

                // Create a TCP/IP socket.  
                client.seat1Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Connect to the remote endpoint.  
                client.seat1Socket.Connect(remoteEP);

                while (client.seat1WorkerFlag)
                {
                    if (!client.seat1Response.Equals(string.Empty)) // if the message response string is not empty
                    {
                        var message = client.seat1Response;      // save the incoming message
                        client.seat1Response = string.Empty; // clear for the next message

                        Console.WriteLine($"Seat 1: {message}");

                        if (message.Contains("Info,Reply:"))
                        {
                            //GGHost.Send(message);
                            client.GGHostQueue.Enqueue(message);
                        }
                        
                        //logger.Info($"Response for Seat 1 Received : {message}");
                    }

                    Thread.Sleep(10);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Seat 1 Worker Error: {e.Message}\n{e}");
                logger.Debug($"Seat 1 Worker Error: {e.Message}\n{e}");
            }
        }

        /// <summary>
        /// Thread to monitor for a message from the table for seat 2
        /// </summary>
        private static void Seat2WorkerThread()
        {
            Console.WriteLine($"Seat 2 Worker Started.");
            logger.Debug($"Seat 2 Worker Started.");

            try
            {
                client.seat2WorkerFlag = true;
                IPAddress ipAddress = IPAddress.Parse(client.seatIPs[(int)SEAT.TWO]);
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, Convert.ToInt32(client.seatPorts[(int)SEAT.TWO]));
                Console.WriteLine($"Attempting to Connect to Seat 2 at {ipAddress}:{client.seatPorts[(int)SEAT.TWO]}");
                logger.Debug($"Attempting to Connect to Seat 2 at {ipAddress}:{client.seatPorts[(int)SEAT.TWO]}");

                // Create a TCP/IP socket.  
                client.seat2Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Connect to the remote endpoint.  
                client.seat2Socket.Connect(remoteEP);

                while (client.seat2WorkerFlag)
                {
                    if (!client.seat2Response.Equals(string.Empty)) // if the message response string is not empty
                    {
                        var message = client.seat2Response;      // save the incoming message
                        client.seat2Response = string.Empty; // clear for the next message

                        Console.WriteLine($"Seat 2: {message}");

                        if (message.Contains("Info,Reply:"))
                        {
                            //GGHost.Send(message);
                            client.GGHostQueue.Enqueue(message);
                        }

                        //logger.Info($"Response for Seat 1 Received : {message}");
                    }

                    Thread.Sleep(10);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Seat 2 Worker Error: {e.Message}\n{e}");
                logger.Debug($"Seat 2 Worker Error: {e.Message}\n{e}");
            }
        }

        /// <summary>
        /// Thread to monitor for a message from the table for seat 3
        /// </summary>
        private static void Seat3WorkerThread()
        {
            Console.WriteLine($"Seat 3 Worker Started.");

            try
            {
                client.workerFlags[(int)SEAT.THREE] = true;
                IPAddress ipAddress = IPAddress.Parse(client.seatIPs[(int)SEAT.THREE]);
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, Convert.ToInt32(client.seatPorts[(int)SEAT.THREE]));
                Console.WriteLine($"Attempting to Connect to Seat 3 at {ipAddress}:{client.seatPorts[(int)SEAT.THREE]}");

                // Create a TCP/IP socket.  
                client.Sockets[(int)SEAT.THREE] = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Connect to the remote endpoint.  
                client.Sockets[(int)SEAT.THREE].Connect(remoteEP);

                while (client.workerFlags[(int)SEAT.THREE])
                {
                    if (!client.seatResponses[(int)SEAT.THREE].Equals(string.Empty)) // if the message response string is not empty
                    {
                        var message = client.seatResponses[(int)SEAT.THREE];      // save the incoming message
                        client.seatResponses[(int)SEAT.THREE] = string.Empty; // clear for the next message
                        // Write the response to the console.  
                        Console.WriteLine($"Response for Seat 3 Received : {message}");
                    }

                    Thread.Sleep(10);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Seat 3 Worker Error: {e.Message}\n{e}");
            }
        }

        /// <summary>
        /// Thread to monitor for a message from the table for seat 4
        /// </summary>
        private static void Seat4WorkerThread()
        {
            Console.WriteLine($"Seat 4 Worker Started.");

            try
            {
                client.workerFlags[(int)SEAT.FOUR] = true;
                IPAddress ipAddress = IPAddress.Parse(client.seatIPs[(int)SEAT.FOUR]);
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, Convert.ToInt32(client.seatPorts[(int)SEAT.FOUR]));
                Console.WriteLine($"Attempting to Connect to Seat 4 at {ipAddress}:{client.seatPorts[(int)SEAT.FOUR]}");

                // Create a TCP/IP socket.  
                client.Sockets[(int)SEAT.FOUR] = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Connect to the remote endpoint.  
                client.Sockets[(int)SEAT.FOUR].Connect(remoteEP);

                while (client.workerFlags[(int)SEAT.FOUR])
                {
                    if (!client.seatResponses[(int)SEAT.FOUR].Equals(string.Empty)) // if the message response string is not empty
                    {
                        var message = client.seatResponses[(int)SEAT.FOUR];      // save the incoming message
                        client.seatResponses[(int)SEAT.FOUR] = string.Empty; // clear for the next message
                        // Write the response to the console.  
                        Console.WriteLine($"Response for Seat 4 Received : {message}");
                    }

                    Thread.Sleep(10);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Seat 4 Worker Error: {e.Message}\n{e}");
            }
        }

        /// <summary>
        /// Thread to monitor for a message from the table for seat 5
        /// </summary>
        private static void Seat5WorkerThread()
        {
            Console.WriteLine($"Seat 5 Worker Started.");

            try
            {
                client.workerFlags[(int)SEAT.FIVE] = true;
                IPAddress ipAddress = IPAddress.Parse(client.seatIPs[(int)SEAT.FIVE]);
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, Convert.ToInt32(client.seatPorts[(int)SEAT.FIVE]));
                Console.WriteLine($"Attempting to Connect to Seat 5 at {ipAddress}:{client.seatPorts[(int)SEAT.FIVE]}");

                // Create a TCP/IP socket.  
                client.Sockets[(int)SEAT.FIVE] = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Connect to the remote endpoint.  
                client.Sockets[(int)SEAT.FIVE].Connect(remoteEP);

                while (client.workerFlags[(int)SEAT.FIVE])
                {
                    if (!client.seatResponses[(int)SEAT.FIVE].Equals(string.Empty)) // if the message response string is not empty
                    {
                        var message = client.seatResponses[(int)SEAT.FIVE];      // save the incoming message
                        client.seatResponses[(int)SEAT.FIVE] = string.Empty; // clear for the next message
                        // Write the response to the console.  
                        Console.WriteLine($"Response for Seat 5 Received : {message}");
                    }

                    Thread.Sleep(10);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Seat 5 Worker Error: {e.Message}\n{e}");
            }
        }

        /// <summary>
        /// Thread to monitor for a message from the table for seat 6
        /// </summary>
        private static void Seat6WorkerThread()
        {
            Console.WriteLine($"Seat 6 Worker Started.");

            try
            {
                client.workerFlags[(int)SEAT.SIX] = true;
                IPAddress ipAddress = IPAddress.Parse(client.seatIPs[(int)SEAT.SIX]);
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, Convert.ToInt32(client.seatPorts[(int)SEAT.SIX]));
                Console.WriteLine($"Attempting to Connect to Seat 6 at {ipAddress}:{client.seatPorts[(int)SEAT.SIX]}");

                // Create a TCP/IP socket.  
                client.Sockets[(int)SEAT.SIX] = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Connect to the remote endpoint.  
                client.Sockets[(int)SEAT.SIX].Connect(remoteEP);

                while (client.workerFlags[(int)SEAT.SIX])
                {
                    if (!client.seatResponses[(int)SEAT.SIX].Equals(string.Empty)) // if the message response string is not empty
                    {
                        var message = client.seatResponses[(int)SEAT.SIX];      // save the incoming message
                        client.seatResponses[(int)SEAT.SIX] = string.Empty; // clear for the next message
                        // Write the response to the console.  
                        Console.WriteLine($"Response for Seat 6 Received : {message}");
                    }

                    Thread.Sleep(10);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Seat 6 Worker Error: {e.Message}\n{e}");
            }
        }

        /// <summary>
        /// Thread to monitor for a message from the table for seat 7
        /// </summary>
        private static void Seat7WorkerThread()
        {
            Console.WriteLine($"Seat 7 Worker Started.");

            try
            {
                client.workerFlags[(int)SEAT.SEVEN] = true;
                IPAddress ipAddress = IPAddress.Parse(client.seatIPs[(int)SEAT.SEVEN]);
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, Convert.ToInt32(client.seatPorts[(int)SEAT.SEVEN]));
                Console.WriteLine($"Attempting to Connect to Seat 7 at {ipAddress}:{client.seatPorts[(int)SEAT.SEVEN]}");

                // Create a TCP/IP socket.  
                client.Sockets[(int)SEAT.SEVEN] = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Connect to the remote endpoint.  
                client.Sockets[(int)SEAT.SEVEN].Connect(remoteEP);

                while (client.workerFlags[(int)SEAT.SEVEN])
                {
                    if (!client.seatResponses[(int)SEAT.SEVEN].Equals(string.Empty)) // if the message response string is not empty
                    {
                        var message = client.seatResponses[(int)SEAT.SEVEN];      // save the incoming message
                        client.seatResponses[(int)SEAT.SEVEN] = string.Empty; // clear for the next message
                        // Write the response to the console.  
                        Console.WriteLine($"Response for Seat 7 Received : {message}");
                    }

                    Thread.Sleep(10);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Seat 7 Worker Error: {e.Message}\n{e}");
            }
        }

        /// <summary>
        /// Thread to monitor for a message from the table for seat 8
        /// </summary>
        private static void Seat8WorkerThread()
        {
            Console.WriteLine($"Seat 8 Worker Started.");

            try
            {
                client.workerFlags[(int)SEAT.EIGHT] = true;
                IPAddress ipAddress = IPAddress.Parse(client.seatIPs[(int)SEAT.EIGHT]);
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, Convert.ToInt32(client.seatPorts[(int)SEAT.EIGHT]));
                Console.WriteLine($"Attempting to Connect to Seat 8 at {ipAddress}:{client.seatPorts[(int)SEAT.EIGHT]}");

                // Create a TCP/IP socket.  
                client.Sockets[(int)SEAT.EIGHT] = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Connect to the remote endpoint.  
                client.Sockets[(int)SEAT.EIGHT].Connect(remoteEP);

                while (client.workerFlags[(int)SEAT.EIGHT])
                {
                    if (!client.seatResponses[(int)SEAT.EIGHT].Equals(string.Empty)) // if the message response string is not empty
                    {
                        var message = client.seatResponses[(int)SEAT.EIGHT];      // save the incoming message
                        client.seatResponses[(int)SEAT.EIGHT] = string.Empty; // clear for the next message
                        // Write the response to the console.  
                        Console.WriteLine($"Response for Seat 8 Received : {message}");
                    }

                    Thread.Sleep(10);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Seat 8 Worker Error: {e.Message}\n{e}");
            }
        }

        /// <summary>
        /// Thread to monitor for a message from the table for seat 9
        /// </summary>
        private static void Seat9WorkerThread()
        {
            Console.WriteLine($"Seat 9 Worker Started.");

            try
            {
                client.workerFlags[(int)SEAT.NINE] = true;
                IPAddress ipAddress = IPAddress.Parse(client.seatIPs[(int)SEAT.NINE]);
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, Convert.ToInt32(client.seatPorts[(int)SEAT.NINE]));
                Console.WriteLine($"Attempting to Connect to Seat 9 at {ipAddress}:{client.seatPorts[(int)SEAT.NINE]}");

                // Create a TCP/IP socket.  
                client.Sockets[(int)SEAT.NINE] = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Connect to the remote endpoint.  
                client.Sockets[(int)SEAT.NINE].Connect(remoteEP);

                while (client.workerFlags[(int)SEAT.NINE])
                {
                    if (!client.seatResponses[(int)SEAT.NINE].Equals(string.Empty)) // if the message response string is not empty
                    {
                        var message = client.seatResponses[(int)SEAT.NINE];      // save the incoming message
                        client.seatResponses[(int)SEAT.NINE] = string.Empty; // clear for the next message
                        // Write the response to the console.  
                        Console.WriteLine($"Response for Seat 9 Received : {message}");
                    }

                    Thread.Sleep(10);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Seat 9 Worker Error: {e.Message}\n{e}");
            }
        }

        /// <summary>
        /// Thread to monitor for a message from the table for seat 12
        /// </summary>
        private static void Seat12WorkerThread()
        {
            Console.WriteLine("Seat 12 Worker Started.");

            try
            {
                client.Seat12WorkerFlag = true;
                IPAddress ipAddress = IPAddress.Parse(client.seat1IPAddress);
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, Convert.ToInt32(client.Seat12Port));
                Console.WriteLine($"Attempting to Connect to Seat 12 at {ipAddress}:{client.Seat12Port}");

                // Create a TCP/IP socket.  
                client.Seat12Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Connect to the remote endpoint.  
                client.Seat12Socket.Connect(remoteEP);

                while (client.Seat12WorkerFlag)
                {
                    if (!client.Seat12Response.Equals(string.Empty)) // if the message response string is not empty
                    {
                        var message = client.Seat12Response;      // save the incoming message
                        client.Seat12Response = string.Empty; // clear for the next message
                        // Write the response to the console.  
                        Console.WriteLine($"Response for Seat 12 Received : {message}");
                    }

                    Thread.Sleep(10);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Seat 12 Worker Error: {e.Message}\n{e}");
            }
        }

        private bool ImportChipDB()
        {
            string[] msga;
            string msgb;
            string[] msgba;

            var path = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).LocalPath + @"\Resources\ChipdbRoulette2.txt";

            try
            {
                msga = File.ReadAllLines(path, Encoding.UTF8);

                if (msga.Length > 0)
                {
                    for (int i = 0; i < msga.Length; i++)
                    {
                        ChipData chipData = new ChipData();
                        msgb = msga[i];
                        msgb.Trim('\r');
                        msgb.Trim('\n');

                        if (msgb.Length == 0)
                            break;

                        msgba = msgb.Split(",");
                        chipData.ChipType = msgba[1];
                        chipData.ChipValue = msgba[2];
                        client.chipsDict.Add(msgba[0].Substring(12), chipData); // skip the 12 leading zeros of the chip sn record
                    }

                    return true;
                }

            }
            catch (Exception e)
            {
                logger.Error($"Import Chip DB Error: {e.Message}");
                Console.WriteLine($"Import Chip DB Error: {e.Message}");
            }

            return false;
        }

        private void Command_0xA2(Socket sock)
        {
            try
            {
                Send(sock, client.A2CommandBytes);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Command_0xA2 Error: {e.Message}");
            }
        }

        private void Command_0xA2()
        {
            try
            {
                for (int i = 0; i < num_seats; i++)
                {
                    Send(client.Sockets[i], client.A2CommandBytes);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Command_0xA2 Error: {e.Message}");
            }
        }

        public byte[] Assemble0xA2Data()
        {
            var ROULETTE_GEN_3_ID = 0x0B;
            byte[] temp = new byte[BYTES_IN_A2_COMMAND];
            byte[] bitflags = new byte[65];

            try
            {
                bitflags[0] = 0xFF; bitflags[1] = 0xFF; bitflags[2] = 0xFF; bitflags[3] = 0xFF; bitflags[4] = 0xFF; bitflags[5] = 0xFF; bitflags[6] = 0xFF; bitflags[7] = 0x0F;
                bitflags[8] = 0xFF; bitflags[9] = 0xFF; bitflags[10] = 0xFF; bitflags[11] = 0xFF; bitflags[12] = 0xFF; bitflags[13] = 0xFF; bitflags[14] = 0xFF; bitflags[15] = 0x07;
                bitflags[16] = 0xFF; bitflags[17] = 0xFF; bitflags[18] = 0xFF; bitflags[19] = 0xFF; bitflags[20] = 0xFF; bitflags[21] = 0xFF; bitflags[22] = 0xFF; bitflags[23] = 0x3F;
                bitflags[24] = 0xFF; bitflags[25] = 0xFF; bitflags[26] = 0xFF; bitflags[27] = 0xFF; bitflags[28] = 0xE7; bitflags[29] = 0x2A; bitflags[30] = 0x00; bitflags[31] = 0x3E;
                bitflags[32] = 0x00; bitflags[33] = 0x00; bitflags[34] = 0x00; bitflags[35] = 0x00; bitflags[36] = 0x00; bitflags[37] = 0x00; bitflags[38] = 0x00; bitflags[39] = 0x00;
                bitflags[40] = 0x00; bitflags[41] = 0x00; bitflags[42] = 0x00; bitflags[43] = 0x00; bitflags[44] = 0x00; bitflags[45] = 0x00; bitflags[46] = 0x00; bitflags[47] = 0x00;
                bitflags[48] = 0x00; bitflags[49] = 0x00; bitflags[50] = 0x00; bitflags[51] = 0x00; bitflags[52] = 0x00; bitflags[53] = 0x00; bitflags[54] = 0x00; bitflags[55] = 0x00;
                bitflags[56] = 0x00; bitflags[57] = 0x00; bitflags[58] = 0x00; bitflags[59] = 0x00; bitflags[60] = 0x00; bitflags[61] = 0x00; bitflags[62] = 0x00; bitflags[63] = 0x00;
                bitflags[64] = 0x00;

                temp[0] = 0xA2;
                temp[1] = (byte)ROULETTE_GEN_3_ID;
                temp[2] = 0x7C;
                temp[3] = 2;
                temp[4] = 0;
                temp[5] = 0;

                temp[6] = bitflags[0];
                temp[7] = bitflags[1];
                temp[8] = bitflags[2];
                temp[9] = bitflags[3];
                temp[10] = bitflags[4];
                temp[11] = bitflags[5];
                temp[12] = bitflags[6];
                temp[13] = bitflags[7];
                temp[14] = 0;
                temp[15] = bitflags[8];
                temp[16] = bitflags[9];
                temp[17] = bitflags[10];
                temp[18] = bitflags[11];
                temp[19] = bitflags[12];
                temp[20] = bitflags[13];
                temp[21] = bitflags[14];
                temp[22] = bitflags[15];
                temp[23] = 0;
                temp[24] = bitflags[16];
                temp[25] = bitflags[17];
                temp[26] = bitflags[18];
                temp[27] = bitflags[19];
                temp[28] = bitflags[20];
                temp[29] = bitflags[21];
                temp[30] = bitflags[22];
                temp[31] = bitflags[23];
                temp[32] = 0;
                temp[33] = bitflags[24];
                temp[34] = bitflags[25];
                temp[35] = bitflags[26];
                temp[36] = bitflags[27];
                temp[37] = bitflags[28];
                temp[38] = bitflags[29];
                temp[39] = bitflags[30];
                temp[40] = bitflags[31];
                temp[41] = 0;
                temp[42] = bitflags[32];
                temp[43] = bitflags[33];
                temp[44] = bitflags[34];
                temp[45] = bitflags[35];
                temp[46] = bitflags[36];
                temp[47] = bitflags[37];
                temp[48] = bitflags[38];
                temp[49] = bitflags[39];

                temp[50] = 0;

                temp[51] = bitflags[40];
                temp[52] = bitflags[41];
                temp[53] = bitflags[42];
                temp[54] = bitflags[43];
                temp[55] = bitflags[44];
                temp[56] = bitflags[45];
                temp[57] = bitflags[46];
                temp[58] = bitflags[47];

                temp[59] = 0;

                temp[60] = bitflags[48];
                temp[61] = bitflags[49];
                temp[62] = bitflags[50];
                temp[63] = bitflags[51];
                temp[64] = bitflags[52];
                temp[65] = bitflags[53];
                temp[66] = bitflags[54];
                temp[67] = bitflags[55];

                temp[68] = 0;

                temp[69] = bitflags[56];
                temp[70] = bitflags[57];
                temp[71] = bitflags[58];
                temp[72] = bitflags[59];
                temp[73] = bitflags[60];
                temp[74] = bitflags[61];
                temp[75] = bitflags[62];
                temp[76] = bitflags[63];

                temp[77] = bitflags[64]; // Reserved Always 0

            }
            catch (Exception ex)
            {
                logger.Error($"Error in Assemble0xA2Data:: {ex.Message}\n{ex.StackTrace}");
            }

            return temp;
        }


        private static void Send(Socket client, String data)
        {
            // Convert the string data to byte data using ASCII encoding.
            Send(client, Encoding.ASCII.GetBytes(data));
        }

        private static void Send(Socket client, byte[] data)
        {
            // Begin sending the data to the remote device.  
            //client.BeginSend(data, 0, data.Length, 0, new AsyncCallback(SendCallback), client);
            try
            {
                if (client.Connected)
                    client.Send(data);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Socket Send Error: {e.Message}");
            }
        }

        private static long EncryptUID(long uid, byte seed)
        {
            byte[] xorbytes = new byte[8];

            try
            {
                for (byte i = 3; i <= xorbytes.Length; i++)
                {
                    xorbytes[xorbytes.Length - i] = (byte)(seed + (i - 3));
                }

                return uid ^ BitConverter.ToInt64(xorbytes, 0);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Encrypt UID Error: {e.Message}");
            }

            return 0L;
        }

        private static int getDenomHex(String denom) // calculate the hex value of the denomination from the passed denom string
        {
            int value = 0;
            int index;

            for (index = 0; index < client.denomArray.Length; index++)
            {
                if (client.denomArray[index] == Convert.ToDouble(denom)) // if the denominations match
                    break;
            }

            // create the hex value that represents what a chip has encoded in it.  For Example, if the passed in denom string was 100, that equates to a hex value of 0x0401 per the official
            // denom table. Base is 1, Magnifier is 4, for a denom of 100.00.  Based on the Denomination Array, 100.00 is at index 14 which yeilds a 0x0400 after the modulo 10 with the left
            // shift of 8 and a 0x0401 after the divide by 10 and addition.
            if (index > 0 && index < client.denomArray.Length) // range check the index
            {
                value = (index % 10) << 8; // shift the denomination magnifier into the correct nibble
                value += index / 10;       // add in the denomination base value
            }

            return value;
        }
    }

    public class ChipObject
    {
        public string ChipDB_SN { get; set; }
        public string ChipDB_Dollars { get; set; }
        public string ChipDB_Player { get; set; }
        public string ChipDB_SN_For_Colored { get; set; }
        public string ChipDB_Player_For_Colored { get; set; }
        public string ChipDB_Dollars_For_Colored { get; set; }
    }

    public class ChipData
    {
        public string ChipValue { get; set; }
        public string ChipType { get; set; }
    }

    public class AntennaTranslation
    {
        public int HGIndex { get; set; }
        public int BravoIndex { get; set; }
    }
}
