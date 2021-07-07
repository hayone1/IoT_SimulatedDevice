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

namespace SimulatedDevice
{
    public class MqttHandler
    {
        MqttClient client = new MqttClient("raspberrypi");
        string subscriptionTopic = "device/authcontrol";
        string SendTopic = "device/startup/broadcast";
        internal string recievedClientID = string.Empty;
        public CancellationTokenSource waitCts;
        JsonSerializerSettings jsonSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
        internal void Initialize()
        {
            // create client instance 
            client = new MqttClient("192.168.8.100");
            //client = new MqttClient("raspberrypi");

            // register to message received 
            client.MqttMsgPublishReceived += client_MqttMsgReceived;

            string clientId = Guid.NewGuid().ToString();
            client.Connect(clientId);

            // subscribe to the topic "/home/temperature" with QoS 2
            client.Subscribe(new string[] { subscriptionTopic }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE});
        }

        internal void BroadcastID(string _deviceID)
        {
            //send device id
            string sendMsg = "DeviceID:" + _deviceID;
            client.Publish(SendTopic, Encoding.UTF8.GetBytes(sendMsg), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);

            waitCts = new CancellationTokenSource();
        }
        void client_MqttMsgReceived(object sender, MqttMsgPublishEventArgs e)
        {
            string receivedMsg = Encoding.UTF8.GetString(e.Message);
            if (receivedMsg.Contains("ID:"))
            {
                Console.WriteLine("received ID from client" + receivedMsg);
                recievedClientID = receivedMsg.Substring(startIndex: 3);  //read the client ID
                //at the stage, the main program uses this ID to alter the devices "row" info
                waitCts.Cancel();   //call the cancel and allow mainProgram to continue
                //the append it to the telemetry data point classesio
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
