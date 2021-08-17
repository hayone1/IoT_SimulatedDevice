using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Exceptions;
using uPLibrary.Networking.M2Mqtt.Internal;
using uPLibrary.Networking.M2Mqtt.Messages;
using uPLibrary.Networking.M2Mqtt.Session;
using uPLibrary.Networking.M2Mqtt.Utility;
using Newtonsoft.Json;
using System.IO;
using static System.IO.Directory;
using static System.IO.Path;
using static System.Environment;
using System.Net;

namespace SimulatedDevice
{
    public class MqttHandler
    {
        MqttClient client = new MqttClient("raspberrypi");
        string subscriptionTopic = "Rpi/Request/AuthControl";
        string SendTopic = "device/startup/broadcast";
        internal string recievedClientID = string.Empty;    //this is Email
        internal string recievedPartitionKeyID = string.Empty;
        internal string recievedClientNumber = string.Empty;
        public CancellationTokenSource waitCts;
        JsonSerializerSettings jsonSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
        internal void Initialize()
        {
            // create client instance trying possible raspberry pi IPs
            
            
            try
            {
                client = new MqttClient("raspberrypi.local");
                //client = new MqttClient("192.168.8.101");   //change this back to raspberrypi.local
            }
            catch (System.Net.Sockets.SocketException)
            {
                Console.WriteLine("couldnt conect to broker via socket raspberrypi");
                try
                {
                    client = new MqttClient("192.168.8.150");   //static ip assigned to raspberry pi
                }
                catch (System.Net.Sockets.SocketException e)
                {
                    Console.WriteLine("couldnt conect to broker via socket 192.168.8.102");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Failed to connect to mqtt broker: " + e.Message);
                }
            }

            // register to message received 
            client.MqttMsgPublishReceived += client_MqttMsgReceived;

            string clientId = Guid.NewGuid().ToString();
            client.Connect(clientId);

            // subscribe to the topic "/home/temperature" with QoS 2
            client.Subscribe(new string[] { subscriptionTopic }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE});
        }

        internal void BroadcastID(string _deviceType)
        {
            //send device id
            string sendMsg = "deviceType:" + _deviceType;
            client.Publish(SendTopic, Encoding.UTF8.GetBytes(sendMsg), MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, false);

            waitCts = new CancellationTokenSource();
        }
        internal void ReBroadcastID(string _deviceType)
        {
            //send device id
            string sendMsg = "deviceType:" + _deviceType;
            client.Publish(SendTopic, Encoding.UTF8.GetBytes(sendMsg), MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, false);

            //waitCts = new CancellationTokenSource();
        }
        void client_MqttMsgReceived(object sender, MqttMsgPublishEventArgs e)
        {
            string receivedMsg = Encoding.UTF8.GetString(e.Message);
            if (receivedMsg.Contains("ID|"))    //Id will come in the format of "ID:<userIdWithoutParenthesis>;userPhoneNo
            {
                Console.WriteLine("received ID from client" + receivedMsg);
                //the ID chosen is the mail of the client, I intentionally made it start from ':'
                //so it reflects as the ID when appended to the devices RowKey in the telemetry data
                var partitionStartIndex = receivedMsg.IndexOf('|') + 1;    //starting from ':' is intentional
                var iDstartIndex = receivedMsg.IndexOf(':');    //starting from ':' is intentional as I want the sign included in the payload
                //using iDstartIndex as ":" also eliminated the need to put +1 in the PartitioniDlength
                int PartitioniDlength = iDstartIndex - partitionStartIndex;
                var numberstartIndex = receivedMsg.LastIndexOf(';');
                //using numberstartIndex as ";" eliminated the need to put +1 in the iDlength
                int iDlength = numberstartIndex - iDstartIndex; //the id lies between the 2 symbols
                //Console.WriteLine("index start is:" + iDstartIndex);
                //int iDlength = numberstartIndex - iDstartIndex + 1; //the id lies between the 2 symbols
                //the id lies between the 2 symbols
                recievedClientID = receivedMsg.Substring(startIndex: iDstartIndex, length: iDlength);  //read the client ID
                recievedPartitionKeyID = receivedMsg.Substring(startIndex: partitionStartIndex, length: PartitioniDlength);
                if (!recievedClientID.Contains('@'))    //if the mail is invalid
                {
                    var _exception = new InvalidDataException();
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.Error.WriteLine(_exception + " \ninvalid user email provided: " + recievedClientID);
                    Console.ForegroundColor = ConsoleColor.White;
                    //throw new InvalidDataException().;
                }
                recievedClientNumber = receivedMsg.Substring(startIndex: numberstartIndex + 1);  //read the client phone number

                //at the stage, the main program uses this ID to alter the devices "row" info
                waitCts.Cancel();   //call the cancel and allow mainProgram to continue
                //then append it to the telemetry data point classesio
            }
        }
        internal void BroadcastDevices(string _devicesJson)
        {
            //send altered device class info to client
            //client will use string.contains to know the kind of message broadcasted on this channel
            //it sholud be necessary for client to report back id an invalid Json was sent, but that is currenly out of scope of this project
            client.Publish(SendTopic, Encoding.UTF8.GetBytes(_devicesJson), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
            waitCts = new CancellationTokenSource();    //renew this while main program waits for it
            //cancel is callen on MqttMsgPublished
            client.MqttMsgPublished += client_MqttMsgPublished;

        }

        
        void client_MqttMsgPublished(object sender, MqttMsgPublishedEventArgs e)
        {
            //I'm not sure what e will contain but at this stage the agreement should be complete
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine("Client agreement complete");
            Console.ForegroundColor = ConsoleColor.White;
            client.Publish(SendTopic, Encoding.UTF8.GetBytes("ControlAccessGranted"), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
            client.MqttMsgPublished -= client_MqttMsgPublished; //to avoid loop sending
            waitCts.Cancel();   //this allows main program to finish agreement logic and start sending telemetry

        }

        internal Dictionary<string, object> CheckExistingUser()
        {
            // define a directory path to output files
            // starting in the user's folder
            string dir = Combine(
            GetFolderPath(SpecialFolder.ApplicationData, SpecialFolderOption.None), "ControlFiles");
            Console.WriteLine("directory is " + dir);
            string datFile = Combine(dir, "Devices.bin");
            if (File.Exists(datFile))   //if the devices data has already been stored
            {
                using (BinaryReader reader = new BinaryReader(File.Open(datFile, FileMode.Open)))
                {
                    var storedDeviceData = reader.ReadString(); //read and return the device data
                    return JsonConvert.DeserializeObject<Dictionary<string, object>>(storedDeviceData, jsonSettings);
                    //the binary reader is indirectly disposed
                }
                //signify that directory already exists and device is already authenticated with user
            }
            return null;  //otherwise return empty string if it doesnt exist
        }
        internal bool StoreDevicesFiles(Dictionary<string, object> _devicesTelemetry)
        {
            var dir = Combine(
            GetFolderPath(SpecialFolder.ApplicationData, SpecialFolderOption.Create), "ControlFiles");
            CreateDirectory(dir);
            string datFile = Combine(dir, "Devices.bin");
            //JsonSerializerSettings _settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
            var _devicesJson = JsonConvert.SerializeObject(_devicesTelemetry, formatting: Formatting.Indented, settings: jsonSettings);
            using (BinaryWriter writer = new BinaryWriter(File.Open(datFile, FileMode.Create)))
            {
                try
                {
                writer.Write(_devicesJson);
                return true;
                }
                catch(Exception e)
                {
                    Console.WriteLine("Exception occured" + e.Message);
                    return false;
                }
            }

        }

    }
}
