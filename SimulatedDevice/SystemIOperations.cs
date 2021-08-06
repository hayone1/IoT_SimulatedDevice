using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SimulatedDevice
{
    class SystemIOperations
    {   //this class handles serial communication with the arduino
        private SerialPort ard1Port;    //indoor arduino
        private SerialPort ard2Port;    //outdoor arduino
        private List<SerialPort> ardSerialPorts;
        //private Action<object, SerialDataReceivedEventArgs> localSerialDataReceivedCallback;
        private SerialDataReceivedEventHandler dataReceivedHandler;
        public SystemIOperations(Action<object, SerialDataReceivedEventArgs> SerialDataReceivedCallback)
        {
            //initialize parameters and add callback to react to serial data received from arduinos
            //the callback is a method within the initializing class, which in this cass is Program
            //localSerialDataReceivedCallback = SerialDataReceivedCallback;
            dataReceivedHandler = new SerialDataReceivedEventHandler(SerialDataReceivedCallback);    //assign delegate to call back
            if (OperatingSystem.IsLinux())
            {
                //ports labels were made fixed 
                ard1Port = new SerialPort("/dev/ttyACM0");
                ard2Port = new SerialPort("/dev/ttyACM1");
                Console.WriteLine("We're on linux!");
            }
            else if (OperatingSystem.IsWindows())
            {
                //windows is for testing puroses, you should confirm thr port labels on our device from the Arduino IDE or others
                //as the labels wont be same for everybody
                Console.WriteLine("We're on WIndows!");
                ard1Port = new SerialPort("COM8", 9600);
                ard2Port = new SerialPort("COM9", 9600);
                //ard1Port = new SerialPort("COM8", 9600);
                //ard2Port = new SerialPort("COM14", 9600);
            }

                ard1Port.BaudRate = 9600;
                ard1Port.Parity = Parity.None;
                ard1Port.StopBits = StopBits.One;
                ard1Port.DataBits = 8;
                ard1Port.Handshake = Handshake.None;
                ard1Port.RtsEnable = true;
                ard1Port.Encoding = System.Text.Encoding.ASCII;

                ard2Port.BaudRate = 9600;
                ard2Port.Parity = Parity.None;
                ard2Port.StopBits = StopBits.One;
                ard2Port.DataBits = 8;
                ard2Port.Handshake = Handshake.None;
                ard2Port.RtsEnable = true;
                ard1Port.Encoding = System.Text.Encoding.ASCII;

                
            ardSerialPorts = new List<SerialPort> { ard1Port, ard2Port };
            StartSerial();

            //foreach (SerialPort _port in ardSerialPorts)
            //{   //initialize port parameters
            //    _port.BaudRate = 9600;
            //    _port.Parity = Parity.None;
            //    _port.StopBits = StopBits.One;
            //    _port.DataBits = 8;
            //    _port.Handshake = Handshake.None;
            //    _port.RtsEnable = true;


            //}



        }
        public void ActivateSerialDataHanadler()
        {
            ard1Port.DataReceived += dataReceivedHandler;
            ard2Port.DataReceived += dataReceivedHandler;
        }
        public void StartSerial()
        {
            //ard1Port.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
            //ard1Port.Open();
            //ard2Port.Open();
            foreach(SerialPort _port in ardSerialPorts) {_port.Open(); }
        }
        public void StopSerial()
        {
            ard1Port.DataReceived -= dataReceivedHandler;   //unsubscribe
            ard2Port.DataReceived -= dataReceivedHandler;   //unsubscribe
            foreach (SerialPort _port in ardSerialPorts)
            {  //cleanup
                _port.Close();

            }
        }
        public void SendSerial(string message_string, string combined_Id)
        {
            var device_Id = combined_Id.Substring(combined_Id.LastIndexOf(";") + 1);    
            //I didnt really do anythig with this but left it to the arduino to select the device based on the string sent
            if (combined_Id.Contains(messages.myard1))
            {
                ard1Port.WriteLine(message_string);
            }
            else if (combined_Id.Contains(messages.myard2))
            {
                ard2Port.WriteLine(message_string);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("error in selecting serial device, please check code Method 'SendSerial'");
            }
        }

        private static void DataReceivedHandler(object sender,SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting();
            Console.WriteLine("Data Received:");
            Console.Write(indata);
        }


        public static class OperatingSystem
        {
            public static bool IsWindows() =>
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            public static bool IsMacOS() =>
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

            public static bool IsLinux() =>
                RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        }
    }
}