using log4net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HGTranslator
{
    class GGHost
    {
        private static readonly ILog logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public static TcpListener server;
        public static bool clientListenerLoop = false;
        public static bool BravoWorkerFlag = false;
        public static bool messageInWorkerFlag = false;
        public static bool GGClientWorkerFlag = false;
        public static TcpClient BravoSocket;
        public static NetworkStream BravoStream;
        public static Dictionary<int, TcpClient> ClientSocketDict;
        public static int connectedClientCount = 1;
        public static Dictionary<int, bool> messageHandlerThreadEnabled;
        public static StreamWriter clientOut = null;
        public static string clientInMsg = string.Empty;
        public static Queue BravoQueue = new();
        public static StreamReader clientIn;
        public static string ipAddress = ConfigurationManager.AppSettings.Get("GGHostIPAddress");
        public static int connNum = 1;

        public static string groupPollPattern = @"^(GroupPoll)\S(\d*)\S$";

        public static void Start()
        {
            server = new TcpListener(IPAddress.Parse(ipAddress), 8023);
            server.Start();
            Thread msgProcessingThread = new(() => MessageInWorker());
            msgProcessingThread.Start();

            clientListenerLoop = true;
            ClientSocketDict = new Dictionary<int, TcpClient>();
            messageHandlerThreadEnabled = new Dictionary<int, bool>();
            Console.WriteLine($"Server Listening....");
            logger.Debug($"Server Listening....");

            while (clientListenerLoop)
            {
                try
                {
                    BravoSocket = server.AcceptTcpClient();
                    Console.WriteLine($"Connection #{connectedClientCount} Started!");
                    logger.Debug($"Connection #{connectedClientCount} Started!");
                    handleClient client = new handleClient();
                    client.startClient(BravoSocket, connectedClientCount);
                    connectedClientCount++;
                }
                catch (Exception e)
                {
                    if (!HGTableInterface.instance.exitClicked)
                    {
                        Console.WriteLine($"GGHost Start Error: {e.Message}\n{e.StackTrace}");
                        logger.Error($"GGHost Start Error: {e.Message}\n{e.StackTrace}");
                    }
                }

                Thread.Sleep(250);
            }
        }

        public static bool BravoSocketConnected(TcpClient bravoSocket)
        {
            if (bravoSocket == null)
                return false;

            try
            {
                return (bravoSocket.Client.Poll(1, SelectMode.SelectRead) && bravoSocket.Available == 0) == false;

            }
            catch (Exception e)
            {
                return false;
            }        
        }

        public static void ListenForBravo()
        {
            BravoSocket = new TcpClient();
            BravoSocket = server.AcceptTcpClient();
            BravoStream = BravoSocket.GetStream();
            clientIn = new StreamReader(BravoStream);
            clientOut = new StreamWriter(BravoStream)
            {
                AutoFlush = true
            };

            Console.WriteLine($"Accepted Bravo Connection from {BravoSocket.Client.RemoteEndPoint}");
            logger.Debug($"Accepted Bravo Connection from {BravoSocket.Client.RemoteEndPoint}");
        }

        private static void MessageInWorker()
        {
            try
            {
                messageInWorkerFlag = true;

                while (messageInWorkerFlag)
                {
                    if (BravoQueue.Count > 0)
                    {
                        ProcessMessage((string)BravoQueue.Dequeue());
                    }

                    Thread.Sleep(10);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Client In Message Processing Thread Error: {e.Message}\n{e.StackTrace}");
                logger.Error($"Client In Message Processing Thread Error: {e.Message}\n{e.StackTrace}");
            }
        }

        private static void ProcessMessage(string msg)
        {
            Console.WriteLine($"Message from Bravo: {msg}");
            logger.Debug($"Message from Bravo: {msg}");

            string[] msgArr = msg.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            try
            {
                foreach (var cmd in msgArr)
                {
                    if (cmd.ToLower().Equals("version()"))
                    {
                        string OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
                        Send(@"'OS': " + OS + ", " + HGTableInterface.appName.Name + ": " + HGTableInterface.appName.Version);
                    }
                    else if (cmd.ToLower().Contains("axisgroupcount("))
                    {
                        string numGroups = cmd.Substring(cmd.IndexOf('(') + 1);
                        numGroups = numGroups.Substring(0, 1);
                        HGTableInterface.instance.axisInGroup = Convert.ToInt32(numGroups);

                        if (HGTableInterface.instance.axisInGroup > (int)HGTableInterface.SEAT.MAX_SEATS + 2)
                        {
                            Console.WriteLine($"AxisGroupCount Error: Max Group Count Exceeded: {HGTableInterface.instance.axisInGroup}");
                            logger.Debug($"AxisGroupCount Error: Max Group Count Exceeded: {HGTableInterface.instance.axisInGroup}");
                        }
                        else
                        {
                            Console.WriteLine($"AxisGroupCount: {HGTableInterface.instance.axisInGroup}");
                            logger.Debug($"AxisGroupCount: {HGTableInterface.instance.axisInGroup}");
                            Array.Resize(ref HGTableInterface.instance.pollGroupArr, HGTableInterface.instance.axisInGroup);

                            // turn off manual group poll for each configured group.
                            for (int i = 0; i < HGTableInterface.instance.axisInGroup; i++)
                            {
                                HGTableInterface.instance.pollGroupArr[i] = false;
                            }
                        }
                    }
                    else if (cmd.ToLower().Contains("grouppriority("))
                    {
                        string activeGroups = cmd.Trim();
                        activeGroups = cmd.Substring(cmd.IndexOf('[') + 1);
                        //activeGroups = activeGroups.Remove(activeGroups.Length - 2);
                        activeGroups = activeGroups.TrimEnd(new char[] { ')', ']' });
                        string[] activeGroupsArr = activeGroups.Split(',');
                        //Console.WriteLine($"activeGroupsArr = {string.Join(", ", activeGroupsArr)}");

                        if (activeGroupsArr.Length == HGTableInterface.instance.axisInGroup)
                        {
                            HGTableInterface.instance.groupPriorityList = new List<int>();

                            foreach (var priority in activeGroupsArr)
                            {
                                //Console.WriteLine($"priority = {priority}");
                                HGTableInterface.instance.groupPriorityList.Add(Convert.ToInt32(priority));
                            }
                        }
                        else
                        {
                            Console.WriteLine($"GroupPriority Message Error: Group Priority Count of {activeGroupsArr.Length} != AxisGroupCount of {HGTableInterface.instance.axisInGroup}");
                            logger.Debug($"GroupPriority Message Error: Group Priority Count of {activeGroupsArr.Length} != AxisGroupCount of {HGTableInterface.instance.axisInGroup}");
                        }
                    }
                    else if (cmd.ToLower().Contains("axisingroup("))
                    {
                        //Console.WriteLine("AxisInGroup Message Seen");
                        string axisInGroupMsg = cmd.Substring(0, cmd.Length - 4);
                        axisInGroupMsg = axisInGroupMsg.Substring(cmd.IndexOf('[') + 1);
                        axisInGroupMsg = axisInGroupMsg.Trim().Replace("(", string.Empty).Replace("[", string.Empty).Replace("'", string.Empty);
                        string[] axisInGroupArr = axisInGroupMsg.Split(')');

                        for (int i = 0; i < axisInGroupArr.Length; i++)
                        {
                            axisInGroupArr[i] = axisInGroupArr[i].TrimStart(new char[] { ',', ' ' }).TrimEnd(',');
                        }

                        if (axisInGroupArr.Length == HGTableInterface.instance.axisInGroup)
                        {
                            string[] axisToIngnore = axisInGroupArr[HGTableInterface.instance.axisInGroup - 1].Split(",");
                            HGTableInterface.instance.axisToIgnoreList = new List<decimal>();
                            HGTableInterface.instance.groupNumberDict = new Dictionary<int, int>();

                            foreach (var axis in axisToIngnore)
                            {
                                if (axis != string.Empty)
                                    HGTableInterface.instance.axisToIgnoreList.Add(Convert.ToDecimal(axis));
                            }

                            HGTableInterface.instance.seat1AntennaList = new List<decimal>();
                            HGTableInterface.instance.seat2AntennaList = new List<decimal>();
                            HGTableInterface.instance.seat3AntennaList = new List<decimal>();
                            HGTableInterface.instance.seat4AntennaList = new List<decimal>();
                            HGTableInterface.instance.seat5AntennaList = new List<decimal>();
                            HGTableInterface.instance.seat6AntennaList = new List<decimal>();
                            HGTableInterface.instance.seat7AntennaList = new List<decimal>();
                            HGTableInterface.instance.seat8AntennaList = new List<decimal>();
                            HGTableInterface.instance.seat9AntennaList = new List<decimal>();
                            HGTableInterface.instance.seat10AntennaList = new List<decimal>();
                            HGTableInterface.instance.exchangeAntennaList = new List<decimal>();
                            HGTableInterface.instance.floatAntennaList = new List<decimal>();
                            var index = 0;

                            foreach (var group in axisInGroupArr)
                            {
                                int groupNum = 99;

                                if (index == HGTableInterface.instance.axisInGroup - 1)
                                    break;

                                string[] groupArr = group.Trim().Split(',');

                                for (int i = 0; i < groupArr.Length; i++)
                                {
                                    //groupArr[i] = groupArr[i].Trim();

                                    if (groupArr[i] != string.Empty)
                                    {
                                        string groupstr = "";

                                        if (groupArr[i].Contains('.'))
                                        {
                                            groupstr = groupArr[i].Replace(" ", string.Empty);
                                            groupstr = groupstr.Substring(0, groupArr[i].IndexOf('.'));
                                        }
                                        else
                                            groupstr = groupArr[i].Replace(" ", string.Empty);

                                        groupNum = Convert.ToInt32(groupstr);
                                        break;
                                    }
                                }

                                switch (groupNum)
                                {
                                    case 0:
                                        foreach (var axis in groupArr)
                                        {
                                            if (axis != string.Empty)
                                            {
                                                HGTableInterface.instance.seat1AntennaList.Add(Convert.ToDecimal(axis));

                                                if (!HGTableInterface.instance.groupNumberDict.ContainsKey(groupNum))
                                                    HGTableInterface.instance.groupNumberDict.Add(groupNum, index);
                                            }
                                        }
                                        break;

                                    case 1:
                                        foreach (var axis in groupArr)
                                        {
                                            if (axis != string.Empty)
                                            {
                                                HGTableInterface.instance.seat2AntennaList.Add(Convert.ToDecimal(axis));

                                                if (!HGTableInterface.instance.groupNumberDict.ContainsKey(groupNum))
                                                    HGTableInterface.instance.groupNumberDict.Add(groupNum, index);
                                            }
                                        }
                                        break;

                                    case 2:
                                        foreach (var axis in groupArr)
                                        {
                                            if (axis != string.Empty)
                                            {
                                                HGTableInterface.instance.seat3AntennaList.Add(Convert.ToDecimal(axis));

                                                if (!HGTableInterface.instance.groupNumberDict.ContainsKey(groupNum))
                                                    HGTableInterface.instance.groupNumberDict.Add(groupNum, index);
                                            }
                                        }
                                        break;

                                    case 3:
                                        foreach (var axis in groupArr)
                                        {
                                            if (axis != string.Empty)
                                            {
                                                HGTableInterface.instance.seat4AntennaList.Add(Convert.ToDecimal(axis));

                                                if (!HGTableInterface.instance.groupNumberDict.ContainsKey(groupNum))
                                                    HGTableInterface.instance.groupNumberDict.Add(groupNum, index);
                                            }
                                        }
                                        break;

                                    case 4:
                                        foreach (var axis in groupArr)
                                        {
                                            if (axis != string.Empty)
                                            {
                                                HGTableInterface.instance.seat5AntennaList.Add(Convert.ToDecimal(axis));

                                                if (!HGTableInterface.instance.groupNumberDict.ContainsKey(groupNum))
                                                    HGTableInterface.instance.groupNumberDict.Add(groupNum, index);
                                            }
                                        }
                                        break;

                                    case 5:
                                        foreach (var axis in groupArr)
                                        {
                                            if (axis != string.Empty)
                                            {
                                                HGTableInterface.instance.seat6AntennaList.Add(Convert.ToDecimal(axis));

                                                if (!HGTableInterface.instance.groupNumberDict.ContainsKey(groupNum))
                                                    HGTableInterface.instance.groupNumberDict.Add(groupNum, index);
                                            }
                                        }
                                        break;

                                    case 6:
                                        foreach (var axis in groupArr)
                                        {
                                            if (axis != string.Empty)
                                            {
                                                HGTableInterface.instance.seat7AntennaList.Add(Convert.ToDecimal(axis));

                                                if (!HGTableInterface.instance.groupNumberDict.ContainsKey(groupNum))
                                                    HGTableInterface.instance.groupNumberDict.Add(groupNum, index);
                                            }
                                        }
                                        break;

                                    case 7:
                                        foreach (var axis in groupArr)
                                        {
                                            if (axis != string.Empty)
                                            {
                                                HGTableInterface.instance.seat8AntennaList.Add(Convert.ToDecimal(axis));

                                                if (!HGTableInterface.instance.groupNumberDict.ContainsKey(groupNum))
                                                    HGTableInterface.instance.groupNumberDict.Add(groupNum, index);
                                            }
                                        }
                                        break;

                                    case 8:
                                        foreach (var axis in groupArr)
                                        {
                                            if (axis != string.Empty)
                                            {
                                                HGTableInterface.instance.seat9AntennaList.Add(Convert.ToDecimal(axis));

                                                if (!HGTableInterface.instance.groupNumberDict.ContainsKey(groupNum))
                                                    HGTableInterface.instance.groupNumberDict.Add(groupNum, index);
                                            }
                                        }
                                        break;

                                    case 9:
                                        foreach (var axis in groupArr)
                                        {
                                            if (axis != string.Empty)
                                            {
                                                HGTableInterface.instance.seat10AntennaList.Add(Convert.ToDecimal(axis));

                                                if (!HGTableInterface.instance.groupNumberDict.ContainsKey(groupNum))
                                                    HGTableInterface.instance.groupNumberDict.Add(groupNum, index);
                                            }
                                        }
                                        break;

                                    case 20:
                                        foreach (var axis in groupArr)
                                        {
                                            if (axis != string.Empty)
                                            {
                                                HGTableInterface.instance.exchangeAntennaList.Add(Convert.ToDecimal(axis));

                                                if (!HGTableInterface.instance.groupNumberDict.ContainsKey(groupNum))
                                                    HGTableInterface.instance.groupNumberDict.Add(groupNum, index);
                                            }
                                        }
                                        break;

                                    case 22:
                                        foreach (var axis in groupArr)
                                        {
                                            if (axis != string.Empty)
                                            {
                                                HGTableInterface.instance.floatAntennaList.Add(Convert.ToDecimal(axis));

                                                if (!HGTableInterface.instance.groupNumberDict.ContainsKey(groupNum))
                                                    HGTableInterface.instance.groupNumberDict.Add(groupNum, index);
                                            }
                                        }
                                        break;

                                    default:
                                        Console.WriteLine($"AxisInGroup, Invalid Group Number: {groupNum}");
                                        logger.Debug($"AxisInGroup, Invalid Group Number: {groupNum}");
                                        break;
                                }

                                index++;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"AxisInGroup Message Error: Group Count of {axisInGroupArr.Length} != AxisGroupCount of {HGTableInterface.instance.axisInGroup}");
                            logger.Debug($"AxisInGroup Message Error: Group Count of {axisInGroupArr.Length} != AxisGroupCount of {HGTableInterface.instance.axisInGroup}");
                        }
                    }
                    else if (cmd.ToLower().Contains("flushtags("))
                    {
                        var axis = cmd.Substring(cmd.IndexOf('(') + 1);
                        axis = axis.TrimEnd(')');

                        //Console.WriteLine("Flush Tags Message for Axis {axis} Seen");

                        HGTableInterface.instance.FlushingTags = true;
                        HGTableInterface.instance.FlushTags(axis);
                    }
                    else if (cmd.ToLower().Contains("grouppoll("))
                    {
                        int groupNum;
                        var group = cmd.Substring(cmd.IndexOf('(') + 1);
                        group = group.TrimEnd(')');

                        //Console.WriteLine($"Group Poll Message for Axis {groupNum} Seen");

                        if (group.Length < 1)
                        {
                            Console.WriteLine("Invalid GroupPoll Command,  Must Have a Group Number Included!");
                            logger.Debug("Invalid GroupPoll Command,  Must Have a Group Number Included!");
                            return;
                        }

                        groupNum = Convert.ToInt32(group);

                        if (groupNum <= HGTableInterface.instance.axisInGroup - 1)
                        {
                            for (int i = 0; i < HGTableInterface.instance.axisInGroup; i++)
                            {
                                if (i == groupNum)
                                    HGTableInterface.instance.pollGroupArr[i] = true;
                                else
                                    HGTableInterface.instance.pollGroupArr[i] = false;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Invalid Group Number in GroupPoll Command: {groupNum} > Than Number of Configured Groups!");
                            logger.Debug($"Invalid Group Number in GroupPoll Command: {groupNum} > Than Number of Configured Groups!");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Unsupported Message from Bravo: {cmd}");
                        logger.Debug($"Unsupported Message from Bravo: {cmd}");
                    }
                }

            }
            catch (Exception e)
            {
                Console.WriteLine($"GGHost.ProcessMessage Error: {e.Message}\n{e.StackTrace}");
                logger.Error($"GGHost.ProcessMessage Error: {e.Message}\n{e.StackTrace}");
            }
        }

        public static void ShutDown()
        {
            if (server != null)
            {
                clientListenerLoop = false;
                BravoWorkerFlag = false;
                messageInWorkerFlag = false;

                var keys = new List<int>(messageHandlerThreadEnabled.Keys);

                foreach (var key in keys)
                {
                    messageHandlerThreadEnabled[key] = false;
                }

                server.Stop();
                BravoSocket.Close();
            }
        }

        public static void Send(String msg)
        {
            StringBuilder str = new StringBuilder(msg).Append('\n'); // do this so the socket doesnt append a \r onto the message

            try
            {
                if (BravoSocket != null)
                {
                    Dictionary<int, TcpClient> wrkDict = new(ClientSocketDict);

                    foreach (var client in wrkDict)
                    {
                        try
                        {
                            NetworkStream outStream = client.Value.GetStream();
                            StreamWriter clientOut = new(outStream)
                            {
                                AutoFlush = true
                            };

                            clientOut.Write(str.ToString());
                            //Console.WriteLine($"GGHost Sending to client {str.ToString()}");

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Client {client.Key} No Longer Available, Removing...");
                            logger.Debug($"Client {client.Key} No Longer Available, Removing...");

                            if (ex.GetType() == typeof(IOException))
                            {
                                ClientSocketDict.Remove(client.Key);
                                messageHandlerThreadEnabled[client.Key] = false;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error Sending Message to GG Client: {e.Message}");
                logger.Debug($"Error Sending Message to GG Client: {e.Message}");
            }
        }
    }

    public class handleClient
    {
        TcpClient clientSocket;
        NetworkStream networkStream = null;

        int clNo;

        public void startClient(TcpClient inClientSocket, int clientNo)
        {
            clientSocket = inClientSocket;
            networkStream = clientSocket.GetStream();
            GGHost.ClientSocketDict.Add(clientNo, clientSocket);
            GGHost.messageHandlerThreadEnabled.Add(clientNo, true);
            clNo = clientNo;
            Thread ctThread = new(MessageHandler);
            ctThread.Start();
        }

        private void MessageHandler()
        {
            int readBufferLength = 65535;
            byte[] bytesFrom = new byte[readBufferLength];
            string dataFromClient = null;
            int bytesRead = 0;

            while (GGHost.messageHandlerThreadEnabled[clNo] == true)
            {
                try
                {
                    if (networkStream.DataAvailable)
                    {
                        bytesRead = networkStream.Read(bytesFrom, 0, readBufferLength);

                        if (bytesRead > 0)
                        {
                            dataFromClient = Encoding.ASCII.GetString(bytesFrom).TrimEnd('\0');
                            Console.WriteLine(" >> " + "From client-" + clNo + " " + dataFromClient);
                            Array.Clear(bytesFrom, 0, bytesFrom.Length);
                            GGHost.BravoQueue.Enqueue(dataFromClient);
                        }
                    }
                }
                catch (Exception ex)
                {
                    HGTableInterface.logger.Debug($"MessageHandler Error: {ex.Message}\n{ex.StackTrace}");
                    Console.WriteLine(" >> " + ex.ToString());
                }

                Thread.Sleep(10);
            }

            networkStream.Dispose();
            clientSocket.Close();
            Console.WriteLine($"Terminating MessageHandler Thread for Client {clNo}");
            HGTableInterface.logger.Debug($"Terminating MessageHandler Thread for Client {clNo}");
        }
    }
}
