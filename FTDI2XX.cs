using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic; // Install-Package Microsoft.VisualBasic
using Microsoft.VisualBasic.CompilerServices; // Install-Package Microsoft.VisualBasic
using log4net;
using System.Reflection;
using FTD2XX_NET;
using System.Threading;

namespace HGTranslator
{
    internal static partial class FTDI2XX
    {
        // ===========================================================================================================================

        public static byte[] BitCorrespondence = new byte[] { 1, 2, 4, 8, 16, 32, 64, 128 };
        public static byte[] AND_BitCorrespondence = new byte[] { 254, 0xFD, 0xFB, 0xF7, 0xEF, 0xDF, 0xBF, 0x7F };

        // ===========================================================================================================================
        // FTD2XX Return codes
        // ===========================================================================================================================
        public const int FT_OK = 0;
        public const int FT_INVALID_HANDLE = 1;
        public const int FT_DEVICE_NOT_FOUND = 2;
        public const int FT_DEVICE_NOT_OPENED = 3;
        public const int FT_IO_ERROR = 4;
        public const int FT_INSUFFICIENT_RESOURCES = 5;
        public const int FT_INVALID_PARAMETER = 6;
        public const int FT_INVALID_BAUD_RATE = 7;
        public const int FT_DEVICE_NOT_OPENED_FOR_ERASE = 8;
        public const int FT_DEVICE_NOT_OPENED_FOR_WRITE = 9;
        public const int FT_FAILED_TO_WRITE_DEVICE = 10;
        public const int FT_EEPROM_READ_FAILED = 11;
        public const int FT_EEPROM_WRITE_FAILED = 12;
        public const int FT_EEPROM_ERASE_FAILED = 13;
        public const int FT_EEPROM_NOT_PRESENT = 14;
        public const int FT_EEPROM_NOT_PROGRAMMED = 15;
        public const int FT_INVALID_ARGS = 16;
        public const int FT_OTHER_ERROR = 17;

        // ===========================================================================================================================
        // FTD2XX Flags - These are only used in this module
        // ===========================================================================================================================
        // FT_OpenEx Flags (See FT_OpenEx)
        public const int FT_OPEN_BY_SERIAL_NUMBER = 1;
        public const int FT_OPEN_BY_DESCRIPTION = 2;

        // FT_ListDevices Flags (See FT_ListDevices)
        public const int FT_LIST_NUMBER_ONLY = int.MinValue + 0x00000000;
        public const int FT_LIST_BY_INDEX = 0x40000000;
        public const int FT_LIST_ALL = 0x20000000;

        // ===========================================================================================================================
        // FTD2XX Buffer Constants - These are only used in this module
        // ===========================================================================================================================
        private const int FT_In_Buffer_Size = 0x100000;                  // 1024K
        private const int FT_In_Buffer_Index = FT_In_Buffer_Size - 1;
        private const int FT_Out_Buffer_Size = 0x10000;                  // 64K
        private const int FT_Out_Buffer_Index = FT_Out_Buffer_Size - 1;

        // ===========================================================================================================================
        // FTD2XX Constants
        // ===========================================================================================================================
        // FT Standard Baud Rates (See FT_SetBaudrate)
        public const int FT_BAUD_300 = 300;
        public const int FT_BAUD_600 = 600;
        public const int FT_BAUD_1200 = 1200;
        public const int FT_BAUD_2400 = 2400;
        public const int FT_BAUD_4800 = 4800;
        public const int FT_BAUD_9600 = 9600;
        public const int FT_BAUD_14400 = 14400;
        public const int FT_BAUD_19200 = 19200;
        public const int FT_BAUD_38400 = 38400;
        public const int FT_BAUD_57600 = 57600;
        public const int FT_BAUD_115200 = 115200;
        public const int FT_BAUD_230400 = 230400;
        public const int FT_BAUD_460800 = 460800;
        public const int FT_BAUD_921600 = 921600;

        // FT Data Bits (See FT_SetDataCharacteristics)
        public const int FT_DATA_BITS_7 = 7;
        public const int FT_DATA_BITS_8 = 8;

        // FT Stop Bits (See FT_SetDataCharacteristics)
        public const int FT_STOP_BITS_1 = 0;
        public const int FT_STOP_BITS_2 = 2;

        // FT Parity (See FT_SetDataCharacteristics)
        public const int FT_PARITY_NONE = 0;
        public const int FT_PARITY_ODD = 1;
        public const int FT_PARITY_EVEN = 2;
        public const int FT_PARITY_MARK = 3;
        public const int FT_PARITY_SPACE = 4;

        // FT Flow Control (See FT_SetFlowControl)
        public const int FT_FLOW_NONE = 0x0;
        public const int FT_FLOW_RTS_CTS = 0x100;
        public const int FT_FLOW_DTR_DSR = 0x200;
        public const int FT_FLOW_XON_XOFF = 0x400;

        // Modem Status
        public const int FT_MODEM_STATUS_CTS = 0x10;
        public const int FT_MODEM_STATUS_DSR = 0x20;
        public const int FT_MODEM_STATUS_RI = 0x40;
        public const int FT_MODEM_STATUS_DCD = 0x80;

        // FT Purge Commands (See FT_Purge)
        public const int FT_PURGE_RX = 1;
        public const int FT_PURGE_TX = 2;

        // FT Bit Mode (See FT_SetBitMode)
        public const int FT_RESET_BITMODE = 0x00;
        public const int FT_ASYNCHRONOUS_BIT_BANG = 0x01;
        public const int FT_MPSSE = 0x2;
        public const int FT_SYNCHRONOUS_BIT_BANG = 0x04;
        public const int FT_MCU_HOST = 0x08;
        public const int FT_OPTO_ISOLATE = 0x10;

        // FT Notification Event Masks (See FT_SetEventNotification)
        public const int FT_EVENT_RXCHAR = 1;
        public const int FT_EVENT_MODEM_STATUS = 2;
        public const int WAIT_ABANDONED = 0x80;
        // Public Const WAIT_FAILED As Integer = &HFFFFFFFF
        public const int WAIT_OBJECT_0 = 0x0;
        public const int WAIT_TIMEOUT = 0x102;

        public const int INFINITE = int.MinValue + 0x7FFFFFFF;


