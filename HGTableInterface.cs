#define USE_INTEGRATED_READER

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
using System.Text.RegularExpressions;
using System.Net.NetworkInformation;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace HGTranslator
{
    class HGTableInterface
    {
        public static readonly ILog logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static string versionString = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        public static AssemblyName appName;

        //public ILog logger = LogManager.GetLogger("Logger");
        public static HGTableInterface instance;
        private Reader seat1Client;
        private Reader seat2Client;
        private Reader seat3Client;
        private Reader seat4Client;
        private Reader seat5Client;
        private Reader seat6Client;
        private Reader seat7Client;
        private Reader seat8Client;
        private Reader seat9Client;
        private Reader seat10Client;
        private Reader seatGPIOClient;
        private byte[] A2RouletteCommandBytes = new byte[BYTES_IN_ROULETTE_A2_COMMAND];
        private byte[] A2IRCommandBytes = new byte[BYTES_IN_IR_A2_COMMAND];
        public bool exitClicked = false;                                                                    // true if exiting the application
        public string showLEDControlDiags = ConfigurationManager.AppSettings.Get("ShowLEDControlDiags").ToLower();
        private static int num_seats = Convert.ToInt32(ConfigurationManager.AppSettings.Get("TableSeats")); // number of configured seats
        private const int MAX_ANTENNA_SPOTS = 9;
        private const int A2_COMMAND_TIMEOUT_VALUE = 10;                                                    // timeout value for the A2 command event in milliseconds
        private const int A2_RESPONSE_TIMEOUT_VALUE = 30000;                                                // timeout value for the Response to the A2 command in milliseconds
        private const int BYTES_IN_IR_A2_COMMAND = 16;
        private const int BYTES_IN_ROULETTE_A2_COMMAND = 78;
        private const int ROULETTE_RECORD_LEN = 16;                                                                  // length of a chip record 
        private const int IR_RECORD_LEN = 15;                                                                  // length of a chip record 
        private const int SN_LEN = 12;                                                                      // length of a SN record including the 6 leading 0s
        private const int ROULETTE_HEADER_SIZE = 75;                                                                 // Roulette 4 comm spec 7 A2 header size 
        private const int IR_HEADER_SIZE = 51;                                                                 // Roulette 4 comm spec 7 A2 header size 
        private String UtilityPort = "55012";
        private String DebugPort = "55013";
        private String LEDPort = "55014";
        private const int ALL_OFF = 0xff;
        //private StringBuilder A2Response = new();
        //private HashSet<ChipObject> chips = new HashSet<ChipObject>();                                      // used to hold chip information keyed by UID

#if USE_DENOM_DATA
        private Dictionary<string, ChipData> chipsDict = new Dictionary<string, ChipData>(); // structure as follows: { uid, { chipvalue, chiptype }}
#endif

        private ConcurrentDictionary<string, Tuple<string, int>> chipLocationPrev = new ConcurrentDictionary<string, Tuple<string, int>>();     // structure as follows: { uid, axis }
        private ConcurrentDictionary<string, Tuple<string, int>> chipLocationNow = new ConcurrentDictionary<string, Tuple<string, int>>();      // structure as follows: { uid, axis }

        private Thread InitSeat1Thread;
        private Thread InitSeat2Thread;
        private Thread InitSeat3Thread;
        private Thread InitSeat4Thread;
        private Thread InitSeat5Thread;
        private Thread InitSeat6Thread;
        private Thread InitSeat7Thread;
        private Thread InitSeat8Thread;
        private Thread InitSeat9Thread;
        private Thread InitSeat10Thread;
        private Thread InitSeatGPIOThread;

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
        public List<decimal> seat10AntennaList;
        public List<decimal> floatAntennaList;
        public List<decimal> exchangeAntennaList;
        public List<decimal> axisToIgnoreList;
        public List<int> groupPriorityList;
        public bool[] pollGroupArr = new bool[1];
        public Dictionary<int, int> groupNumberDict;                    // group number, priority
        public Dictionary<Reader, int> connectedReadersDict = new();    // client, seat
        public bool FlushingTags = false;

        private readonly string validIPPattern = "^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";
        private readonly string[] seatIPs = new string[]
        {
            "", // placeholder to allow for 1 based indexing
            ConfigurationManager.AppSettings.Get("seat1IPAddress"),
            ConfigurationManager.AppSettings.Get("seat2IPAddress"),
            ConfigurationManager.AppSettings.Get("seat3IPAddress"),
            ConfigurationManager.AppSettings.Get("seat4IPAddress"),
            ConfigurationManager.AppSettings.Get("seat5IPAddress"),
            ConfigurationManager.AppSettings.Get("seat6IPAddress"),
            ConfigurationManager.AppSettings.Get("seat7IPAddress"),
            ConfigurationManager.AppSettings.Get("seat8IPAddress"),
            ConfigurationManager.AppSettings.Get("seat9IPAddress"),
            ConfigurationManager.AppSettings.Get("seat10IPAddress"),
            ConfigurationManager.AppSettings.Get("seatGPIOIPAddress")
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

#if USE_INTEGRATED_READER
        private readonly Dictionary<int, decimal>[] validAntennasDict = new[] // Array of dictionaries indexed by seat number.  Dictionary is Key = Holy Grail antenna index, Value is MARS equivalent index
        {
            // Blank for 1 based indexing
            new Dictionary<int, decimal>()
            {
            },

            // Seat 1
            new Dictionary<int, decimal>()
            {
                { 27, 1.0M },
                { 28, 1.1M },
                { 1, 1.2M },
                { 2, 1.3M },
                { 3, 1.4M },
                { 4, 1.5M },
                { 5, 1.6M },
                { 6, 1.7M },
                { 7, 1.8M }
            },

            // Seat 2
            new Dictionary<int, decimal>()
            {
                { 27, 2.0M },
                { 28, 2.1M },
                { 1, 2.2M },
                { 2, 2.3M },
                { 3, 2.4M },
                { 4, 2.5M },
                { 5, 2.6M },
                { 6, 2.7M },
                { 7, 2.8M }
            },

            // Seat 3
            new Dictionary<int, decimal>()
            {
                { 27, 3.0M },
                { 28, 3.1M },
                { 1, 3.2M },
                { 2, 3.3M },
                { 3, 3.4M },
                { 4, 3.5M },
                { 5, 3.6M },
                { 6, 3.7M },
                { 7, 3.8M }
            },

            // Seat 4
            new Dictionary<int, decimal>()
            {
                { 27, 4.0M },
                { 28, 4.1M },
                { 1, 4.2M },
                { 2, 4.3M },
                { 3, 4.4M },
                { 4, 4.5M },
                { 5, 4.6M },
                { 6, 4.7M },
                { 7, 4.8M }
            },

            // Seat 5
            new Dictionary<int, decimal>()
            {
                { 27, 5.0M },
                { 28, 5.1M },
                { 1, 5.2M },
                { 2, 5.3M },
                { 3, 5.4M },
                { 4, 5.5M },
                { 5, 5.6M },
                { 6, 5.7M },
                { 7, 5.8M }
            },

            // Seat 6
            new Dictionary<int, decimal>()
            {
                { 27, 6.0M },
                { 28, 6.1M },
                { 1, 6.2M },
                { 2, 6.3M },
                { 3, 6.4M },
                { 4, 6.5M },
                { 5, 6.6M },
                { 6, 6.7M },
                { 7, 6.8M }
            },

            // Seat 7
            new Dictionary<int, decimal>()
            {
                { 27, 7.0M },
                { 28, 7.1M },
                { 1, 7.2M },
                { 2, 7.3M },
                { 3, 7.4M },
                { 4, 7.5M },
                { 5, 7.6M },
                { 6, 7.7M },
                { 7, 7.8M }
            },

            // Seat 8
            new Dictionary<int, decimal>()
            {
                { 27, 8.0M },
                { 28, 8.1M },
                { 1, 8.2M },
                { 2, 8.3M },
                { 3, 8.4M },
                { 4, 8.5M },
                { 5, 8.6M },
                { 6, 8.7M },
                { 7, 8.8M }
            },

            // Seat 9
            new Dictionary<int, decimal>()
            {
                { 27, 9.0M },
                { 28, 9.1M },
                { 1, 9.2M },
                { 2, 9.3M },
                { 3, 9.4M },
                { 4, 9.5M },
                { 5, 9.6M },
                { 6, 9.7M },
                { 7, 9.8M }
            },

            // Seat 10
            new Dictionary<int, decimal>()
            {
                { 27, 10.0M },
                { 28, 10.1M },
                { 1, 10.2M },
                { 2, 10.3M },
                { 3, 10.4M },
                { 4, 10.5M },
                { 5, 10.6M },
                { 6, 10.7M },
                { 7, 10.8M }
            },

            // Seat GPIO
            new Dictionary<int, decimal>()
            {
                { 1, 20.0M },
                { 2, 20.1M },
                { 3, 20.2M },
                { 4, 20.3M },
                { 5, 20.4M },
                { 6, 20.5M },
                { 7, 20.6M },
                { 27, 22.7M },
                { 28, 22.8M }
            },
        };
#else
        // Holy Grail Roulette antenna index, MARS translated antenna index
        private readonly Dictionary<int, decimal>[] validAntennasDict = new[]
        {
            // Blank for 1 based indexing
            new Dictionary<int, decimal>()
            {
            },

            // Seat 1
            new Dictionary<int, decimal>()
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
            },

            // Seat 2
            new Dictionary<int, decimal>()
            {
                { 137, 4.0M },
                { 139, 4.1M },
                { 141, 4.2M },
                { 151, 4.3M },
                { 153, 4.4M },
                { 155, 4.5M }
            },
        };
#endif

        public enum SEAT
        {
            ONE = 1,
            TWO,
            THREE,
            FOUR,
            FIVE,
            SIX,
            SEVEN,
            EIGHT,
            NINE,
            TEN,
            GPIO,
            MAX_SEATS
        }

        public int[] LEDRings = new int[] { 0x1f, 0x20, 0x18, 0x19, 0x1a, 0x1b, 0x1c, 0x1d, 0x1e};

        public enum LED_RINGS
        {
            ONE = 1,
            TWO,
            THREE,
            FOUR,
            FIVE,
            SIX,
            SEVEN,
            EIGHT,
            NINE,
            TEN,
            ELEVEN,
            TWELVE,
        }

        public enum LED_COLORS
        {
            OFF = 0x40,
            DIM_WHITE_SOLID = 0x41,
            MEDIUM_WHITE_SOLID = 0x42,
            FULL_WHITE_SOLID = 0x43,
            RED_SOLID = 0x44,
            YELLOW_SOLID = 0x45,
            GREEN_SOLID = 0x46,
            CYAN_SOLID = 0x47,
            BLUE_SOLID = 0x48,
            MAGENTA_SOLID = 0x49,
            MAX_LED_COLORS = 0x4a
        }

#if USE_DENOM_DATA
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
#endif

        private bool exitSystem = false;

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

            HGTableInterface.instance.exitClicked = true;

            // Release the sockets and shut down the worker threads.
            try
            {
                GGHost.ShutDown();

                foreach (var client in HGTableInterface.instance.connectedReadersDict)
                {
                    instance.SetLEDColor(client.Key, ALL_OFF, Convert.ToInt32(LED_COLORS.OFF));

                    if (client.Key != null)
                    {
                        if (client.Key.Socket != null)
                        {
                            HGTableInterface.instance.Command_0xA2(client.Key.Socket);
                            client.Key.WorkerFlag = false;
                            client.Key.A2CommandTimer.Stop();
                            client.Key.Socket.Shutdown(SocketShutdown.Both);
                            client.Key.Socket.Close();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"ExitHandler Error: {e.Message}\n{e.StackTrace}");
                logger.Error($"ExitHandler Error: {e.Message}\n{e.StackTrace}");
            }

            Thread.Sleep(3000);

            //allow main to run off
            HGTableInterface.instance.exitSystem = true;
            return true;
        }
#endregion

        public HGTableInterface()
        {
        }

        public static int Main(String[] args)
        {
            try
            {
                log4net.Config.XmlConfigurator.Configure();
                instance = new HGTableInterface();
                Assembly execAssembly = Assembly.GetCallingAssembly();
                appName = execAssembly.GetName();
                Console.WriteLine($"{appName.Name} Version {versionString} Starting Up....");
                _exithandler += new EventHandler(ExitHandler);
                SetConsoleCtrlHandler(_exithandler, true);
                FTDI2XX.Init4232();
                //HGTableInterface.instance.ImportChipDB();
                HGTableInterface.instance.A2RouletteCommandBytes = HGTableInterface.instance.Assemble0xA2DataRoulette();
                HGTableInterface.instance.A2IRCommandBytes = HGTableInterface.instance.Assemble0xA2DataIR();

                new Thread(GGHost.Start).Start();
                HGTableInterface.instance.InitWorkers();

                //long uid = 0x4803ad146c4d;
                //var xord = uid ^ 0x0d0e0f101112;
                //var unxord = xord ^ 0x0d0e0f101112;
                //var xord1 = EncryptUID(uid, 0x0d);
                //var xord2 = EncryptUID(xord1, 0x0d);

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
#endif

                while (!HGTableInterface.instance.exitSystem)
                    Thread.Sleep(1000);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Main Error: {e.Message}");
            }

            Environment.Exit(0);
            return 0;
        }

        private void A2CommandResponseHandler(Object source, ElapsedEventArgs args, Reader client)
        {
            byte[] bytes = new byte[131072];
            byte[] bigbuffer = new byte[131072];
            int bytesRcvd;
            int expectedByteCount = 0;
            int totalBytesRcvd = 0;
            int bufferpointer;

            if (FlushingTags) // Don't process chip messages while flushing tags
                return;

            client.A2CommandTimer.Enabled = false; // disable the timer while processing this message

            // Receive the response from the remote device.  
            try
            {
                bytesRcvd = client.Socket.Receive(bytes);
                //Console.WriteLine($"A2 Timer Event Fired, bytes Received from Seat {client.Seat} = {bytesRcvd}");

                if (HGTableInterface.instance.exitClicked)
                    return;

                if (bytesRcvd > 0)
                {
                    client.A2ResponseTimer.Stop(); // stop the response timer while we process this response
                    //logger.Info($"Recieved {bytesRcvd} byte(s) from Table for Client {client.Socket.RemoteEndPoint}, Response = 0x{string.Format("{0:X2}", bytes[0])}");
                    //Console.WriteLine($"Recieved {bytesRcvd} byte(s) from Table for Client {client.Socket.RemoteEndPoint}, Response = 0x{string.Format("{0:X2}", bytes[0])}");

                    if (bytes[0] == 0xA2) // A2 response
                    {
                        expectedByteCount = (bytes[1] * 256) + bytes[2]; // get the number of bytes we are expecting in this response
                        client.numChipsSeen = (bytes[3] * 256) + bytes[4];
                        //logger.Info($"Expecting to Recieve {expectedByteCount} bytes");
                        //Console.WriteLine($"Expecting to Recieve for Client {client.Socket.RemoteEndPoint}, {expectedByteCount} bytes");
                        //Console.WriteLine($"num chips seen for Client {client.Seat} = {client.numChipsSeen}");

                        if (expectedByteCount == bytesRcvd) // if we have the number of bytes we are expecting
                        {
                            Array.Resize(ref bytes, bytesRcvd);
                            Array.Copy(bytes, bigbuffer, bytesRcvd);
                        }
                        else
                        {
                            //logger.Info($"Recieving Extra Bytes, Still Need {expectedByteCount - bytesRcvd} bytes");
                            //Console.WriteLine($"Receiving Extra Bytes, Still Need {expectedByteCount - bytesRcvd} bytes");

                            Array.Copy(bytes, bigbuffer, bytesRcvd);
                            bufferpointer = bytesRcvd;
                            totalBytesRcvd = bytesRcvd; // track the total bytes received

                            do // receive the rest of the expected bytes
                            {
                                Array.Clear(bytes, 0, bytes.Length); // setup for more bytes to be received
                                bytesRcvd = client.Socket.Receive(bytes); // receive additional bytes
                                totalBytesRcvd += bytesRcvd; // update the total bytes received
                                Array.Copy(bytes, 0, bigbuffer, bufferpointer, bytesRcvd);
                                bufferpointer += bytesRcvd;
                                //Console.WriteLine($"Received {bytesRcvd} more bytes");
                                //Console.WriteLine($"totalbytesRcvd = {totalBytesRcvd}");
                            } while (totalBytesRcvd < expectedByteCount);
                        }

                        //var msg = "";
                        //for (int i = 0; i <expectedByteCount; i++)
                        //{
                        //    msg += bigbuffer[i].ToString("X2") + " ";
                        //}

                        //logger.Debug($"reader message = {msg}");

                        client.Response = BuildChipMessage(client, bigbuffer); // build a response message
                    }
                }

                Command_0xA2(client.Socket); // send another A2 command
                client.A2ResponseTimer.Start(); // restart the response timer
            }
            catch (Exception e)
            {
                Console.WriteLine($"A2 Timer Event Handler Error: Client {client.Socket.RemoteEndPoint}\n{e.Message}\n{e.StackTrace}");
                logger.Debug($"A2 Timer Event Handler Error: Client {client.Socket.RemoteEndPoint}\n{e.Message}\n{e.StackTrace}");
                return;
            }

            client.A2CommandTimer.Enabled = true; // enable the timer
        }

        /// <summary>
        /// Build a MARS formatted chip message from the A2 response
        /// </summary>
        /// <param ></param>
        /// <returns>replyStr</returns>
        private string BuildChipMessage(Reader client, byte[] bytes)
        {
            bool added = false;
            bool removed = false;

#if USE_INTEGRATED_READER
            var recpointer = IR_HEADER_SIZE;
#else
            var recpointer = ROULETTE_HEADER_SIZE;
#endif

            int groupNum = -1;
            string replystr = string.Empty;
            string chipUID; // UID of the current chip being processed
            Dictionary<string, Tuple<string, int>> chipLocationCurr = new Dictionary<string, Tuple<string, int>>(); // structure as follows: { uid, axis }

            //logger.Debug($"bytes.Length = {bytes.Length}");

            try
            {
                if (client.numChipsSeen > 0 || HGTableInterface.instance.chipLocationPrev.Count > 0) // if there are chips on an antenna
                {
                    if (HGTableInterface.instance.axisToIgnoreList == null)
                    {
                        Console.WriteLine("BuildChipMessage: Axis Groups Have Not Been Defined!  Send MARS Init to Controller.");
                        logger.Debug("BuildChipMessage: Axis Groups Have Not Been Defined!  Send MARS Init to Controller.");
                        return string.Empty;
                    }

                    if (HGTableInterface.instance.groupPriorityList == null)
                    {
                        Console.WriteLine("BuildChipMessage: Group Priority List Has Not Been Defined!  Send MARS Init to Controller.");
                        logger.Debug("BuildChipMessage: Group Priority List Has Not Been Defined!  Send MARS Init to Controller.");
                        return string.Empty;
                    }

                    for (int i = 0; i < client.numChipsSeen; i++)
                    {
                        var antIndex = 0;
                        var MARSAntIndex = 99M;
                        chipUID = Convert.ToHexString(bytes, recpointer + (SN_LEN - 6), SN_LEN - 6);
                        //Console.WriteLine($"New ChipUID = {chipUID}");
                        //chipUID = Convert.ToHexString(uidbytes); // save the UID pulled from the response
                        //Console.WriteLine($"ChipUID = {chipUID}");

#if USE_INTEGRATED_READER
                        antIndex = bytes[recpointer + SN_LEN]; // single byte for the antenna index
                        recpointer += IR_RECORD_LEN; // next chip record
                        //logger.Debug($"Antenna Index = {antIndex}");
#else
                        antIndex = (bytes[recpointer + SN_LEN] * 256) + bytes[recpointer + SN_LEN + 1]; // two bytes for the antenna index
                        recpointer += ROULETTE_RECORD_LEN; // next chip record
#endif

                        // Check to see which seat the antenna index is on and if it should be ignored or if it's group is disabled.
                        if (!HGTableInterface.instance.validAntennasDict[client.Seat].ContainsKey(antIndex))
                            continue;

                        MARSAntIndex = HGTableInterface.instance.validAntennasDict[client.Seat][antIndex];
                        //logger.Debug($"MARS Antenna Index = {MARSAntIndex}");

                        if (HGTableInterface.instance.axisToIgnoreList.Contains(MARSAntIndex)) // check the axis ignore list first
                            continue;

                        var group = Math.Floor(MARSAntIndex); // get the axis group 0 - 9
                        groupNum = HGTableInterface.instance.groupNumberDict[(int)group]; // get the group number based on active groups in the Axis List

                        if (HGTableInterface.instance.groupPriorityList.ElementAt(groupNum) == 0)
                        {
                            if (HGTableInterface.instance.pollGroupArr[groupNum] == true) // Send chip sighted messages here for manually polled groups
                            {
                                //replystr += $"Info,Reply:Axis {MARSAntIndex}, " +
                                //            $"Timestamp 7fff, SpecificID {chipUID}\r\n";
                                GGHost.Send($"Info,Reply:Axis {MARSAntIndex}, Timestamp 7fff, SpecificID {chipUID}");
                                Console.WriteLine($"Info,Reply:Axis {MARSAntIndex}, Timestamp 7fff, SpecificID {chipUID}");
                                //replystr += $"Info,Reply:Axis {MARSAntIndex}, " +
                                //            $"Timestamp 7fff, SpecificID {Convert.ToHexString(uidbytes)}, " +
                                //            $"ReadAddress 000a, Data: 0000 {getDenomHex(HGTableInterface.instance.chipsDict[chipUID].ChipValue):X4}\r\n";
                            }

                            continue;
                        }

                        groupNum = HGTableInterface.instance.groupNumberDict[(int)group]; // get the group number based on active groups in the Axis List
                        //Console.WriteLine($"Processing Chip for Group {groupNum}");
                        //Console.WriteLine($"UID of Chip = {chipUID}");
                        //Console.WriteLine($"Chip on Spot = {MARSAntIndex}");

                        if (!HGTableInterface.instance.chipLocationPrev.ContainsKey(chipUID)) // if this chip wasn't previously on the table
                        {
                            //Console.WriteLine($"Location Now = {MARSAntIndex}");
                            added = HGTableInterface.instance.chipLocationNow.TryAdd(chipUID, Tuple.Create(MARSAntIndex.ToString(), client.Seat)); // add this chip to the current location dict keyed by uid

                            if (!added)
                            {
                                Console.WriteLine($"BuildChipMessage: Unable to Add UID {chipUID} to Location Previous Dictionary.");
                                logger.Debug($"BuildChipMessage: Unable to Add UID {chipUID} to Location Previous Dictionary.");
                            }

                            chipLocationCurr.Add(chipUID, Tuple.Create(MARSAntIndex.ToString(), client.Seat)); // add this chip to the current location dict keyed by uid
                            GGHost.Send($"Info,Reply:Axis {MARSAntIndex}, Timestamp 7fff, SpecificID {chipUID}");
                            Console.WriteLine($"Info,Reply:Axis {MARSAntIndex}, Timestamp 7fff, SpecificID {chipUID}");
                            var antenna = (int)((MARSAntIndex - Math.Truncate(MARSAntIndex)) * 10);
                            //Console.WriteLine($"BuildChipMessage 1: Set Spot {antenna} LEDs to Covered Color of {Convert.ToInt32(client.ledControl[antenna].SpotCovered):X2}");
                            SetLEDColor(client, MARSAntIndex, client.ledControl[antenna].SpotCovered);
                        }
                        else
                        {
                            //Console.WriteLine($"Current Loc = {MARSAntIndex}");
                            //Console.WriteLine($"Prev Loc = {HGTableInterface.instance.chipLocationPrev[chipUID].Item1}");
                            var prevLoc = HGTableInterface.instance.chipLocationPrev[chipUID].Item1;

                            if (MARSAntIndex.ToString() != prevLoc) // if the current location is different than the previous location
                            {
                                //Console.WriteLine($"Current Loc different than Previous Loc");
                                GGHost.Send($"Info,Tag:Expired {chipUID}"); // send an expiration message for the previous sighting of this chip
                                Console.WriteLine($"Info,Tag:Expired {chipUID}");
                                var antenna = (int)((decimal.Parse(prevLoc) - Math.Truncate(decimal.Parse(prevLoc))) * 10);
                                //Console.WriteLine($"BuildChipMessage 2: Set Spot {antenna} LEDs to Uncovered Color of {Convert.ToInt32(client.ledControl[antenna].SpotUncovered):X2}");
                                SetLEDColor(client, decimal.Parse(prevLoc), client.ledControl[antenna].SpotUncovered);

                                removed = HGTableInterface.instance.chipLocationNow.TryRemove(chipUID, out Tuple<string, int> temp);

                                if (!removed)
                                {
                                    Console.WriteLine($"BuildChipMessage: Unable to Remove UID {chipUID} from the Location Previous Dictionary.");
                                    logger.Debug($"BuildChipMessage: Unable to Remove UID {chipUID} from the Location Previous Dictionary.");
                                }

                                GGHost.Send($"Info,Reply:Axis {MARSAntIndex}, Timestamp 7fff, SpecificID {chipUID}");
                                Console.WriteLine($"Info,Reply:Axis {MARSAntIndex}, Timestamp 7fff, SpecificID {chipUID}");
                                antenna = (int)((MARSAntIndex - Math.Truncate(MARSAntIndex)) * 10);
                                //Console.WriteLine($"BuildChipMessage 3: Set Spot {antenna} LEDs to Covered Color of {Convert.ToInt32(client.ledControl[antenna].SpotCovered):X2}");
                                SetLEDColor(client, MARSAntIndex, client.ledControl[antenna].SpotCovered);
                            }

                            added = HGTableInterface.instance.chipLocationNow.TryAdd(chipUID, Tuple.Create(MARSAntIndex.ToString(), client.Seat)); // add to current location list
                            chipLocationCurr.Add(chipUID, Tuple.Create(MARSAntIndex.ToString(), client.Seat));
                        }
                    }

                    if (client.numChipsSeen > 0)
                    {
                        // check to see if any chips that were present previously have been removed from the table
                        foreach (var chipprev in HGTableInterface.instance.chipLocationPrev)
                        {
                            if (client.Seat == chipprev.Value.Item2)
                            {
                                if (!chipLocationCurr.ContainsKey(chipprev.Key))
                                {
                                    //Console.WriteLine($"chipprev not equal to chiplocation now, chipprev loc = {chipprev.Value.Item1}");
                                    GGHost.Send($"Info,Tag:Expired {chipprev.Key}"); // send an expiration message for the previous sighting of this chip
                                    Console.WriteLine($"Info,Tag:Expired {chipprev.Key}"); // send an expiration message upstream
                                    Thread.Sleep(5); // give some time for the last LED command to settle
                                    removed = HGTableInterface.instance.chipLocationNow.TryRemove(chipprev.Key, out Tuple<string, int> temp);

                                    if (!removed)
                                    {
                                        Console.WriteLine($"BuildChipMessage: Unable to Remove UID {chipprev.Key} from the Location Now Dictionary.");
                                        logger.Debug($"BuildChipMessage: Unable to Remove UID {chipprev.Key} from the Location Now Dictionary.");
                                    }
                                    else
                                    {
                                        bool chipLocFound = false;

                                        foreach (var chipnow in HGTableInterface.instance.chipLocationNow)
                                        {
                                            //Console.WriteLine($"chipnow loc = {chipnow.Value.Item1}, chipprev loc = {chipprev.Value.Item1}");

                                            if (chipnow.Value.Item1 == chipprev.Value.Item1)
                                            {
                                                chipLocFound = true;
                                                break;
                                            }
                                        }

                                        if (!chipLocFound)
                                        {
                                            var antenna = (int)((decimal.Parse(chipprev.Value.Item1) - Math.Truncate(decimal.Parse(chipprev.Value.Item1))) * 10);
                                            //Console.WriteLine($"BuildChipMessage 4: Set Spot {antenna} LEDs to Uncovered Color of {Convert.ToInt32(client.ledControl[antenna].SpotUncovered):X2}");
                                            SetLEDColor(client, decimal.Parse(chipprev.Value.Item1), client.ledControl[antenna].SpotUncovered);
                                        }
                                    }
                                }
                            }
                        }

                        //UpdateSeatLEDs(client, chipLocationNow);
                        HGTableInterface.instance.chipLocationPrev = new ConcurrentDictionary<string, Tuple<string, int>>(HGTableInterface.instance.chipLocationNow); // save current view of the chips on the table
                    }
                    else
                    {
                        if (HGTableInterface.instance.chipLocationPrev.Count > 0) // if there were chips on the table previously
                        {
                            //Console.WriteLine("0 Chips Seen, Sending Expirations for Each Chip Removed");

                            bool chipsExpired = false;
                            Dictionary<string, Tuple<string, int>> tmpdict = new(HGTableInterface.instance.chipLocationPrev);

                            foreach (var chip in tmpdict)
                            {
                                if (client.Seat == chip.Value.Item2)
                                {
                                    GGHost.Send($"Info,Tag:Expired {chip.Key}"); // send an expiration message for the previous sighting of this chip
                                    Console.WriteLine($"Info,Tag:Expired {chip.Key}"); // no chips on the table now, so send an expiration for each chip that was there
                                    removed = HGTableInterface.instance.chipLocationPrev.TryRemove(chip.Key, out Tuple<string, int> temp);

                                    if (!removed)
                                    {
                                        Console.WriteLine($"BuildChipMessage: Unable to Remove UID {chip.Key} from the Location Previous Dictionary.");
                                        logger.Debug($"BuildChipMessage: Unable to Remove UID {chip.Key} from the Location Previous Dictionary.");
                                    }

                                    removed = HGTableInterface.instance.chipLocationNow.TryRemove(chip.Key, out temp);

                                    if (!removed)
                                    {
                                        Console.WriteLine($"BuildChipMessage: Unable to Remove UID {chip.Key} from the Location Now Dictionary.");
                                        logger.Debug($"BuildChipMessage: Unable to Remove UID {chip.Key} from the Location Now Dictionary.");
                                    }

                                    chipsExpired = true;
                                }
                            }

                            if (chipsExpired)
                            {
                                SetLEDColor(client, ALL_OFF, Convert.ToInt32(LED_COLORS.OFF));
                            }
                        }
                    }

                    //UpdateSeatLEDs(client, chipLocationNow);
                }

                if (groupNum > -1)
                {
                    if (HGTableInterface.instance.pollGroupArr[groupNum] == true)
                        HGTableInterface.instance.pollGroupArr[groupNum] = false;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"BuildChipSightedMessage Error: {e.Message}\n{e.StackTrace}");
            }

            return replystr;
        }

        internal void FlushTags(string axis)
        {
            bool removed = false;

            if (!axis.Equals(string.Empty)) // if flushing a specific axis
            {
                ConcurrentDictionary<string, Tuple<string, int>> tempDict = new(HGTableInterface.instance.chipLocationPrev); // get a working copy of the previous location dictionary

                foreach (var chip in tempDict)
                {
                    //if (axis.Equals(HGTableInterface.instance.validAntennasDict[chip.Value.Item2][Convert.ToInt32(chip.Value.Item1)].ToString()))
                    if (axis.Equals(chip.Value.Item1))
                    {
                        Console.WriteLine($"Info,Tag:Expired {chip.Key}"); // send an expiration message for the previous sighting of this chip
                        GGHost.Send($"Info,Tag:Expired {chip.Key}"); // send an expiration message for the previous sighting of this chip
                        removed = HGTableInterface.instance.chipLocationPrev.TryRemove(chip.Key, out Tuple<string, int> temp); // remove the chip from the previous sighting dictionary so it will be re-sighted

                        if (!removed)
                        {
                            Console.WriteLine($"BuildChipMessage: Unable to Remove UID {chip.Key} from the Location Previous Dictionary.");
                            logger.Debug($"BuildChipMessage: Unable to Remove UID {chip.Key} from the Location Previous Dictionary.");
                        }

                        removed = HGTableInterface.instance.chipLocationNow.TryRemove(chip.Key, out temp); // remove the chip from the sighting dictionary so it will be re-sighted

                        if (!removed)
                        {
                            Console.WriteLine($"BuildChipMessage: Unable to Remove UID {chip.Key} from the Location Now Dictionary.");
                            logger.Debug($"BuildChipMessage: Unable to Remove UID {chip.Key} from the Location Now Dictionary.");
                        }
                    }
                }
            }
            else // flushing all axis with a non-zero priority, the GroupPoll command should be used to get a fresh accounting of axis with a priority of 0
            {
                foreach (var chip in HGTableInterface.instance.chipLocationPrev)
                {
                    Console.WriteLine($"Info,Tag:Expired {chip.Key}"); // send an expiration message for the previous sighting of this chip
                    GGHost.Send($"Info,Tag:Expired {chip.Key}"); // send an expiration message for the previous sighting of this chip
                }

                HGTableInterface.instance.chipLocationPrev.Clear(); // clear the dictionary so all chips will be re-sighted
                HGTableInterface.instance.chipLocationNow.Clear();
            }

            HGTableInterface.instance.FlushingTags = false;
        }

        private void UpdateSeatLEDs(Reader client, ConcurrentDictionary<string, Tuple<string, int>> chipLocation)
        {
            List<string> occupiedAntennasList = new List<string>();
            int spot;

            if (!chipLocation.Any())
            {
                SetLEDColor(client, ALL_OFF, Convert.ToInt32(LED_COLORS.OFF));
            }
            else
            {
                foreach (var chip in chipLocation)
                {
                    if (client.Seat == chip.Value.Item2)
                    {
                        var antenna = chip.Value.Item1;
                        Console.WriteLine($"UpdateSeatLEDs: Seat {chip.Value.Item1} Chip Found on Antenna {antenna}");

                        if (!occupiedAntennasList.Contains(antenna))
                            occupiedAntennasList.Add(antenna);
                    }
                }

                Console.WriteLine($"UpdateSeatLEDs: occupiedAntennaList.Count = {occupiedAntennasList.Count}");

                foreach (var antenna in HGTableInterface.instance.validAntennasDict[client.Seat])
                {
                    spot = (int)((antenna.Value - Math.Truncate(antenna.Value)) * 10);

                    if (!occupiedAntennasList.Contains(antenna.Value.ToString()))
                    {
                        Console.WriteLine($"UpdateSeatLEDs: Set Spot {spot} LEDs to Uncovered Color of {Convert.ToInt32(client.ledControl[spot].SpotUncovered):X2}");
                        SetLEDColor(client, antenna.Value, client.ledControl[spot].SpotUncovered);
                    }
                    else
                    {
                        Console.WriteLine($"UpdateSeatLEDs: Set Spot {spot} LEDs to Covered Color of {Convert.ToInt32(client.ledControl[spot].SpotCovered):X2}");
                        SetLEDColor(client, antenna.Value, client.ledControl[spot].SpotCovered);
                    }

                    Thread.Sleep(100);
                }
            }
        }

        private void UpdateSeatLEDs(Reader client, Dictionary<string, Tuple<string, int>> chipLocation)
        {
            List<string> occupiedAntennasList = new List<string>();

            foreach (var chip in chipLocation)
            {
                if (client.Seat == chip.Value.Item2)
                {
                    var antenna = chip.Value.Item1;

                    if (!occupiedAntennasList.Contains(antenna))
                        occupiedAntennasList.Add(antenna);
                }
            }

            foreach (var antenna in HGTableInterface.instance.validAntennasDict[client.Seat])
            {
                if (!occupiedAntennasList.Contains(antenna.Value.ToString()))
                {
                    var spot = (int)((antenna.Value - Math.Truncate(antenna.Value)) * 10);
                    Console.WriteLine($"UpdateSeatLEDs: Set Spot {spot} LEDs to Uncovered Color of {Convert.ToInt32(client.ledControl[spot].SpotUncovered):X2}");
                    SetLEDColor(client, antenna.Value, client.ledControl[spot].SpotUncovered);
                    Thread.Sleep(100);
                }
            }
        }

        private void SetLEDColor(Reader client, decimal antindex, int effect)
        {
            if (antindex == 0xff)
                HGTableInterface.instance.Send(client.LEDSocket, @"$ff18404040404040404040" + instance.GetLEDCommandCheckSum("ff18404040404040404040") + "~");
            else
            {
                int ring = LEDRings[Convert.ToInt32(antindex.ToString().Split('.')[1])];
                var ledStr = "ff" + ring.ToString("x2") + effect.ToString("x2");
                HGTableInterface.instance.Send(client.LEDSocket, "$" + ledStr + instance.GetLEDCommandCheckSum(ledStr) + "~");
            }
        }

        private void InitWorkers()
        {
            if (Regex.IsMatch(seatIPs[(int)SEAT.ONE], validIPPattern))
            {
                HGTableInterface.instance.seat1Client = new Reader
                {
                    Seat = (int)SEAT.ONE,
                    Response = string.Empty,
                    ledControl = new LEDControl[MAX_ANTENNA_SPOTS],
                    WorkerFlag = false
                };

                for (int i = 0; i < MAX_ANTENNA_SPOTS; i++)
                {
                    HGTableInterface.instance.seat1Client.ledControl[i] = new LEDControl();
                    HGTableInterface.instance.seat1Client.ledControl[i].enabled = true;
                    HGTableInterface.instance.seat1Client.ledControl[i].SpotCovered = (int)LED_COLORS.DIM_WHITE_SOLID + i;
                    HGTableInterface.instance.seat1Client.ledControl[i].SpotUncovered = (int)LED_COLORS.OFF;
                }

                InitSeat1Thread = new Thread(() => ConnectToSeatThread(HGTableInterface.instance.seat1Client));
                InitSeat1Thread.Start();
            }

            Thread.Sleep(100);

            if (Regex.IsMatch(seatIPs[(int)SEAT.TWO], validIPPattern))
            {
                HGTableInterface.instance.seat2Client = new Reader
                {
                    Seat = (int)SEAT.TWO,
                    Response = string.Empty,
                    ledControl = new LEDControl[MAX_ANTENNA_SPOTS],
                    WorkerFlag = false
                };

                for (int i = 0; i < MAX_ANTENNA_SPOTS; i++)
                {
                    HGTableInterface.instance.seat2Client.ledControl[i] = new LEDControl();
                    HGTableInterface.instance.seat2Client.ledControl[i].enabled = true;
                    HGTableInterface.instance.seat2Client.ledControl[i].SpotCovered = (int)LED_COLORS.DIM_WHITE_SOLID + i;
                    HGTableInterface.instance.seat2Client.ledControl[i].SpotUncovered = (int)LED_COLORS.OFF;
                }

                InitSeat2Thread = new Thread(() => ConnectToSeatThread(HGTableInterface.instance.seat2Client));
                InitSeat2Thread.Start();
            }

            Thread.Sleep(100);

            if (Regex.IsMatch(seatIPs[(int)SEAT.THREE], validIPPattern))
            {
                HGTableInterface.instance.seat3Client = new Reader
                {
                    Seat = (int)SEAT.THREE,
                    Response = string.Empty,
                    ledControl = new LEDControl[MAX_ANTENNA_SPOTS],
                    WorkerFlag = false
                };

                for (int i = 0; i < MAX_ANTENNA_SPOTS; i++)
                {
                    HGTableInterface.instance.seat3Client.ledControl[i] = new LEDControl();
                    HGTableInterface.instance.seat3Client.ledControl[i].enabled = true;
                    HGTableInterface.instance.seat3Client.ledControl[i].SpotCovered = (int)LED_COLORS.DIM_WHITE_SOLID + i;
                    HGTableInterface.instance.seat3Client.ledControl[i].SpotUncovered = (int)LED_COLORS.OFF;
                }

                InitSeat3Thread = new Thread(() => ConnectToSeatThread(HGTableInterface.instance.seat3Client));
                InitSeat3Thread.Start();
            }

            Thread.Sleep(100);

            if (Regex.IsMatch(seatIPs[(int)SEAT.FOUR], validIPPattern))
            {
                HGTableInterface.instance.seat4Client = new Reader
                {
                    Seat = (int)SEAT.FOUR,
                    Response = string.Empty,
                    ledControl = new LEDControl[MAX_ANTENNA_SPOTS],
                    WorkerFlag = false
                };

                for (int i = 0; i < MAX_ANTENNA_SPOTS; i++)
                {
                    HGTableInterface.instance.seat4Client.ledControl[i] = new LEDControl();
                    HGTableInterface.instance.seat4Client.ledControl[i].enabled = true;
                    HGTableInterface.instance.seat4Client.ledControl[i].SpotCovered = (int)LED_COLORS.DIM_WHITE_SOLID + i;
                    HGTableInterface.instance.seat4Client.ledControl[i].SpotUncovered = (int)LED_COLORS.OFF;
                }

                InitSeat4Thread = new Thread(() => ConnectToSeatThread(HGTableInterface.instance.seat4Client));
                InitSeat4Thread.Start();
            }

            Thread.Sleep(100);

            if (Regex.IsMatch(seatIPs[(int)SEAT.FIVE], validIPPattern))
            {
                HGTableInterface.instance.seat5Client = new Reader
                {
                    Seat = (int)SEAT.FIVE,
                    Response = string.Empty,
                    ledControl = new LEDControl[MAX_ANTENNA_SPOTS],
                    WorkerFlag = false
                };

                for (int i = 0; i < MAX_ANTENNA_SPOTS; i++)
                {
                    HGTableInterface.instance.seat5Client.ledControl[i] = new LEDControl();
                    HGTableInterface.instance.seat5Client.ledControl[i].enabled = true;
                    HGTableInterface.instance.seat5Client.ledControl[i].SpotCovered = (int)LED_COLORS.DIM_WHITE_SOLID + i;
                    HGTableInterface.instance.seat5Client.ledControl[i].SpotUncovered = (int)LED_COLORS.OFF;
                }

                InitSeat5Thread = new Thread(() => ConnectToSeatThread(HGTableInterface.instance.seat5Client));
                InitSeat5Thread.Start();
            }

            Thread.Sleep(100);

            if (Regex.IsMatch(seatIPs[(int)SEAT.SIX], validIPPattern))
            {
                HGTableInterface.instance.seat6Client = new Reader
                {
                    Seat = (int)SEAT.SIX,
                    Response = string.Empty,
                    ledControl = new LEDControl[MAX_ANTENNA_SPOTS],
                    WorkerFlag = false
                };

                for (int i = 0; i < MAX_ANTENNA_SPOTS; i++)
                {
                    HGTableInterface.instance.seat6Client.ledControl[i] = new LEDControl();
                    HGTableInterface.instance.seat6Client.ledControl[i].enabled = true;
                    HGTableInterface.instance.seat6Client.ledControl[i].SpotCovered = (int)LED_COLORS.DIM_WHITE_SOLID + i;
                    HGTableInterface.instance.seat6Client.ledControl[i].SpotUncovered = (int)LED_COLORS.OFF;
                }

                InitSeat6Thread = new Thread(() => ConnectToSeatThread(HGTableInterface.instance.seat6Client));
                InitSeat6Thread.Start();
            }

            Thread.Sleep(100);

            if (Regex.IsMatch(seatIPs[(int)SEAT.SEVEN], validIPPattern))
            {
                HGTableInterface.instance.seat7Client = new Reader
                {
                    Seat = (int)SEAT.SEVEN,
                    Response = string.Empty,
                    ledControl = new LEDControl[MAX_ANTENNA_SPOTS],
                    WorkerFlag = false
                };

                for (int i = 0; i < MAX_ANTENNA_SPOTS; i++)
                {
                    HGTableInterface.instance.seat7Client.ledControl[i] = new LEDControl();
                    HGTableInterface.instance.seat7Client.ledControl[i].enabled = true;
                    HGTableInterface.instance.seat7Client.ledControl[i].SpotCovered = (int)LED_COLORS.DIM_WHITE_SOLID + i;
                    HGTableInterface.instance.seat7Client.ledControl[i].SpotUncovered = (int)LED_COLORS.OFF;
                }

                InitSeat7Thread = new Thread(() => ConnectToSeatThread(HGTableInterface.instance.seat7Client));
                InitSeat7Thread.Start();
            }

            Thread.Sleep(100);

            if (Regex.IsMatch(seatIPs[(int)SEAT.EIGHT], validIPPattern))
            {
                HGTableInterface.instance.seat8Client = new Reader
                {
                    Seat = (int)SEAT.EIGHT,
                    Response = string.Empty,
                    ledControl = new LEDControl[MAX_ANTENNA_SPOTS],
                    WorkerFlag = false
                };

                for (int i = 0; i < MAX_ANTENNA_SPOTS; i++)
                {
                    HGTableInterface.instance.seat8Client.ledControl[i] = new LEDControl();
                    HGTableInterface.instance.seat8Client.ledControl[i].enabled = true;
                    HGTableInterface.instance.seat8Client.ledControl[i].SpotCovered = (int)LED_COLORS.DIM_WHITE_SOLID + i;
                    HGTableInterface.instance.seat8Client.ledControl[i].SpotUncovered = (int)LED_COLORS.OFF;
                }

                InitSeat8Thread = new Thread(() => ConnectToSeatThread(HGTableInterface.instance.seat8Client));
                InitSeat8Thread.Start();
            }

            Thread.Sleep(100);

            if (Regex.IsMatch(seatIPs[(int)SEAT.NINE], validIPPattern))
            {
                HGTableInterface.instance.seat9Client = new Reader
                {
                    Seat = (int)SEAT.NINE,
                    Response = string.Empty,
                    ledControl = new LEDControl[MAX_ANTENNA_SPOTS],
                    WorkerFlag = false
                };

                for (int i = 0; i < MAX_ANTENNA_SPOTS; i++)
                {
                    HGTableInterface.instance.seat9Client.ledControl[i] = new LEDControl();
                    HGTableInterface.instance.seat9Client.ledControl[i].enabled = true;
                    HGTableInterface.instance.seat9Client.ledControl[i].SpotCovered = (int)LED_COLORS.DIM_WHITE_SOLID + i;
                    HGTableInterface.instance.seat9Client.ledControl[i].SpotUncovered = (int)LED_COLORS.OFF;
                }

                InitSeat9Thread = new Thread(() => ConnectToSeatThread(HGTableInterface.instance.seat9Client));
                InitSeat9Thread.Start();
            }

            Thread.Sleep(100);

            if (Regex.IsMatch(seatIPs[(int)SEAT.TEN], validIPPattern))
            {
                HGTableInterface.instance.seat10Client = new Reader
                {
                    Seat = (int)SEAT.TEN,
                    Response = string.Empty,
                    ledControl = new LEDControl[MAX_ANTENNA_SPOTS],
                    WorkerFlag = false
                };

                for (int i = 0; i < MAX_ANTENNA_SPOTS; i++)
                {
                    HGTableInterface.instance.seat10Client.ledControl[i] = new LEDControl();
                    HGTableInterface.instance.seat10Client.ledControl[i].enabled = true;
                    HGTableInterface.instance.seat10Client.ledControl[i].SpotCovered = (int)LED_COLORS.DIM_WHITE_SOLID + i;
                    HGTableInterface.instance.seat10Client.ledControl[i].SpotUncovered = (int)LED_COLORS.OFF;
                }

                InitSeat10Thread = new Thread(() => ConnectToSeatThread(HGTableInterface.instance.seat10Client));
                InitSeat10Thread.Start();
            }

            Thread.Sleep(100);

            if (Regex.IsMatch(seatIPs[(int)SEAT.GPIO], validIPPattern))
            {
                HGTableInterface.instance.seatGPIOClient = new Reader
                {
                    Seat = (int)SEAT.GPIO,
                    Response = string.Empty,
                    ledControl = new LEDControl[MAX_ANTENNA_SPOTS],
                    WorkerFlag = false
                };

                for (int i = 0; i < MAX_ANTENNA_SPOTS; i++)
                {
                    HGTableInterface.instance.seatGPIOClient.ledControl[i] = new LEDControl();
                    HGTableInterface.instance.seatGPIOClient.ledControl[i].enabled = false;
                    HGTableInterface.instance.seatGPIOClient.ledControl[i].SpotCovered = (int)LED_COLORS.DIM_WHITE_SOLID + i;
                    HGTableInterface.instance.seatGPIOClient.ledControl[i].SpotUncovered = (int)LED_COLORS.OFF;
                }

                InitSeatGPIOThread = new Thread(() => ConnectToSeatThread(HGTableInterface.instance.seatGPIOClient));
                InitSeatGPIOThread.Start();
            }
        }

        private void ConnectToSeatThread(Reader client)
        {
            IPAddress ipAddress = IPAddress.Parse(HGTableInterface.instance.seatIPs[client.Seat]);
            IPEndPoint remoteEP = new(ipAddress, Convert.ToInt32(HGTableInterface.instance.seatPorts[client.Seat]));

            try
            {
                // Create a TCP/IP socket.  
                client.Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                Console.WriteLine($"Attempting to Connect to Seat {client.Seat} at {ipAddress}:{HGTableInterface.instance.seatPorts[client.Seat]}");
                logger.Debug($"Attempting to Connect to Seat {client.Seat} at {ipAddress}:{HGTableInterface.instance.seatPorts[client.Seat]}");

                // Connect to the remote endpoint.  
                IAsyncResult result = client.Socket.BeginConnect(remoteEP, null, null);
                bool success = result.AsyncWaitHandle.WaitOne(2000, true);

                if (success)
                {
                    client.Socket.EndConnect(result);
                    client.InitUtilityThread = new Thread(() => InitUtilityHandlerThread(client));
                    client.InitUtilityThread.Start();
                    Thread.Sleep(100);
                    client.InitDebugThread = new Thread(() => InitDebugHandlerThread(client));
                    client.InitDebugThread.Start();
                    Thread.Sleep(100);
                    client.InitLEDThread = new Thread(() => InitLEDHandlerThread(client));
                    client.InitLEDThread.Start();
                }
                else if (!HGTableInterface.instance.exitClicked)
                {
                    for (int i = 0; i < 30; i++)
                    {
                        //Console.WriteLine($"Unable to Connect to Seat Client, Retrying...");
                        client.Socket.Close();
                        Thread.Sleep(250);
                        Console.WriteLine($"Attempting to Re-Connect to Seat {client.Seat} at {ipAddress}:{seatPorts[client.Seat]}");
                        logger.Debug($"Attempting to Re-Connect to Seat {client.Seat} at {ipAddress}:{HGTableInterface.instance.seatPorts[client.Seat]}");

                        client.Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                        //Connect to the remote endpoint.
                        result = client.Socket.BeginConnect(remoteEP, null, null);
                        success = result.AsyncWaitHandle.WaitOne(2000, true);

                        if (success)
                        {
                            client.Socket.EndConnect(result);
                            client.InitUtilityThread = new Thread(() => InitUtilityHandlerThread(client));
                            client.InitUtilityThread.Start();
                            Thread.Sleep(100);
                            client.InitDebugThread = new Thread(() => InitDebugHandlerThread(client));
                            client.InitDebugThread.Start();
                            Thread.Sleep(100);
                            client.InitLEDThread = new Thread(() => InitLEDHandlerThread(client));
                            client.InitLEDThread.Start();
                            break;
                        }
                    }

                    if (!SeatConnected(client.Socket, client.Seat))
                    {
                        Console.WriteLine($"Failed to Connect to Seat {client.Seat}, Trying Next Seat....");
                        logger.Debug($"Failed to Connect to Seat {client.Seat}, Trying Next Seat....");
                        return;
                    }
                }

                Console.WriteLine($"Connected To Seat {client.Seat} at {client.Socket.RemoteEndPoint}!");
                logger.Debug($"Connected To Seat {client.Seat} at {client.Socket.RemoteEndPoint}!");

                StartSeatA2CommandTimerTask(client);
                StartSeatA2ResponseTimerTask(client);
                Command_0xA2(client.Socket);
                HGTableInterface.instance.connectedReadersDict.Add(client, client.Seat);
            }
            catch (SocketException)
            {
                Console.WriteLine($"InitSeatHandler SocketException, Attempting to Re-Connect....");
            }
            catch (Exception e)
            {
                Console.WriteLine($"InitSeatHandler Error: {e.Message}\n{e.StackTrace}");
                logger.Error($"InitSeatHandler Error: {e.Message}\n{e.StackTrace}");
            }
        }

        private void InitUtilityHandlerThread(Reader client)
        {
            IPAddress ipAddress = IPAddress.Parse(HGTableInterface.instance.seatIPs[client.Seat]);
            IPEndPoint remoteEP = new(ipAddress, Convert.ToInt32(HGTableInterface.instance.UtilityPort));

            try
            {
                // Create a TCP/IP socket.  
                client.UtilitySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                Console.WriteLine($"Attempting to Connect to Seat {client.Seat} Utility Control Socket at {ipAddress}:{HGTableInterface.instance.UtilityPort}");
                logger.Debug($"Attempting to Connect to Seat {client.Seat} Utility Control Socket at {ipAddress}:{HGTableInterface.instance.UtilityPort}");

                // Connect to the remote endpoint.  
                IAsyncResult result = client.UtilitySocket.BeginConnect(remoteEP, null, null);
                bool success = result.AsyncWaitHandle.WaitOne(2000, true);

                if (success)
                {
                    client.UtilitySocket.EndConnect(result);
                }
                else if (!HGTableInterface.instance.exitClicked)
                {
                    for (int i = 0; i < 30; i++)
                    {
                        client.UtilitySocket.Close();
                        Thread.Sleep(250);
                        Console.WriteLine($"Attempting to Re-Connect to Seat {client.Seat} Utility Control Socket at {ipAddress}:{HGTableInterface.instance.UtilityPort}");
                        logger.Debug($"Attempting to Re-Connect to Seat {client.Seat} Utility Control Socket at {ipAddress}:{HGTableInterface.instance.UtilityPort}");

                        client.UtilitySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                        //Connect to the remote endpoint.
                        result = client.UtilitySocket.BeginConnect(remoteEP, null, null);
                        success = result.AsyncWaitHandle.WaitOne(2000, true);

                        if (success)
                        {
                            client.UtilitySocket.EndConnect(result);
                            break;
                        }
                    }

                    if (!SeatConnected(client.UtilitySocket, client.Seat))
                    {
                        Console.WriteLine($"Failed to Connect to Seat {client.Seat} Utility Control Socket");
                        logger.Debug($"Failed to Connect to Seat {client.Seat} Utility Control Socket");
                        return;
                    }

                    Thread.Sleep(100);
                }

                Console.WriteLine($"Connected To Seat {client.Seat} Utility Control Socket at {client.UtilitySocket.RemoteEndPoint}!");
                logger.Debug($"Connected To Seat {client.Seat} Utility Control Socket at {client.UtilitySocket.RemoteEndPoint}!");

                client.UtilityThread = new Thread(() => UtilityHandlerThread(client));
                client.UtilityThread.Start();
                Thread.Sleep(500);
                byte[] bytes = new byte[] { 0x35 };
                HGTableInterface.instance.Send(client.UtilitySocket, bytes);
            }
            catch (SocketException)
            {
                Console.WriteLine($"InitUtilityHandlerThread SocketException, Attempting to Re-Connect....");
            }
            catch (Exception e)
            {
                Console.WriteLine($"InitUtilityHandlerThread Error: {e.Message}\n{e.StackTrace}");
                logger.Error($"InitUtilityHandlerThread Error: {e.Message}\n{e.StackTrace}");
            }
        }

        private void InitDebugHandlerThread(Reader client)
        {
            IPAddress ipAddress = IPAddress.Parse(HGTableInterface.instance.seatIPs[client.Seat]);
            IPEndPoint remoteEP = new(ipAddress, Convert.ToInt32(HGTableInterface.instance.DebugPort));

            try
            {
                // Create a TCP/IP socket.  
                client.DebugSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                Console.WriteLine($"Attempting to Connect to Seat {client.Seat} Debug Control Socket at {ipAddress}:{HGTableInterface.instance.DebugPort}");
                logger.Debug($"Attempting to Connect to Seat {client.Seat} Debug Control Socket at {ipAddress}:{HGTableInterface.instance.DebugPort}");

                // Connect to the remote endpoint.  
                IAsyncResult result = client.DebugSocket.BeginConnect(remoteEP, null, null);
                bool success = result.AsyncWaitHandle.WaitOne(2000, true);

                if (success)
                {
                    client.DebugSocket.EndConnect(result);
                }
                else if (!HGTableInterface.instance.exitClicked)
                {
                    for (int i = 0; i < 30; i++)
                    {
                        client.DebugSocket.Close();
                        Thread.Sleep(250);
                        Console.WriteLine($"Attempting to Re-Connect to Seat {client.Seat} Debug Control Socket at {ipAddress}:{HGTableInterface.instance.DebugPort}");
                        logger.Debug($"Attempting to Re-Connect to Seat {client.Seat} Debug Control Socket at {ipAddress}:{HGTableInterface.instance.DebugPort}");

                        client.DebugSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                        //Connect to the remote endpoint.
                        result = client.DebugSocket.BeginConnect(remoteEP, null, null);
                        success = result.AsyncWaitHandle.WaitOne(2000, true);

                        if (success)
                        {
                            client.DebugSocket.EndConnect(result);
                            break;
                        }
                    }

                    if (!SeatConnected(client.DebugSocket, client.Seat))
                    {
                        Console.WriteLine($"Failed to Connect to Seat {client.Seat} Debug Control Socket");
                        logger.Debug($"Failed to Connect to Seat {client.Seat} Debug Control Socket");
                        return;
                    }

                    Thread.Sleep(100);
                }

                Console.WriteLine($"Connected To Seat {client.Seat} Debug Control Socket at {client.DebugSocket.RemoteEndPoint}!");
                logger.Debug($"Connected To Seat {client.Seat} Debug Control Socket at {client.DebugSocket.RemoteEndPoint}!");

                client.DebugThread = new Thread(() => DebugHandlerThread(client));
                client.DebugThread.Start();

                Thread.Sleep(500);
                string msg = @"picr 0\r";
                HGTableInterface.instance.Send(client.DebugSocket, msg);

                if (!ConfigurationManager.AppSettings.Get("LEDDemoModeOn").ToLower().Equals("true"))
                {
                    Thread.Sleep(500);
                    HGTableInterface.instance.Send(client.LEDSocket, @"$ff070295~"); // Turn off demo mode
                    Thread.Sleep(500);
                    SetLEDColor(client, ALL_OFF, Convert.ToInt32(LED_COLORS.OFF)); // Turn off all LEDs
                }
            }
            catch (SocketException)
            {
                Console.WriteLine($"InitDebugHandlerThread SocketException, Attempting to Re-Connect....");
            }
            catch (Exception e)
            {
                Console.WriteLine($"InitDebugHandlerThread Error: {e.Message}\n{e.StackTrace}");
                logger.Error($"InitDebugHandlerThread Error: {e.Message}\n{e.StackTrace}");
            }
        }

        private void InitLEDHandlerThread(Reader client)
        {
            IPAddress ipAddress = IPAddress.Parse(HGTableInterface.instance.seatIPs[client.Seat]);
            IPEndPoint remoteEP = new(ipAddress, Convert.ToInt32(HGTableInterface.instance.LEDPort));

            try
            {
                // Create a TCP/IP socket.  
                client.LEDSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                Console.WriteLine($"Attempting to Connect to Seat {client.Seat} LED Control Socket at {ipAddress}:{HGTableInterface.instance.LEDPort}");
                logger.Debug($"Attempting to Connect to Seat {client.Seat} LED Control Socket at {ipAddress}:{HGTableInterface.instance.LEDPort}");

                // Connect to the remote endpoint.  
                IAsyncResult result = client.LEDSocket.BeginConnect(remoteEP, null, null);
                bool success = result.AsyncWaitHandle.WaitOne(2000, true);

                if (success)
                {
                    client.LEDSocket.EndConnect(result);
                }
                else if (!HGTableInterface.instance.exitClicked)
                {
                    for (int i = 0; i < 30; i++)
                    {
                        //Console.WriteLine($"Unable to Connect to Seat Client, Retrying...");
                        client.LEDSocket.Close();
                        Thread.Sleep(250);
                        Console.WriteLine($"Attempting to Re-Connect to Seat {client.Seat} LED Control Socket at {ipAddress}:{HGTableInterface.instance.LEDPort}");
                        logger.Debug($"Attempting to Re-Connect to Seat {client.Seat} LED Control Socket at {ipAddress}:{HGTableInterface.instance.LEDPort}");

                        client.LEDSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                        //Connect to the remote endpoint.
                        result = client.LEDSocket.BeginConnect(remoteEP, null, null);
                        success = result.AsyncWaitHandle.WaitOne(2000, true);

                        if (success)
                        {
                            client.LEDSocket.EndConnect(result);
                            break;
                        }
                    }

                    if (!SeatConnected(client.LEDSocket, client.Seat))
                    {
                        Console.WriteLine($"Failed to Connect to Seat {client.Seat} LED Control Socket");
                        logger.Debug($"Failed to Connect to Seat {client.Seat} LED Control Socket");
                        return;
                    }

                    Thread.Sleep(100);
                }

                Console.WriteLine($"Connected To Seat {client.Seat} LED Control Socket at {client.LEDSocket.RemoteEndPoint}!");
                logger.Debug($"Connected To Seat {client.Seat} LED Control Socket at {client.LEDSocket.RemoteEndPoint}!");

                client.LEDThread = new Thread(() => LEDHandlerThread(client));
                client.LEDThread.Start();
            }
            catch (SocketException)
            {
                Console.WriteLine($"InitLEDHandlerThread SocketException, Attempting to Re-Connect....");
            }
            catch (Exception e)
            {
                Console.WriteLine($"InitLEDHandlerThread Error: {e.Message}\n{e.StackTrace}");
                logger.Error($"InitLEDHandlerThread Error: {e.Message}\n{e.StackTrace}");
            }
        }

        private void UtilityHandlerThread(Reader client)
        {
            byte[] bytes = new byte[1024];
            int bytesRcvd;

            Console.WriteLine($"Utility Control Socket Response Handler Started.");
            logger.Debug($"Utility Control Socket Response Handler Started.");

            try
            {
                client.UtilityWorkerFlag = true;

                while (client.UtilityWorkerFlag)
                {
                    bytesRcvd = client.UtilitySocket.Receive(bytes);
                    //Console.WriteLine($"Received from Seat {client.Seat} on Socket {client.DebugSocket.RemoteEndPoint} {bytesRcvd} bytes");

                    if (bytesRcvd > 0)
                    {
                        var message = Convert.ToHexString(bytes, 0, bytesRcvd);
                        Console.WriteLine($"Response for Seat {client.Seat} Utility Socket Received : {message}");
                        logger.Info($"Response for Seat {client.Seat} Utility Socket Received : {message}");
                    }

                    Thread.Sleep(100);
                }
            }
            catch (Exception e)
            {
                if (!e.GetType().Name.Equals("ExtendedSocketException"))
                {
                    Console.WriteLine($"Seat {client.Seat} Utility Control Socket Response Handler Error: {e.GetType().Name}, {e.Message}\n{e.StackTrace}");
                    logger.Debug($"Seat {client.Seat} Utility Control Socket Response Handler Error: {e.GetType()}, {e.Message}\n{e.StackTrace}");
                }
            }
        }

        private void DebugHandlerThread(Reader client)
        {
            byte[] bytes = new byte[1024];
            int bytesRcvd;

            Console.WriteLine($"Debug Control Socket Response Handler Started.");
            logger.Debug($"Debug Control Socket Response Handler Started.");

            try
            {
                client.DebugWorkerFlag = true;

                while (client.DebugWorkerFlag)
                {
                    bytesRcvd = client.DebugSocket.Receive(bytes);
                    //Console.WriteLine($"Received from Seat {client.Seat} on Socket {client.DebugSocket.RemoteEndPoint} {bytesRcvd} bytes");

                    if (bytesRcvd > 0)
                    {
                        var message = System.Text.Encoding.ASCII.GetString(bytes, 0, bytesRcvd);
                        Console.WriteLine($"Response for Seat {client.Seat} Debug Socket Received : {message}");
                        logger.Info($"Response for Seat {client.Seat} Debug Socket Received : {message}");
                    }

                    Thread.Sleep(100);
                }
            }
            catch (Exception e)
            {
                if (!e.GetType().Name.Equals("ExtendedSocketException"))
                {
                    Console.WriteLine($"Seat {client.Seat} Debug Control Socket Response Handler Error: {e.GetType().Name}, {e.Message}\n{e.StackTrace}");
                    logger.Debug($"Seat {client.Seat} Debug Control Socket Response Handler Error: {e.GetType()}, {e.Message}\n{e.StackTrace}");
                }
            }
        }

        private void LEDHandlerThread(Reader client)
        {
            byte[] bytes = new byte[1024];
            int bytesRcvd;

            Console.WriteLine($"LED Control Socket Response Handler Started.");
            logger.Debug($"LED Control Socket Response Handler Started.");

            try
            {
                client.LEDWorkerFlag = true;

                while (client.LEDWorkerFlag)
                {
                    bytesRcvd = client.LEDSocket.Receive(bytes);
                    //Console.WriteLine($"Received from Seat {client.Seat} on Socket {client.LEDSocket.RemoteEndPoint} {bytesRcvd} bytes");

                    if (bytesRcvd > 0)
                    {
                        var message = System.Text.Encoding.ASCII.GetString(bytes, 0, bytesRcvd);

                        if (instance.showLEDControlDiags.Equals("true"))
                        {
                            Console.WriteLine($"Response for Seat {client.Seat} LED Socket Received : {message}");
                            logger.Info($"Response for Seat {client.Seat} LED Socket Received : {message}");
                        }
                    }

                    Thread.Sleep(50);
                }
            }
            catch (Exception e)
            {
                if (!e.GetType().Name.Equals("ExtendedSocketException"))
                {
                    Console.WriteLine($"Seat {client.Seat} LED Control Socket Response Handler Error: {e.GetType().Name}, {e.Message}\n{e.StackTrace}");
                    logger.Debug($"Seat {client.Seat} LED Control Socket Response Handler Error: {e.GetType()}, {e.Message}\n{e.StackTrace}");
                }
            }
        }

        private void A2ResponseTimeoutHandler(Object source, ElapsedEventArgs args, Reader client)
        {
            bool removed;

            Console.WriteLine($"Communications Timeout for Seat {client.Seat} at {client.Socket.RemoteEndPoint}");
            logger.Debug($"Communications Timeout for Seat {client.Seat} at {client.Socket.RemoteEndPoint}");

            client.A2CommandTimer.Enabled = false;

            // Expire any chips that are this seat's antenna
            ConcurrentDictionary<string, Tuple<string, int>> tempDict = new(HGTableInterface.instance.chipLocationPrev); // get a working copy of the previous location dictionary

            if (tempDict.Any())
            {
                foreach (var chip in tempDict)
                {
                    if (chip.Value.Item2 == client.Seat)
                    {
                        Console.WriteLine($"Info,Tag:Expired {chip.Key}"); // send an expiration message for the previous sighting of this chip
                        GGHost.Send($"Info,Tag:Expired {chip.Key}"); // send an expiration message for the previous sighting of this chip
                        removed = HGTableInterface.instance.chipLocationPrev.TryRemove(chip.Key, out Tuple<string, int> temp); // remove the chip from the previous sighting dictionary so it will be re-sighted

                        if (!removed)
                        {
                            Console.WriteLine($"BuildChipMessage: Unable to Remove UID {chip.Key} from the Location Previous Dictionary.");
                            logger.Debug($"BuildChipMessage: Unable to Remove UID {chip.Key} from the Location Previous Dictionary.");
                        }

                        removed = HGTableInterface.instance.chipLocationNow.TryRemove(chip.Key, out temp); // remove the chip from the sighting dictionary so it will be re-sighted

                        if (!removed)
                        {
                            Console.WriteLine($"BuildChipMessage: Unable to Remove UID {chip.Key} from the Location Now Dictionary.");
                            logger.Debug($"BuildChipMessage: Unable to Remove UID {chip.Key} from the Location Now Dictionary.");
                        }
                    }
                }
            }

            // Try and Re-connect to the reader 
            IPAddress ipAddress = IPAddress.Parse(HGTableInterface.instance.seatIPs[client.Seat]);
            IPEndPoint remoteEP = new(ipAddress, Convert.ToInt32(HGTableInterface.instance.seatPorts[client.Seat]));

            while (true)
            {
                if (!HGTableInterface.instance.exitClicked)
                {
                    try
                    {
                        client.Socket.Close();
                        HGTableInterface.instance.connectedReadersDict.Remove(client);
                        Thread.Sleep(250);
                        Console.WriteLine($"Attempting to Connect to Seat {client.Seat} at {ipAddress}:{HGTableInterface.instance.seatPorts[client.Seat]}");
                        logger.Debug($"Attempting to Connect to Seat {client.Seat} at {ipAddress}:{HGTableInterface.instance.seatPorts[client.Seat]}");
                        client.Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        IAsyncResult result = client.Socket.BeginConnect(remoteEP, null, null);
                        bool success = result.AsyncWaitHandle.WaitOne(2000, true);

                        if (success)
                        {
                            client.Socket.EndConnect(result);
                            Console.WriteLine($"Connected To Seat {client.Seat} at {client.Socket.RemoteEndPoint}!");
                            logger.Debug($"Connected To Seat {client.Seat} at {client.Socket.RemoteEndPoint}!");
                            StartSeatA2ResponseTimerTask(client);
                            Command_0xA2(client.Socket);
                            HGTableInterface.instance.connectedReadersDict.Add(client, client.Seat);
                            client.A2CommandTimer.Enabled = true;
                            break;
                        }
                        else
                        {
                            Console.WriteLine($"Failed to Connect to Seat {client.Seat} at {client.Socket.RemoteEndPoint}, Re-Trying....");
                            logger.Debug($"Failed to Connect to Seat {client.Seat} at {client.Socket.RemoteEndPoint}, Re-Trying....");
                        }

                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"A2ResponseTimeoutHandler Error: {e.Message}\n{e.StackTrace}");
                        logger.Error($"A2ResponseTimeoutHandler Error: {e.Message}\n{e.StackTrace}");
                    }
                }

                Thread.Sleep(300);
            }
        }

        private static void StartSeatA2CommandTimerTask(Reader client)
        {
            client.A2CommandTimer = new System.Timers.Timer(A2_COMMAND_TIMEOUT_VALUE);
            client.A2CommandTimer.Elapsed += (sender, e) => HGTableInterface.instance.A2CommandResponseHandler(sender, e, client);
            client.A2CommandTimer.AutoReset = false;
            client.A2CommandTimer.Enabled = true;
        }

        private static void StartSeatA2ResponseTimerTask(Reader client)
        {
            Console.WriteLine($"Initializing A2 Response Timer for Seat {client.Seat}");

            client.A2ResponseTimer = new System.Timers.Timer(A2_RESPONSE_TIMEOUT_VALUE);
            client.A2ResponseTimer.Elapsed += (sender, e) => HGTableInterface.instance.A2ResponseTimeoutHandler(sender, e, client);
            client.A2ResponseTimer.AutoReset = false;
            client.A2ResponseTimer.Start();
        }

        private bool SeatConnected(Socket sock, int seat)
        {
            bool blockingState = sock.Blocking;

            try
            {
                byte[] tmp = new byte[1];

                sock.Blocking = false;
                sock.Send(tmp, 0, 0);
                //Console.WriteLine($"Seat {seat} Connected!");
            }
            catch (SocketException e)
            {
                // 10035 == WSAEWOULDBLOCK
                if (e.NativeErrorCode.Equals(10035))
                {
                    Console.WriteLine($"Seat {seat} Still Connected, but the Send would block");
                }
                else
                {
                    Console.WriteLine($"Seat {seat} Disconnected: error code {e.NativeErrorCode}!");
                    logger.Error($"Seat {seat} Disconnected: error code {e.NativeErrorCode}!");
                    return false;
                }
            }
            finally
            {
                sock.Blocking = blockingState;
            }

            //Console.WriteLine("Seat {0} Connected: {1}", seat, client.Socket.Connected);
            return true;
        }

#if USE_DENOM_DATA
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
                        chipsDict.Add(msgba[0].Substring(12), chipData); // skip the 12 leading zeros of the chip sn record
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
#endif

        private void Command_0xA2(Socket sock)
        {
            try
            {
#if USE_INTEGRATED_READER
                Send(sock, HGTableInterface.instance.A2IRCommandBytes);
#else
                Send(sock, HGTableInterface.instance.A2RouletteCommandBytes);
#endif
            }
            catch (Exception e)
            {
                Console.WriteLine($"Command_0xA2 Error: {e.Message}");
            }
        }

        public byte[] Assemble0xA2DataIR()
        {
            byte[] temp = new byte[BYTES_IN_IR_A2_COMMAND];

            temp[0] = 0xA2;
            temp[1] = 0x06;
            temp[2] = 0x74;
            temp[3] = 0x00;
            temp[4] = 0x00;
            temp[5] = 0x7F;
            temp[6] = 0x00;
            temp[7] = 0x00;
            temp[8] = 0x0C;
            temp[9] = 0x00;
            temp[10] = 0x00;
            temp[11] = 0x00;
            temp[12] = 0x00;
            temp[13] = 0x00;
            temp[14] = 0x00;
            temp[15] = 0x00;

            return temp;
        }

        public byte[] Assemble0xA2DataRoulette()
        {
            var ROULETTE_GEN_3_ID = 0x0B;
            byte[] temp = new byte[BYTES_IN_ROULETTE_A2_COMMAND];
            byte[] bitflags = new byte[65];

            try
            {
                bitflags[0] = 0xFF; bitflags[1] = 0xFF; bitflags[2] = 0xFF; bitflags[3] = 0xFF; bitflags[4] = 0xFF; bitflags[5] = 0xFF; bitflags[6] = 0xFF; bitflags[7] = 0x07;
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


        private void Send(Socket sock, String msg)
        {
            if (instance.showLEDControlDiags.Equals("true"))
                Console.WriteLine($"Sending {msg} to Socket {sock.RemoteEndPoint}");

            // Convert the string data to byte data using ASCII encoding.
            Send(sock, Encoding.ASCII.GetBytes(msg));
        }

        private void Send(Socket sock, byte[] data)
        {
            try
            {
                if (sock.Connected)
                {
                    sock.Send(data);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Socket Send Error: {e.Message}");
            }
        }

        private string GetLEDCommandCheckSum(string msg)
        {
            int chksum = 0;

            foreach (var c in msg)
            {
                chksum += c;
            }

            return Convert.ToHexString(new byte[] { (byte)(chksum & 0xff)}).ToLower();
        }

#if false
        private long EncryptUID(long uid, byte seed)
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
#endif

#if USE_DENOM_DATA
        private int getDenomHex(String denom) // calculate the hex value of the denomination from the passed denom string
        {
            int value = 0;
            int index;

            for (index = 0; index < HGTableInterface.instance.denomArray.Length; index++)
            {
                if (HGTableInterface.instance.denomArray[index] == Convert.ToDouble(denom)) // if the denominations match
                    break;
            }

            // create the hex value that represents what a chip has encoded in it.  For Example, if the passed in denom string was 100, that equates to a hex value of 0x0401 per the official
            // denom table. Base is 1, Magnifier is 4, for a denom of 100.00.  Based on the Denomination Array, 100.00 is at index 14 which yeilds a 0x0400 after the modulo 10 with the left
            // shift of 8 and a 0x0401 after the divide by 10 and addition.
            if (index > 0 && index < HGTableInterface.instance.denomArray.Length) // range check the index
            {
                value = (index % 10) << 8; // shift the denomination magnifier into the correct nibble
                value += index / 10;       // add in the denomination base value
            }

            return value;
        }
#endif
    }

    public class Reader
    {
        public System.Timers.Timer A2ResponseTimer;
        public System.Timers.Timer A2CommandTimer;
        public Socket Socket;
        public int Seat;
        public string Response = "";
        public bool WorkerFlag = false;
        public int numChipsSeen = 0;

        public Thread InitUtilityThread;
        public Thread UtilityThread;
        public Socket UtilitySocket;
        public bool UtilityWorkerFlag = false;

        public Thread InitDebugThread;
        public Thread DebugThread;
        public Socket DebugSocket;
        public bool DebugWorkerFlag = false;

        public Thread InitLEDThread;
        public Thread LEDThread;
        public Socket LEDSocket;
        public bool LEDWorkerFlag = false;

        public LEDControl[] ledControl;
    }

    public class AntennaTranslation
    {
        public int HGIndex { get; set; }
        public int BravoIndex { get; set; }
    }

    public class LEDControl
    {
        public bool enabled { get; set; }
        public int SpotCovered { get; set; }
        public int SpotUncovered { get; set; }
        //public int Spot2Covered { get; set; }
        //public int Spot2Uncovered { get; set; }
        //public int Spot3Covered { get; set; }
        //public int Spot3Uncovered { get; set; }
        //public int Spot4Covered { get; set; }
        //public int Spot4Uncovered { get; set; }
        //public int Spot5Covered { get; set; }
        //public int Spot5Uncovered { get; set; }
        //public int Spot6Covered { get; set; }
        //public int Spot6Uncovered { get; set; }
        //public int Spot7Covered { get; set; }
        //public int Spot7Uncovered { get; set; }
        //public int Spot8Covered { get; set; }
        //public int Spot8Uncovered { get; set; }
        //public int Spot9Covered { get; set; }
        //public int Spot9Uncovered { get; set; }
    }

#if false
        public class ChipObject
    {
        public string ChipDB_SN { get; set; }
        public string ChipDB_Dollars { get; set; }
        public string ChipDB_Player { get; set; }
        public string ChipDB_SN_For_Colored { get; set; }
        public string ChipDB_Player_For_Colored { get; set; }
        public string ChipDB_Dollars_For_Colored { get; set; }
    }
#endif

#if USE_DENOM_DATA
        public class ChipData
    {
        public string ChipValue { get; set; }
        public string ChipType { get; set; }
    }
#endif
}