        // ===========================================================================================================================
        // Type definition for EEPROM as equivalent for C-structure "FT_PROGRAM_DATA"
        // ===========================================================================================================================

        // Define string as Integer and use FT_EE_ReadEx and FT_EE_ProgramEx functions

        public partial struct FT_PROGRAM_DATA
        {
            public int Signature1;                  // // Header - must be 0x00000000
            public int Signature2;                  // // Header - must be 0xFFFFFFFF
            public int Version;                     // // 0 = original, 1 = FT2232C extensions
            public short VendorID;                 // // 0x0403
            public short ProductID;                // // 0x6001
            public int Manufacturer;                // // "FTDI" (32 bytes allocated)
            public int ManufacturerID;              // // "FT" (16 bytes allocated)
            public int Description;                 // // "USB HS Serial Converter" (64 bytes allocated)
            public int SerialNumber;                // // "FT000001" if fixed, or NULL (16 bytes allocated)
            public short MaxPower;                 // // 0 < MaxPower <= 500
            public short PnP;                      // // 0 = disabled, 1 = enabled
            public short SelfPowered;              // // 0 = bus powered, 1 = self powered
            public short RemoteWakeup;             // // 0 = not capable, 1 = capable
                                                   // Rev4 extensions:
            public byte Rev4;                        // // true if Rev4 chip, false otherwise
            public byte IsoIn;                       // // true if in endpoint is isochronous
            public byte IsoOut;                      // // true if out endpoint is isochronous
            public byte PullDownEnable;              // // true if pull down enabled
            public byte SerNumEnable;                // // true if serial number to be used
            public byte USBVersionEnable;            // // true if chip uses USBVersion
            public short USBVersion;               // // BCD (0x0200 => USB2)
                                                   // FT2232C extensions:
            public byte Rev5;                        // // non-zero if Rev5 chip, zero otherwise
            public byte IsoInA;                      // // non-zero if in endpoint is isochronous
            public byte IsoInB;                      // // non-zero if in endpoint is isochronous
            public byte IsoOutA;                     // // non-zero if out endpoint is isochronous
            public byte IsoOutB;                     // // non-zero if out endpoint is isochronous
            public byte PullDownEnable5;             // // non-zero if pull down enabled
            public byte SerNumEnable5;               // // non-zero if serial number to be used
            public byte USBVersionEnable5;           // // non-zero if chip uses USBVersion
            public short USBVersion5;              // // BCD (0x0200 => USB2)
            public byte AIsHighCurrent;              // // non-zero if interface is high current
            public byte BIsHighCurrent;              // // non-zero if interface is high current
            public byte IFAIsFifo;                   // // non-zero if interface is 245 FIFO
            public byte IFAIsFifoTar;                // // non-zero if interface is 245 FIFO CPU target
            public byte IFAIsFastSer;                // // non-zero if interface is Fast serial
            public byte AIsVCP;                      // // non-zero if interface is to use VCP drivers
            public byte IFBIsFifo;                   // // non-zero if interface is 245 FIFO
            public byte IFBIsFifoTar;                // // non-zero if interface is 245 FIFO CPU target
            public byte IFBIsFastSer;                // // non-zero if interface is Fast serial
            public byte BIsVCP;                      // // non-zero if interface is to use VCP drivers

            // ''''''''''''''''''''''''FT4232H EEPROM
            public byte ASlowSlew; // non-zero if A pins have slow slew
            public byte ASchmittInput; // // non-zero if A pins are Schmitt input
            public byte ADriveCurrent; // // valid values are 4mA, 8mA, 12mA, 16mA
            public byte BSlowSlew; // // non-zero if B pins have slow slew
            public byte BSchmittInput; // // non-zero if B pins are Schmitt input
            public byte BDriveCurrent; // // valid values are 4mA, 8mA, 12mA, 16mA
            public byte CSlowSlew; // // non-zero if C pins have slow slew
            public byte CSchmittInput; // // non-zero if C pins are Schmitt input
            public byte CDriveCurrent; // // valid values are 4mA, 8mA, 12mA, 16mA
            public byte DSlowSlew; // // non-zero if D pins have slow slew
            public byte DSchmittInput; // // non-zero if D pins are Schmitt input
            public byte DDriveCurrent; // // valid values are 4mA, 8mA, 12mA, 16mA
                                       // // Hardware options
            public byte ARIIsTXDEN; // // non-zero if port A uses RI as RS485 TXDEN
            public byte BRIIsTXDEN; // // non-zero if port B uses RI as RS485 TXDEN
            public byte CRIIsTXDEN; // // non-zero if port C uses RI as RS485 TXDEN
            public byte DRIIsTXDEN; // // non-zero if port D uses RI as RS485 TXDEN
                                    // // Driver option
            public byte ADriverType; // //
            public byte BDriverType; // //
            public byte CDriverType; // //
            public byte DDriverType; // //
        }

        // ===========================================================================================================================
        // Public declarations of variables for external modules to access from FTD2XX.dll
        // ===========================================================================================================================
        public static int FT_Status;
        public static UInt32 FT_Device_Count;
        public static string FT_Serial_Number;
        public static string FT_Description;
        public static int FT_Handle;
        public static string FT_Type;
        public static string FT_VID_PID;
        public static int FT_Current_Baud;
        public static byte FT_Current_DataBits;
        public static byte FT_Current_StopBits;
        public static byte FT_Current_Parity;
        public static int FT_Current_FlowControl;
        public static byte FT_Current_XOn_Char;
        public static byte FT_Current_XOff_Char;
        public static int FT_ModemStatus;
        public static int FT_RxQ_Bytes;
        public static int FT_TxQ_Bytes;
        public static int FT_EventStatus;
        public static bool FT_Event_On;
        public static bool FT_Error_On;
        public static byte FT_Event_Value;
        public static byte FT_Error_Value;
        public static byte[] FT_In_Buffer = new byte[1048576];
        public static byte[] FT_Out_Buffer = new byte[65536];
        public static byte FT_Latency;
        public static FT_PROGRAM_DATA FT_EEPROM_DataBuffer;
        public static string FT_EEPROM_Manufacturer;
        public static string FT_EEPROM_ManufacturerID;
        public static string FT_EEPROM_Description;
        public static string FT_EEPROM_SerialNumber;
        public static int FT_UA_Size;
        public static byte[] FT_UA_Data;     // NOTE: when using Read_EEPROM_UA and Write_EEPROM_UA get size of user area first,
                                             // then use the command ReDim FT_UA_Data(0 to FT_UA_Size-1) As Byte

        public static byte[] ReceiveBuffer = new byte[256];
        public static int BytesRead;
        public static readonly ILog logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public static FTDI myFtdiDevice = new FTDI();
        public static FTDI.FT_STATUS ftStatus = FTDI.FT_STATUS.FT_OK;
        public static FTDI.FT_DEVICE_INFO_NODE[] ftdiDeviceList;


        public static byte portState = 0;
        public static byte[] inData;

        public partial struct FT_RTN // FT return for read
        {
            public int status;
            public byte portvalue;
        }

        public enum PORTS
        {
            A,
            B,
            C,
            D
        }

        public static IntPtr HandleA = IntPtr.Zero;
        public static IntPtr HandleB = IntPtr.Zero;
        public static IntPtr HandleC = IntPtr.Zero;
        public static IntPtr HandleD = IntPtr.Zero;
        public static IntPtr[] handleArray = new IntPtr[] { HandleA, HandleB, HandleC, HandleD };

        [DllImportAttribute("ftd2xx.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern FTDI.FT_STATUS FT_OpenEx(string pvArg1, int dwFlags, ref IntPtr ftHandle);

        [DllImportAttribute("ftd2xx.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern FTDI.FT_STATUS FT_Close(IntPtr ftHandle);

        [DllImportAttribute("ftd2xx.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern FTDI.FT_STATUS FT_SetBaudRate(IntPtr ftHandle, int dwBaudRate);

        [DllImportAttribute("ftd2xx.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern FTDI.FT_STATUS FT_SetDataCharacteristics(IntPtr ftHandle, byte uWordLength, byte uStopBits, byte uParity);

        [DllImportAttribute("ftd2xx.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern FTDI.FT_STATUS FT_SetFlowControl(IntPtr ftHandle, ushort usFlowControl, byte uXon, byte uXoff);

        [DllImportAttribute("ftd2xx.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern FTDI.FT_STATUS FT_SetTimeouts(IntPtr ftHandle, int dwReadTimeout, int dwWriteTimeout);

        [DllImportAttribute("ftd2xx.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern FTDI.FT_STATUS FT_GetQueueStatus(IntPtr ftHandle, ref int lpdwAmountInRxQueue);

        [DllImportAttribute("ftd2xx.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern FTDI.FT_STATUS FT_Purge(IntPtr ftHandle, byte uEventCh);

        [DllImportAttribute("ftd2xx.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern FTDI.FT_STATUS FT_Read(IntPtr ftHandle, byte[] lpBuffer, int dwBytesToRead, ref int lpdwBytesReturned);

        [DllImportAttribute("ftd2xx.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern FTDI.FT_STATUS FT_Write(IntPtr ftHandle, byte[] lpBuffer, int dwBytesToWrite, ref int lpdwBytesWritten);

        [DllImportAttribute("ftd2xx.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern FTDI.FT_STATUS FT_SetDtr(IntPtr ftHandle);

        [DllImportAttribute("ftd2xx.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern FTDI.FT_STATUS FT_ClrDtr(IntPtr ftHandle);

        [DllImportAttribute("ftd2xx.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern FTDI.FT_STATUS FT_SetRts(IntPtr ftHandle);

        [DllImportAttribute("ftd2xx.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern FTDI.FT_STATUS FT_ClrRts(IntPtr ftHandle);

        [DllImportAttribute("ftd2xx.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern FTDI.FT_STATUS FT_GetModemStatus(IntPtr ftHandle, ref byte ModemStatus);

        [DllImportAttribute("ftd2xx.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern FTDI.FT_STATUS FT_SetBitMode(IntPtr ftHandle, byte ucMask, byte ucMode);

        [DllImportAttribute("ftd2xx.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern FTDI.FT_STATUS FT_GetBitMode(IntPtr ftHandle, ref byte pucMode);

        [DllImportAttribute("ftd2xx.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern FTDI.FT_STATUS FT_ResetDevice(IntPtr ftHandle);

        // ===========================================================================================================================
        // 
        // Wrapper FTD2XX.DLL function calls
        // 
        // ===========================================================================================================================

        public static bool Init4232()
        {

            // Purpose: Init the 4232 4 port FT part

            // Inputs: none

            // Outputs: pass/ fail t/f

            // Assumptions: for this version 

            // Port A shall be configured as follows (AD0-AD7)
            // Output AD0 =   SEAT_PWR_INH_0 (optional TPP power control)
            // Output AD1 =   SEAT_PWR_INH_1 (seat 1 power)
            // Output AD2 =   SEAT_PWR_INH_2 (seat 2 power)
            // Output AD3 =   SEAT_PWR_INH_3 (seat 3 power)
            // Output AD4 =   SEAT_PWR_INH_4 (seat 4 power)
            // Output AD5 =   SEAT_PWR_INH_5 (seat 5 power)
            // Output AD6 =   SEAT_PWR_INH_6 (seat 6 power)
            // Output AD7 =   SEAT_PWR_INH_7 (seat 7 power)

            // Port B shall be configured as follows (BD0-BD7)
            // Output BD0 =   SEAT_PWR_INH_8 (seat 8 power)
            // Output BD1 =   SEAT_PWR_INH_9 (seat 9 power)
            // Output BD2 =   DLR_PWR_INH (Tray power)
            // Input BD3 =   GPIO_FTDI_0 (Main Lock Box Switch)
            // Input BD4 =   GPIO_FTDI_1 (Jackpot Lock Box Switch)
            // Input BD5 =   GPIO_FTDI_2 (Dice Cup switch)
            // Input BD6 =   GPIO_FTDI_3 (LED Sw)
            // Input BD7=   GPIO_FTDI_4 (Tray position sense)

            // Port C shall be configured as follows (CD0-CD7)
            // Input CD0 =   SEAT_PGOOD_0 (power good for seat 0)
            // Input CD1 =   SEAT_PGOOD_1 (power good for seat 1)
            // Input CD0 =   SEAT_PGOOD_2 (power good for seat 2)
            // Input CD3 =   SEAT_PGOOD_3 (power good for seat 3)
            // Input CD4 =   SEAT_PGOOD_4 (power good for seat 4)
            // Input CD5 =   SEAT_PGOOD_5 (power good for seat 5)
            // Input CD6 =   SEAT_PGOOD_6 (power good for seat 6)
            // Input CD7 =   SEAT_PGOOD_7 (power good for seat 7)

            // Port D shall be configured as follows (DD0-DD7)
            // Input DD0 =   SEAT_PGOOD_8 (power good for seat 8)
            // Input DD1 =   SEAT_PGOOD_9 (power good for seat 9)
            // Input DD2 =   DLR_PGOOD (power good dealer)
            // Input DD3 =   undefined
            // Output DD4 =   undefined
            // Output DD5 =   undefined
            // Output DD6 =   undefined
            // Output DD7 =   SEAT_PWR_INH_0

            // FT Chip must have been previously configured as "FT-GPIO" for its device description

            // Comments: mask values can be bit assigned where a 1 = output and 0 = input
            // There can be more than 4 FTDI devices (drop slot and possibly others) so we need to track
            // the actual index number correspondence for the 4232.

            byte mask_all_outputs = 0xFF;
            byte mask_all_inputs = 0;
            UInt32 devcount = 0;
            string portStr = "";
            int index;
            bool failedInit = false;

            try
            {
                Console.WriteLine("Initializing FT4232....");

                // call to get the number of devices - will update variable: FT_Device_Count
                if (GetFTDeviceCount() == -1)
                {
                    Console.WriteLine("No Devices Found");
                    logger.Debug("No Devices Found");
                    return false;
                }

                // keep it local 
                devcount = FT_Device_Count;

                // check for expected
                if (devcount < 4)
                {
                    Console.WriteLine($"Expecting 4 or more devices - Found {devcount}");
                    logger.Error($"Expecting 4 or more devices - Found {devcount}");
                    return false;
                }

                // Allocate storage for device info list
                ftdiDeviceList = new FTDI.FT_DEVICE_INFO_NODE[devcount];

                // Populate our device list
                ftStatus = myFtdiDevice.GetDeviceList(ftdiDeviceList);

                if (ftStatus != FTDI.FT_STATUS.FT_OK)
                {
                    // Wait for a key press
                    Console.WriteLine("Failed to Get Device List: (error " + ftStatus.ToString() + ")");
                    logger.Error("Failed to Get Device List: (error " + ftStatus.ToString() + ")");
                    return false;
                }
                else
                {
                    Console.WriteLine("");
                    logger.Debug("");

                    for (index = 0; index < devcount; index++)
                    {
                        if (FT_OpenEx(ftdiDeviceList[index].SerialNumber, FT_OPEN_BY_SERIAL_NUMBER, ref handleArray[index]) != FTDI.FT_STATUS.FT_OK)
                        {
                            Console.WriteLine($"Failed to Open Device #{index}, Serial Number {ftdiDeviceList[index].SerialNumber}, Error {ftStatus.ToString()}");
                            logger.Debug($"Failed to Open Device #{index}, Serial Number {ftdiDeviceList[index].SerialNumber}, Error {ftStatus.ToString()}");
                            failedInit = true;
                            break;
                        }

                        if (!UARTConfig(handleArray[index], FT_BAUD_921600))
                        {
                            Console.WriteLine($"Failed to Set Baud Rate for device #{index}, Serial Number {ftdiDeviceList[index].SerialNumber}, Error {ftStatus.ToString()}");
                            logger.Debug($"Failed to Set Baud Rate for device #{index}, Serial Number {ftdiDeviceList[index].SerialNumber}, Error {ftStatus.ToString()}");
                            failedInit = true;
                            break;
                        }

                        Console.WriteLine("Device Index: " + index.ToString());
                        Console.WriteLine("Device Port: " + Enum.GetName(typeof(PORTS), index));
                        Console.WriteLine("Flags: " + String.Format("{0:x}", ftdiDeviceList[index].Flags));
                        Console.WriteLine("Type: " + ftdiDeviceList[index].Type.ToString());
                        Console.WriteLine("ID: " + String.Format("{0:x}", ftdiDeviceList[index].ID));
                        Console.WriteLine("Location ID: " + String.Format("{0:x}", ftdiDeviceList[index].LocId));
                        Console.WriteLine("Serial Number: " + ftdiDeviceList[index].SerialNumber.ToString());
                        Console.WriteLine("Description: " + ftdiDeviceList[index].Description.ToString());
                        Console.WriteLine($"Handle: {handleArray[index]:X}");
                        logger.Debug("Device Index: " + index.ToString());
                        logger.Debug("Device Port: " + Enum.GetName(typeof(PORTS), index));
                        logger.Debug("Flags: " + String.Format("{0:x}", ftdiDeviceList[index].Flags));
                        logger.Debug("Type: " + ftdiDeviceList[index].Type.ToString());
                        logger.Debug("ID: " + String.Format("{0:x}", ftdiDeviceList[index].ID));
                        logger.Debug("Location ID: " + String.Format("{0:x}", ftdiDeviceList[index].LocId));
                        logger.Debug("Serial Number: " + ftdiDeviceList[index].SerialNumber.ToString());
                        logger.Debug("Description: " + ftdiDeviceList[index].Description.ToString());
                        logger.Debug($"Handle: {handleArray[index]:X}");

                        switch (index)
                        {
                            case 0:

                                // Port A - All Outputs
                                if (FT_SetBitMode(handleArray[index], mask_all_outputs, FT_SYNCHRONOUS_BIT_BANG) != FTDI.FT_STATUS.FT_OK)
                                {
                                    Console.WriteLine($"Failed to Set Bit Bang Mode for device #{index}, Serial Number {ftdiDeviceList[index].SerialNumber}, Error {ftStatus.ToString()}");
                                    logger.Debug($"Failed to Set Bit Bang Mode for device #{index}, Serial Number {ftdiDeviceList[index].SerialNumber}, Error {ftStatus.ToString()}");
                                    failedInit = true;
                                }

                                break;

                            case 1:
                                // Port B - First 3 Bits Ouputs, Last 5 Bits Inputs
                                if (FT_SetBitMode(handleArray[index], 0x07, FT_SYNCHRONOUS_BIT_BANG) != FTDI.FT_STATUS.FT_OK)
                                {
                                    Console.WriteLine($"Failed to Set Bit Mode for device #{index}, Serial Number {ftdiDeviceList[index].SerialNumber}, Error {ftStatus.ToString()}");
                                    logger.Debug($"Failed to Set Bit Mode for device #{index}, Serial Number {ftdiDeviceList[index].SerialNumber}, Error {ftStatus.ToString()}");
                                    failedInit = true;
                                }

                                break;

                            case 2:
                                // Port C - All Inputs
                                if (FT_SetBitMode(handleArray[index], mask_all_inputs, FT_ASYNCHRONOUS_BIT_BANG) != FTDI.FT_STATUS.FT_OK)
                                {
                                    Console.WriteLine($"Failed to Set Bit Mode for device #{index}, Serial Number {ftdiDeviceList[index].SerialNumber}, Error {ftStatus.ToString()}");
                                    logger.Debug($"Failed to Set Bit Mode for device #{index}, Serial Number {ftdiDeviceList[index].SerialNumber}, Error {ftStatus.ToString()}");
                                    failedInit = true;
                                }

                                break;


                            case 3:
                                // Port D - First 4 Bits Ouputs, Last 4 Bits Inputs
                                if (FT_SetBitMode(handleArray[index], 0xF0, FT_SYNCHRONOUS_BIT_BANG) != FTDI.FT_STATUS.FT_OK)
                                {
                                    Console.WriteLine($"Failed to Set Bit Mode for device #{index}, Serial Number {ftdiDeviceList[index].SerialNumber}, Error {ftStatus.ToString()}");
                                    logger.Debug($"Failed to Set Bit Mode for device #{index}, Serial Number {ftdiDeviceList[index].SerialNumber}, Error {ftStatus.ToString()}");
                                    failedInit = true;
                                }

                                break;

                            default:
                                break;
                        }

                        if (index != 2)
                        {
                            if (WriteToRelayBoard(index, 0, false) == false)
                            {
                                Console.WriteLine($"Failed to Write Data to device #{index}, Serial Number {ftdiDeviceList[index].SerialNumber}, Error {ftStatus.ToString()}");
                                logger.Debug($"Failed to  Write Data to device #{index}, Serial Number {ftdiDeviceList[index].SerialNumber}, Error {ftStatus.ToString()}");
                                failedInit = true;
                            }
                        }

                        if (!GetPortState(index, false))
                            failedInit = true;

                        if (failedInit)
                            break;

                        Console.WriteLine($"Port Value = {portState:X2}");
                        logger.Debug($"Port Value = {portState:X2}");

                        Console.WriteLine("");
                        logger.Debug("");
                    }
                }

                if (index < devcount)
                {
                    Console.WriteLine($"Init4232: Failed to Initalize All Devices, Error on Device {index}");
                    logger.Error($"Init4232: Failed to Initalize All Devices, Error on Device {index}");
                    return false;
                }

                // all must be good if we get here
                return true;
            }

            catch (Exception ex)
            {
                logger.Debug("Init exception: " + ex.Message + "\n" + ex.StackTrace);
                return false;
            }


        }

        public static bool WriteToRelayBoard(int device, byte datatowrite, bool openDevice)
        {
            // Purpose: write byte to selected port 1,2,4 on the 4232

            // Inputs: board number 1,2,4(actually port number) and the data to be written to the specified port

            // Outputs: pass/ fail t/f

            // Assumptions:

            // Comments:

            byte[] buff = new byte[datatowrite];

            // specify lenght of data to be passed in the array
            int Write_Count = 1;
            var Write_Result = default(int);

            // write the data buffer to the FT device 
            if (FT_Write(handleArray[device], buff, Write_Count, ref Write_Result) != FTDI.FT_STATUS.FT_OK)
            {
                Console.WriteLine($"WriteToRelayBoard Error: Unable to Write to Device {device}.  Error = {ftStatus}");
                logger.Error($"WriteToRelayBoard Error: Unable to Write to Device {device}.  Error = {ftStatus}");
                return false;
            }
            else
            {
                return true;
            }
        }

        public static bool ReadRelayBoard(int device, bool openDevice)
        {
            inData = new byte[1];

            // specify lenght of data to be passed in the array
            int Read_Count = 1;
            var Read_Result = default(int);

            if (FT_Read(handleArray[device], inData, Read_Count, ref Read_Result) != FTDI.FT_STATUS.FT_OK)
            {
                Console.WriteLine($"ReadRelayBoard Error: Unable to Write to Device {device}.  Error = {ftStatus}");
                logger.Error($"ReadRelayBoard Error: Unable to Write to Device {device}.  Error = {ftStatus}");
                return false;
            }
            else
            {
                return true;
            }
        }

        public static bool ResetDevice(int device, bool openDevice)
        {
            if (FT_ResetDevice(handleArray[device]) != FTDI.FT_STATUS.FT_OK)
            {
                Console.WriteLine($"ResetDevice Error: Unable to Reset Device {device}.  Error = {ftStatus}");
                logger.Error($"ResetDevice Error: Unable to Reset Device {device}.  Error = {ftStatus}");
                return false;
            }
            else
            {
                return true;
            }
        }

        public static int GetFTDeviceCount()
        {
            FT_Status = (int)myFtdiDevice.GetNumberOfDevices(ref FT_Device_Count);

            if (FT_Status != FT_OK)
            {
                Console.WriteLine($"GetFTDeviceCount Error: Error = {ftStatus}");
                logger.Error($"GetFTDeviceCount Error: Error = {ftStatus}");
                return -1;
            }
            else
            {
                return FT_OK;
            }
        }

        public static string GetSerialNumber(int device)
        {
            if (device < ftdiDeviceList.Length)
                return ftdiDeviceList[device].SerialNumber.ToString();
            else
            {

                Console.WriteLine($"GetSerialNumber Error: Invalid Device {device}.  Error = {ftStatus}");
                logger.Error($"GetSerialNumber Error: Invalid Device {device}.  Error = {ftStatus}");
                return string.Empty;
            }
        }

        public static string GetDeviceDescription(int device)
        {
            if (device < ftdiDeviceList.Length)
                return ftdiDeviceList[device].Description.ToString();
            else
            {

                Console.WriteLine($"GetDeviceDescription Error: Invalid Device {device}.  Error = {ftStatus}");
                logger.Error($"GetDeviceDescription Error: Invalid Device {device}.  Error = {ftStatus}");
                return string.Empty;
            }
        }

        public static string GetDeviceVIDPID(int device)
        {
            if (device < ftdiDeviceList.Length)
                return ftdiDeviceList[device].ID.ToString();
            else
            {

                Console.WriteLine($"GetDevicePID Error: Invalid Device {device}.  Error = {ftStatus}");
                logger.Error($"GetDevicePID Error: Invalid Device {device}.  Error = {ftStatus}");
                return string.Empty;
            }
        }

        public static bool GetPortState(int device, bool openDevice)
        {
            byte mode = 0;

            if (FT_GetBitMode(handleArray[device], ref mode) != FTDI.FT_STATUS.FT_OK)
            {
                Console.WriteLine($"GetPortState Error: Unable to Read Pin States for Device {device}.  Error = {ftStatus}");
                logger.Error($"GetPortState Error: Unable to Read Pin States for Device {device}.  Error = {ftStatus}");
                return false;
            }
            else
            {
                portState = mode;
                return true;
            }
        }

        private static bool UARTConfig(IntPtr ftHandle, int baudrate)
        {
            bool _result = false;

            if (FT_SetBaudRate(ftHandle, baudrate) == FTDI.FT_STATUS.FT_OK)
                if (FT_SetDataCharacteristics(ftHandle, 8, FT_STOP_BITS_1, FT_PARITY_NONE) == FTDI.FT_STATUS.FT_OK)
                    if (FT_SetFlowControl(ftHandle, FT_FLOW_RTS_CTS, 0x11, 0x13) == FTDI.FT_STATUS.FT_OK)
                        if (FT_SetTimeouts(ftHandle, 500, 500) == FTDI.FT_STATUS.FT_OK)
                            _result = true;
            return _result;
        }

        public static bool SetDeviceDTR(int device, bool enable)
        {
            if (device < ftdiDeviceList.Length)
            {
                if (FT_SetDtr(handleArray[device]) != FTDI.FT_STATUS.FT_OK)
                {
                    Console.WriteLine($"SetDeviceDTR Error: Unable to Set DTR for Device {device}.  Error = {ftStatus}");
                    logger.Error($"SetDeviceDTR Error: Unable to Set DTR for Device {device}.  Error = {ftStatus}");
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                Console.WriteLine($"SetDeviceDTR Error: Invalid Device {device}.  Error = {ftStatus}");
                logger.Error($"SetDeviceDTR Error: Invalid Device {device}.  Error = {ftStatus}");
                return false;
            }
        }

        public static bool ClearDeviceDTR(int device, bool enable)
        {
            if (device < ftdiDeviceList.Length)
            {
                if (FT_ClrDtr(handleArray[device]) != FTDI.FT_STATUS.FT_OK)
                {
                    Console.WriteLine($"ClearDeviceDTR Error: Unable to Set DTR for Device {device}.  Error = {ftStatus}");
                    logger.Error($"ClearDeviceDTR Error: Unable to Set DTR for Device {device}.  Error = {ftStatus}");
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                Console.WriteLine($"ClearDeviceDTR Error: Invalid Device {device}.  Error = {ftStatus}");
                logger.Error($"ClearDeviceDTR Error: Invalid Device {device}.  Error = {ftStatus}");
                return false;
            }
        }

        public static bool SetDeviceRTS(int device, bool enable)
        {
            if (device < ftdiDeviceList.Length)
            {
                if (FT_SetRts(handleArray[device]) != FTDI.FT_STATUS.FT_OK)
                {
                    Console.WriteLine($"SetDeviceRTS Error: Unable to Set RTS for Device {device}.  Error = {ftStatus}");
                    logger.Error($"SetDeviceRTS Error: Unable to Set RTS for Device {device}.  Error = {ftStatus}");
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                Console.WriteLine($"SetDeviceRTS Error: Invalid Device {device}.  Error = {ftStatus}");
                logger.Error($"SetDeviceRTS Error: Invalid Device {device}.  Error = {ftStatus}");
                return false;
            }
        }

        public static bool ClearDeviceRTS(int device, bool enable)
        {
            if (device < ftdiDeviceList.Length)
            {
                if (FT_ClrRts(handleArray[device]) != FTDI.FT_STATUS.FT_OK)
                {
                    Console.WriteLine($"ClearDeviceRTS Error: Unable to Set RTS for Device {device}.  Error = {ftStatus}");
                    logger.Error($"ClearDeviceRTS Error: Unable to Set RTS for Device {device}.  Error = {ftStatus}");
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                Console.WriteLine($"ClearDeviceRTS Error: Invalid Device {device}.  Error = {ftStatus}");
                logger.Error($"ClearDeviceRTS Error: Invalid Device {device}.  Error = {ftStatus}");
                return false;
            }
        }

        public static int Close_USB_Device(int device)
        {
            if (FT_Close(handleArray[device]) != FT_OK)
            {
                Console.WriteLine($"Close_USB_Device Error");
                logger.Error($"Close_USB_Device Error");
                return -1;
            }
            else
            {
                return FT_OK;
            }
        }

#if false

        public static int Get_USB_Device_Queue_Status()
        {

            FT_Status = FT_GetQueueStatus(FT_Handle, FT_RxQ_Bytes);
            if (FT_Status != FT_OK)
            {
                return -1;
            }
            else
            {
                return FT_OK;
            }

        }

        public static int Read_Data_String(string StringData)
        {
            var Read_Result = default(int);
            string TempStringData;

            TempStringData = Strings.Space(FT_RxQ_Bytes + 1);

            FT_Status = FTDI2XX.FT_Read_String(FT_Handle, TempStringData, FT_RxQ_Bytes, Read_Result);
            if (FT_Status != FT_OK)
            {
                return -1;
            }
            else
            {
                return FT_OK;
            }

            StringData = Strings.Trim(TempStringData);

        }

        public static int Read_Data_Bytes(int Read_Count)
        {
            // Reads Read_Count Bytes (or less) from the USB device to the FT_In_Buffer
            // Function returns the number of bytes actually received  which may range from zero
            // to the actual number of bytes requested, depending on how many have been received
            // at the time of the request + the read timeout value.Dim Read_Result As Integer
            var Read_Result = default(int);

            FT_Status = FT_Read_Bytes(FT_Handle, FT_In_Buffer[0], Read_Count, Read_Result);
            if (FT_Status != FT_OK)
            {
                return -1;
            }
            else
            {
                return FT_OK;
            }

        }


        public static int Get_USB_Device_Status()
        {

            FT_Status = FT_GetStatus(FT_Handle, FT_RxQ_Bytes, FT_TxQ_Bytes, FT_EventStatus);
            if (FT_Status != FT_OK)
            {
                return -1;
            }
            else
            {
                return FT_OK;
            }

        }

        #region FT_EEPEOM


        public static int Read_FT232_FT245_EEPROM()
        {
            string TempManufacturer;
            string TempManufacturerID;
            string TempDescription;
            string TempSerialNumber;

            // Create empty strings
            TempManufacturer = Strings.Space(32);
            TempManufacturerID = Strings.Space(16);
            TempDescription = Strings.Space(64);
            TempSerialNumber = Strings.Space(16);

            // Initialise structure
            FT_EEPROM_DataBuffer.Signature1 = 0x0;
            FT_EEPROM_DataBuffer.Signature2 = int.MinValue + 0x7FFFFFFF;
            FT_EEPROM_DataBuffer.Version = 0;
            FT_EEPROM_DataBuffer.VendorID = 0;
            FT_EEPROM_DataBuffer.ProductID = 0;
            FT_EEPROM_DataBuffer.Manufacturer = 0;
            FT_EEPROM_DataBuffer.ManufacturerID = 0;
            FT_EEPROM_DataBuffer.Description = 0;
            FT_EEPROM_DataBuffer.SerialNumber = 0;
            FT_EEPROM_DataBuffer.MaxPower = 0;
            FT_EEPROM_DataBuffer.PnP = 0;
            FT_EEPROM_DataBuffer.SelfPowered = 0;
            FT_EEPROM_DataBuffer.RemoteWakeup = 0;
            // Rev4 extensions:
            FT_EEPROM_DataBuffer.Rev4 = 0;
            FT_EEPROM_DataBuffer.IsoIn = 0;
            FT_EEPROM_DataBuffer.IsoOut = 0;
            FT_EEPROM_DataBuffer.PullDownEnable = 0;
            FT_EEPROM_DataBuffer.SerNumEnable = 0;
            FT_EEPROM_DataBuffer.USBVersionEnable = 0;
            FT_EEPROM_DataBuffer.USBVersion = 0;
            // FT2232C extensions:
            FT_EEPROM_DataBuffer.Rev5 = 0;
            FT_EEPROM_DataBuffer.IsoInA = 0;
            FT_EEPROM_DataBuffer.IsoInB = 0;
            FT_EEPROM_DataBuffer.IsoOutA = 0;
            FT_EEPROM_DataBuffer.IsoOutB = 0;
            FT_EEPROM_DataBuffer.PullDownEnable5 = 0;
            FT_EEPROM_DataBuffer.SerNumEnable5 = 0;
            FT_EEPROM_DataBuffer.USBVersionEnable5 = 0;
            FT_EEPROM_DataBuffer.USBVersion5 = 0;
            FT_EEPROM_DataBuffer.AIsHighCurrent = 0;
            FT_EEPROM_DataBuffer.BIsHighCurrent = 0;
            FT_EEPROM_DataBuffer.IFAIsFifo = 0;
            FT_EEPROM_DataBuffer.IFAIsFifoTar = 0;
            FT_EEPROM_DataBuffer.IFAIsFastSer = 0;
            FT_EEPROM_DataBuffer.AIsVCP = 0;
            FT_EEPROM_DataBuffer.IFBIsFifo = 0;
            FT_EEPROM_DataBuffer.IFBIsFifoTar = 0;
            FT_EEPROM_DataBuffer.IFBIsFastSer = 0;
            FT_EEPROM_DataBuffer.BIsVCP = 0;

            FT_Status = FTDI2XX.FT_EE_ReadEx(FT_Handle, FT_EEPROM_DataBuffer, TempManufacturer, TempManufacturerID, TempDescription, TempSerialNumber);
            if (FT_Status != FT_OK)
            {
                return -1;
            }
            else
            {
                FT_EEPROM_Manufacturer = Strings.Left(TempManufacturer, Strings.InStr(1, TempManufacturer, Constants.vbNullChar) - 1);
                FT_EEPROM_ManufacturerID = Strings.Left(TempManufacturerID, Strings.InStr(1, TempManufacturerID, Constants.vbNullChar) - 1);
                FT_EEPROM_Description = Strings.Left(TempDescription, Strings.InStr(1, TempDescription, Constants.vbNullChar) - 1);
                FT_EEPROM_SerialNumber = Strings.Left(TempSerialNumber, Strings.InStr(1, TempSerialNumber, Constants.vbNullChar) - 1);
                return FT_OK;
            }

        }

        public static int Read_FT2232C_EEPROM()
        {
            string TempManufacturer;
            string TempManufacturerID;
            string TempDescription;
            string TempSerialNumber;

            // Create empty strings
            TempManufacturer = Strings.Space(32);
            TempManufacturerID = Strings.Space(16);
            TempDescription = Strings.Space(64);
            TempSerialNumber = Strings.Space(16);

            // Initialise structure
            FT_EEPROM_DataBuffer.Signature1 = 0x0;
            FT_EEPROM_DataBuffer.Signature2 = int.MinValue + 0x7FFFFFFF;
            FT_EEPROM_DataBuffer.Version = 1;
            FT_EEPROM_DataBuffer.VendorID = 0;
            FT_EEPROM_DataBuffer.ProductID = 0;
            FT_EEPROM_DataBuffer.Manufacturer = 0;
            FT_EEPROM_DataBuffer.ManufacturerID = 0;
            FT_EEPROM_DataBuffer.Description = 0;
            FT_EEPROM_DataBuffer.SerialNumber = 0;
            FT_EEPROM_DataBuffer.MaxPower = 0;
            FT_EEPROM_DataBuffer.PnP = 0;
            FT_EEPROM_DataBuffer.SelfPowered = 0;
            FT_EEPROM_DataBuffer.RemoteWakeup = 0;
            // Rev4 extensions:
            FT_EEPROM_DataBuffer.Rev4 = 0;
            FT_EEPROM_DataBuffer.IsoIn = 0;
            FT_EEPROM_DataBuffer.IsoOut = 0;
            FT_EEPROM_DataBuffer.PullDownEnable = 0;
            FT_EEPROM_DataBuffer.SerNumEnable = 0;
            FT_EEPROM_DataBuffer.USBVersionEnable = 0;
            FT_EEPROM_DataBuffer.USBVersion = 0;
            // FT2232C extensions:
            FT_EEPROM_DataBuffer.Rev5 = 0;
            FT_EEPROM_DataBuffer.IsoInA = 0;
            FT_EEPROM_DataBuffer.IsoInB = 0;
            FT_EEPROM_DataBuffer.IsoOutA = 0;
            FT_EEPROM_DataBuffer.IsoOutB = 0;
            FT_EEPROM_DataBuffer.PullDownEnable5 = 0;
            FT_EEPROM_DataBuffer.SerNumEnable5 = 0;
            FT_EEPROM_DataBuffer.USBVersionEnable5 = 0;
            FT_EEPROM_DataBuffer.USBVersion5 = 0;
            FT_EEPROM_DataBuffer.AIsHighCurrent = 0;
            FT_EEPROM_DataBuffer.BIsHighCurrent = 0;
            FT_EEPROM_DataBuffer.IFAIsFifo = 0;
            FT_EEPROM_DataBuffer.IFAIsFifoTar = 0;
            FT_EEPROM_DataBuffer.IFAIsFastSer = 0;
            FT_EEPROM_DataBuffer.AIsVCP = 0;
            FT_EEPROM_DataBuffer.IFBIsFifo = 0;
            FT_EEPROM_DataBuffer.IFBIsFifoTar = 0;
            FT_EEPROM_DataBuffer.IFBIsFastSer = 0;
            FT_EEPROM_DataBuffer.BIsVCP = 0;

            FT_Status = FTDI2XX.FT_EE_ReadEx(FT_Handle, FT_EEPROM_DataBuffer, TempManufacturer, TempManufacturerID, TempDescription, TempSerialNumber);
            if (FT_Status != FT_OK)
            {
                return -1;
            }
            else
            {
                FT_EEPROM_Manufacturer = Strings.Left(TempManufacturer, Strings.InStr(1, TempManufacturer, Constants.vbNullChar) - 1);
                FT_EEPROM_ManufacturerID = Strings.Left(TempManufacturerID, Strings.InStr(1, TempManufacturerID, Constants.vbNullChar) - 1);
                FT_EEPROM_Description = Strings.Left(TempDescription, Strings.InStr(1, TempDescription, Constants.vbNullChar) - 1);
                FT_EEPROM_SerialNumber = Strings.Left(TempSerialNumber, Strings.InStr(1, TempSerialNumber, Constants.vbNullChar) - 1);
                return FT_OK;
            }

        }

        public static int Program_FT232_FT245_EEPROM(FT_PROGRAM_DATA FT_EEPROM_DataBuffer, string FT_EEPROM_Manufacturer, string FT_EEPROM_ManufacturerID, string FT_EEPROM_Description, string FT_EEPROM_SerialNumber)
        {

            FT_EEPROM_DataBuffer.Signature1 = 0x0;
            FT_EEPROM_DataBuffer.Signature2 = int.MinValue + 0x7FFFFFFF;
            FT_EEPROM_DataBuffer.Version = 0;

            FT_Status = FTDI2XX.FT_EE_ProgramEx(FT_Handle, FT_EEPROM_DataBuffer, FT_EEPROM_Manufacturer, FT_EEPROM_ManufacturerID, FT_EEPROM_Description, FT_EEPROM_SerialNumber);
            if (FT_Status != FT_OK)
            {
                return -1;
            }
            else
            {
                return FT_OK;
            }

        }

        public static int Program_FT2232C_EEPROM(FT_PROGRAM_DATA FT_EEPROM_DataBuffer, string FT_EEPROM_Manufacturer, string FT_EEPROM_ManufacturerID, string FT_EEPROM_Description, string FT_EEPROM_SerialNumber)
        {

            FT_EEPROM_DataBuffer.Signature1 = 0x0;
            FT_EEPROM_DataBuffer.Signature2 = int.MinValue + 0x7FFFFFFF;
            FT_EEPROM_DataBuffer.Version = 1;

            FT_Status = FTDI2XX.FT_EE_ProgramEx(FT_Handle, FT_EEPROM_DataBuffer, FT_EEPROM_Manufacturer, FT_EEPROM_ManufacturerID, FT_EEPROM_Description, FT_EEPROM_SerialNumber);
            if (FT_Status != FT_OK)
            {
                return -1;
            }
            else
            {
                return FT_OK;
            }

        }

        public static int Erase_EEPROM()
        {

            FT_Status = FT_EraseEE(FT_Handle);
            if (FT_Status != FT_OK)
            {
                return -1;
            }
            else
            {
                return FT_OK;
            }

        }

        public static int Get_EEPROM_UA_Size()
        {

            FT_Status = FT_EE_UASize(FT_Handle, FT_UA_Size);
            if (FT_Status != FT_OK)
            {
                return -1;
            }
            else
            {
                return FT_OK;
            }

        }

        public static int Read_EEPROM_UA(int UA_Read_Count)
        {
            var UA_Read_Result = default(int);

            FT_Status = FT_EE_UARead(FT_Handle, FT_UA_Data[0], UA_Read_Count, UA_Read_Result);
            if (FT_Status != FT_OK)
            {
                return -1;
            }
            else
            {
                return FT_OK;
            }

        }

        public static int Write_EEPROM_UA(int UA_Write_Count)
        {
            // Dim UA_Write_Result As Integer

            FT_Status = FT_EE_UAWrite(FT_Handle, FT_UA_Data[0], UA_Write_Count);
            if (FT_Status != FT_OK)
            {
                return -1;
            }
            else
            {
                return FT_OK;
            }

        }

        #endregion
#endif
    }
}
