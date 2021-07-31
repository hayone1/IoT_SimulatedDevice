using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client.Exceptions;
using System.IO.Ports;





namespace SimulatedDevice
{
    class Program
    {
        //private static RaspberryPiUWP


        private static readonly string RpiConnectionString = "HostName=FinalYearHub.azure-devices.net;DeviceId=RaspberryPi;SharedAccessKey=rwforzwg0XC7eZpARG0bKD+mjoBkX6ebvEOQ26w2RIA=";
        private static TimeSpan telemetryInterval = TimeSpan.FromSeconds(20);
        private static string telemetryDataString;
        //create devices connected to raspberry pi including raspberry pi
        //private static readonly string RpiConnectionString = "HostName=FinalYearHub.azure-devices.net;DeviceId=MyDotnetDevice;SharedAccessKey=c/HyNUWA04EyjH5wdfpOuY4PtlCG+gI3BMuqARb7kww=";

        internal static TelemetryDataPoint<bool> raspBerryPi = new TelemetryDataPoint<bool>("microcomputer", "rpi123", messages.myrpi, "activated", "connected", false, s_property2: false, messages.awakeMode);
        internal static TelemetryDataPoint<bool> arduino1 = new TelemetryDataPoint<bool>("microcontroller", "ard123", messages.myard1, "connectedassource", "connectedassink", false, s_property2: false);
        internal static TelemetryDataPoint<bool> arduino2 = new TelemetryDataPoint<bool>("microcontroller", "ard124", messages.myard2, "connectedassource", "connectedassink", false, s_property2: false);
        internal static TelemetryDataPoint<double> temperatueSensor = new TelemetryDataPoint<double>("temperaturesensor", "tempsens123", messages.mytemperaturesensor, "connected", "currenttemperture", false, s_property2: 0);
        internal static TelemetryDataPoint<double> humiditySensor = new TelemetryDataPoint<double>("humiditysensor", "humidsens123", messages.myhumiditysensor, "connected", "currenthumidity", false, s_property2: 0);
        internal static TelemetryDataPoint<bool> doorSensor = new TelemetryDataPoint<bool>("doorsensor", "doorsens123", messages.mydoorsensor, "connected", "currentdoorstate", false, s_property2: false);  //this is the contact sensor
        //the doorController's misc string also contains the password inputted by user
        internal static TelemetryDataPoint<double> doorController = new TelemetryDataPoint<double>("doorcontroller", messages.mydoorcontroller, "mydoorcontroller", "connected", "currentdoorrotation", false, s_property2: 0, "0000"); //misc is when user inputs pin
        internal static TelemetryDataPoint<bool> motionSensor = new TelemetryDataPoint<bool>("motionsensor", "humidsens123", messages.mymotionsensor, "connected", "active", false, s_property2: false, messages.homeMode);
        internal static TelemetryDataPoint<bool> light1 = new TelemetryDataPoint<bool>("lightsensor", "lightsens123", messages.mylightsensor1, "connected", "active", false, s_property2: false);   //indoor light
        internal static TelemetryDataPoint<bool> light2 = new TelemetryDataPoint<bool>("lightsensor", "lightsens124", messages.mylightsensor2, "connected", "active", false, s_property2: false);   //outdoor light
        internal static TelemetryDataPoint<bool> extension = new TelemetryDataPoint<bool>("extensionbox", "extensionbox123", messages.myextensionbox, "connected", "active", false, s_property2: false);   //extension box
        internal static TelemetryDataPoint<string> UserDetails = new TelemetryDataPoint<string>("UserID", "userdetails123", messages.myuserdetails, "connected", "phoneno", false, s_property2: "+15005550006", $"{messages.normalMessage}:this is a normal message");   //initialized using magic number



        //add the devices to list
        //private static List<TelemetryDataPoint> telemetryDevices = new List<TelemetryDataPoint>
        //        {raspBerryPi, arduino1, arduino2, temperatureSensor, humiditySensor, doorSensor, motionSensor, light1, light2};
        private static Dictionary<string, object> telemetryDevicesDict = new Dictionary<string, object>()
        {
            //I hope the object casting works lol
            { raspBerryPi.deviceId, raspBerryPi},
            { arduino1.deviceId, arduino1},
            { arduino2.deviceId, arduino2 },
            { temperatueSensor.deviceId, temperatueSensor },
            { humiditySensor.deviceId, humiditySensor },
            { doorSensor.deviceId, doorSensor },
            { doorController.deviceId, doorController },
            { motionSensor.deviceId, motionSensor },
            { light1.deviceId, light1 },
            { light2.deviceId, light2 },
            { extension.deviceId, extension },
            { UserDetails.deviceId, UserDetails }
        };

        private static DeviceClient deviceClient;
        public static  CancellationTokenSource cts;
        private static MqttHandler LocalNetworkHandler = new MqttHandler();
        internal static SystemIOperations serialOPerations = new SystemIOperations(OnSerialReceived);  //to communicate with the arduinos over serial
        private static DeviceCommands deviceCommands = new DeviceCommands(serialOPerations);    //methods to handle serial commands from cloud
        //work on storing devices file to use on startup and test auth with mqttX
        public static async Task Main()
        {
            Console.WriteLine("Routing Practical: Simulated device\n press Ctrl + C to cancel");
            //serialOPerations = new SystemIOperations(OnSerialReceived);
            //deviceCommands = new DeviceCommands(serialOPerations);
            #region if device has not paired with client yet
#if DEBUG

            Dictionary<string, object> storedDeviceTelemetry = LocalNetworkHandler.CheckExistingUser();
            if (storedDeviceTelemetry == null)    //if device has not been athenticated b4
            {
                LocalNetworkHandler.Initialize();
                LocalNetworkHandler.BroadcastID(raspBerryPi.PartitionKey);
                while (!LocalNetworkHandler.waitCts.Token.IsCancellationRequested)
                {
                    //wait infinitely for client to send his own ID, the cancellation is called in LocalNetworkHandler 
                    //when system received ID
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("waiting to recieve client ID");
                    var cancelled = LocalNetworkHandler.waitCts.Token.WaitHandle.WaitOne(Timeout.InfiniteTimeSpan); //wait for device to respond
                }
                if (!string.IsNullOrEmpty(LocalNetworkHandler.recievedClientID))    //if ID was successfully received
                {
                    //create a temporary dictionary to access the telemetry base class data
                    Dictionary<string, TelemetryData> tempDevicesDict = new Dictionary<string, TelemetryData>();
                    foreach(var _telemetryData in telemetryDevicesDict)
                    {
                        //cast the dictionary objects to the telemetryData base class
                        tempDevicesDict.Add(_telemetryData.Key, ((TelemetryData)_telemetryData.Value));
                        /*concatenate rowkey of the TelemetryData to include the clientID
                        hopefully all the boxing, unboxing and passing around of telemetry data is done by reference and no copying is happening*/
                        tempDevicesDict[_telemetryData.Key].RowKey = tempDevicesDict[_telemetryData.Key].RowKey + LocalNetworkHandler.recievedClientID;
                        //altering the values in tempDeviceDick also alters it in the original telemetryDevicesDict
                    }
                    /* Very important piece of code here to get the user's phone number to the cloud
                     twilo SMS service depends on this to work*/
                    UserDetails.property2 = LocalNetworkHandler.recievedClientNumber;   //set this so it can be stored on the azure table storage

                    LocalNetworkHandler.BroadcastDevices(JsonConvert.SerializeObject(telemetryDevicesDict, Formatting.Indented));
                    //connection string is not broadcasted as client code doent use it, rather it exists in the azure function
                    while (!LocalNetworkHandler.waitCts.Token.IsCancellationRequested)
                    {
                        //wait infinitely for client to send his own ID, the cancellation is called in LocalNetworkHandler 
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine("waiting to recieve client final auth acknowledgement");
                        var cancelled = LocalNetworkHandler.waitCts.Token.WaitHandle.WaitOne(Timeout.InfiniteTimeSpan);
                    }
                    //after this point, the device can proceed to send telemetry
                }
                else
                {
                    Console.Error.WriteLine("Code error occured, client ID invalid");
                }
            }
            else
            {   //if device is already registered with cleint
                telemetryDevicesDict = new Dictionary<string, object>();
                telemetryDevicesDict = storedDeviceTelemetry;
                //exract the device last stored data from file
            }
#endif
            #endregion
            //create conection string
            deviceClient = DeviceClient.CreateFromConnectionString(RpiConnectionString, TransportType.Mqtt);    //connect to device in hub
            //to receive direct commands consider using a switch to select the desired command to invoke


            //I dont know if these multiple await would work out
            //commands scheme |a|b|
            //|a| : 1-select subdevice; 2-devicecommand code
            //|b| : 0-off; 1-on; 2-toggle
            //ard1|a| : 1-mytemperaturesensor; 2-myhumiditysensor; 3-mymotionsensor; 4-mylightsensor1; 5-myextensionbox
            //ard2|b| : 1-mydoorsensor; 2-mydoorcontroller; 3-mylightsensor2

            await deviceClient.SetMethodHandlerAsync(messages.ToggleLight, deviceCommands.ToggleLight, null);
            await deviceClient.SetMethodHandlerAsync(messages.TogglePresenceMode, deviceCommands.TogglePresenceMode, null);
            await deviceClient.SetMethodHandlerAsync(messages.ToggleSleepMode, deviceCommands.ToggleSleepMode, null);
            await deviceClient.SetMethodHandlerAsync(messages.UnlockDoor, deviceCommands.ToggleDoor, null);
            await deviceClient.SetMethodHandlerAsync(messages.ToggleMotionSensor, deviceCommands.ToggleMotionSensor, null);
            await deviceClient.SetMethodHandlerAsync(messages.ToggleExtensionBox, deviceCommands.ToggleExtension, null);
            Console.WriteLine("back to main thread");
            //ReceiveDirectCalls();  //start on a separate thread


            cts = new CancellationTokenSource();    //create cancellation token
            //set to cancel on key press
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                serialOPerations.StopSerial();  //stop serial operations
                cts.Cancel();
                Console.WriteLine("Exiting...");
            };



            //perform changes to telemetry data at this point
            //UpdateTelemetry();  //populate this
            await SendDeviceToCloudMessagesAsync(cts.Token);
            await deviceClient.CloseAsync();
            //ok.Dispose();

            deviceClient.Dispose();
            cts.Dispose();

            // await SendDeviceToCloudMessagesAsync();
            //AzureIoTHub.GetConnectionString();
        }


        private static void UpdateTelemetry() { } //to update telemetry=

        private static async Task SendDeviceToCloudMessagesAsync(CancellationToken input_token)
        {

            while (!input_token.IsCancellationRequested)
            {

                string infoString = "this is a normal message";
                string levelValue = "normal";



                //perfrom actions here

                foreach (KeyValuePair<string, object> telemetrydata in telemetryDevicesDict)
                {
                    //send telemetry from all the connected devices including rpi
                    //the telemetry can be occasssionally altered asrec
                    // serialize the telemetry data and convert it to JSON.
                    var currentTelemery = (telemetrydata.Value);  //this will be of type object
                    JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
                    telemetryDataString = JsonConvert.SerializeObject(currentTelemery, settings);


                    // Encode the serialized object using UTF-8 so it can be parsed by IoT Hub when
                    var message = new Message(Encoding.UTF8.GetBytes(telemetryDataString))
                    {
                        ContentEncoding = "utf-8",
                        ContentType = "application/json",
                    };
                     #region code to determine the alert level of the telemetry data for routing

                    //the dictionary key helps know the correct type cast for the currentTelemetry checking
                    if (telemetrydata.Key == temperatueSensor.deviceId && ((TelemetryDataPoint<double>)currentTelemery).property2 > 40)
                    {
                        //if house temperaure reaches 40 degrees celsius
                        infoString = "House Temperature is reaching undesirable levels: " + ((TelemetryDataPoint<double>)currentTelemery).property2;
                        levelValue = messages.warningMessage;
                    }
                    if (telemetrydata.Key == humiditySensor.deviceId && ((TelemetryDataPoint<double>)currentTelemery).property2 > 45)
                    {
                        //if house temperaure reaches 40 degrees celsius
                        infoString = "House Humidity is reaching undesirable levels: " + ((TelemetryDataPoint<double>)currentTelemery).property2;
                        levelValue = messages.warningMessage;
                    }
                    if (telemetrydata.Key == motionSensor.deviceId && ((TelemetryDataPoint<bool>)currentTelemery).property2 == true && 
                        ((TelemetryDataPoint<bool>)currentTelemery).Misc == messages.awayMode)
                    {
                        //if the motion sensor is triggered and home owner is away(indicated by misc)
                        infoString = "possible intruder detected inside home, please check immediately";
                        levelValue = messages.criticalMessage;
                    }
                    if (telemetrydata.Key == doorSensor.deviceId && ((TelemetryDataPoint<bool>)currentTelemery).property2 == false &&
                        raspBerryPi.Misc == messages.sleepMode)
                    {
                        //if the contact sensor senses the door open but the house is in sleep mode
                        infoString = "The front door has been left open";
                        levelValue = messages.warningMessage;
                    }
                    if (telemetrydata.Key == doorSensor.deviceId && ((TelemetryDataPoint<bool>)currentTelemery).property2 == false &&
                        doorController.property2 < 5)
                    {
                        //if the contact sensor senses the door open but the servo isnt in an authorized open state
                        //as in the last known servo state was closed
                        infoString = "breach detected at house door, please act immediately";
                        levelValue = messages.criticalMessage;
                    }
                    #endregion


                    message.Properties.Add("level", levelValue);
                    message.Properties.Add("info", infoString);
                    //also append the message to UserDetails so that Azure functions can detect and send SMS if necessary
                    UserDetails.Misc = $"{levelValue}:{infoString}";    
                    // add message routing rules.
                    // Add one property to the message.
                    /*all messages are sent to SERVICEBUSQUEUE for processing by azure function and storge
                     * and messages some are processed by azure logicApps and functions for appropriate alerts*/

                    // Submit the message to the hub.
                    var t_cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));   //time canellation token of 10 secs
                    //t_cts.Token.ThrowIfCancellationRequested();
                    //t_cts.CancelAfter(TimeSpan.FromSeconds(5)); //allow process to fall into catch block on cancellation
                    t_cts.Token.Register(() =>
                    {
                        //deviceClient.CloseAsync();
                        Console.WriteLine("Request cancelled!");
                        t_cts.Dispose();

                    });
                    try
                    {
                        await deviceClient.SendEventAsync(message, t_cts.Token);   //reference the task
                        Console.WriteLine("event sent");
                        // await _telemetryTask;
                    }
                    catch (TaskCanceledException)
                    {
                        var timedOutDevice = JsonConvert.DeserializeObject<TelemetryDataPoint<object>>(telemetryDataString as string);

                        Console.WriteLine("\nTasks cancelled: timed out. while sending telemetry from \n" + timedOutDevice.deviceId);
                        //change level value
                        levelValue = "failure";
                        message.Properties.Remove("level");
                        message.Properties.Add("level", levelValue);
                        await deviceClient.SendEventAsync(message);    //send but dont await                    }
                    }
                    catch (IotHubCommunicationException)
                    {
                        var timedOutDevice = JsonConvert.DeserializeObject<TelemetryDataPoint<object>>(telemetryDataString as string);

                        Console.WriteLine("\ncannot communicate with iotHub. while sending telemetry from \n" + timedOutDevice.deviceId);
                        Console.WriteLine("trying again");
                        //change level value
                        levelValue = "failure";
                        message.Properties.Remove("level");
                        message.Properties.Add("level", levelValue);
                        _ = deviceClient.SendEventAsync(message);    //send but dont await//its a form of trying again                 }
                                                                       // tasks.Add(task); 
                    }
                    Console.WriteLine($"{infoString} > Sent message: parameter {DateTime.UtcNow} : {telemetryDataString}");
                    // Print out the message.

                }
                if (!string.IsNullOrEmpty(telemetryDataString))
                {
                LocalNetworkHandler.StoreDevicesFiles(telemetryDevicesDict);

                }
                await Task.Delay(telemetryInterval); //delay before sending next telemetry batch

                // await Task.WhenAll(tasks);  //wait for all telemtry to complete
            }
        }
        private static void OnSerialReceived(object sender, SerialDataReceivedEventArgs e)
        {
            //arduino will send telemetry using the device ID of the updated device
            //and the parameter label of the telemetry it is sending
            //like myard1;property2:.... or myard1;misc:....
            //put data received logic here
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadLine();

            //read the data from after colon(:) and remove any newline
            //when a device property is changed, set the device state to connected{property1}
            char[] trimChars = { '\n', '\0' };
            var value = indata.Substring(indata.LastIndexOf(":") + 1).Trim(trimChars);
            if (indata.Contains(arduino1.deviceId))
            {
                arduino1.property1 = true;
                if (indata.Contains(messages.property2)) {
                    //telemetryDevicesDict[arduino1.deviceId]
                    arduino1.property2 = Convert.ToBoolean(value);
                }
                else if (indata.Contains(messages.misc))
                {
                    //"False" or "True"
                    arduino1.Misc = value;
                }
                //I really dont want to put a final else statement
            }
            else if (indata.Contains(arduino2.deviceId))
            {
                arduino2.property1 = true;
                //respond to possible inputs from this device
                if (indata.Contains(messages.property2))
                {
                    //telemetryDevicesDict[arduino1.deviceId]
                    arduino2.property2 = Convert.ToBoolean(value);
                }
                else if (indata.Contains(messages.misc))
                {
                    //"False" or "True"
                    arduino2.Misc = value;
                }
                //I really dont want to put a final else statement
            }
            else if (indata.Contains(temperatueSensor.deviceId))
            {
                temperatueSensor.property1 = true;
                //respond to possible inputs from this device
                if (indata.Contains(messages.property2))
                {
                    //telemetryDevicesDict[arduino1.deviceId]
                    temperatueSensor.property2 = Convert.ToDouble(value);
                }
                else if (indata.Contains(messages.misc))
                {
                    //"False" or "True"
                    temperatueSensor.Misc = value;
                }
                //I really dont want to put a final else statement
            }
            else if (indata.Contains(humiditySensor.deviceId))
            {
                humiditySensor.property1 = true;
                //respond to possible inputs from this ard
                if (indata.Contains(messages.property2))
                {
                    //telemetryDevicesDict[arduino1.deviceId]
                    humiditySensor.property2 = Convert.ToDouble(value);
                }
                else if (indata.Contains(messages.misc))
                {
                    //"False" or "True"
                    humiditySensor.Misc = value;
                }
                //I really dont want to put a final else statement
            }
            else if (indata.Contains(doorSensor.deviceId))
            {
                doorSensor.property1 = true;
                //respond to possible inputs from this ard
                if (indata.Contains(messages.property2))
                {
                    //"False" or "True"
                    doorSensor.property2 = Convert.ToBoolean(value);    //true means open
                }
                else if (indata.Contains(messages.misc))
                {
                    //"False" or "True"
                    doorSensor.Misc = value;
                }
                //I really dont want to put a final else statement
            }
            else if (indata.Contains(doorController.deviceId))
            {
                doorController.property1 = true;
                //respond to possible inputs from this ard
                if (indata.Contains(messages.property2))
                {
                    //telemetryDevicesDict[arduino1.deviceId]
                    doorController.property2 = Convert.ToDouble(value);
                }
                else if (indata.Contains(messages.misc))
                {
                    //this is the user's password, the valuse should actually be evaluated from the cloud
                    doorController.Misc = value;
                    if (value == "0123" && doorSensor.property2 == true || doorController.property2 == 0) //if door is locked
                    { _ = deviceCommands.ToggleDoor(); }
                    else { deviceCommands.WrongDoorCode(); }
                }
                //I really dont want to put a final else statement
            }
            else if (indata.Contains(motionSensor.deviceId))
            {
                motionSensor.property1 = true;
                //respond to possible inputs from this ard
                if (indata.Contains(messages.property2))
                {
                    //telemetryDevicesDict[arduino1.deviceId]
                    motionSensor.property2 = Convert.ToBoolean(value);
                }
                else if (indata.Contains(messages.misc))
                {
                    //"False" or "True"
                    motionSensor.Misc = value;
                }
                //I really dont want to put a final else statement
            }
            else if (indata.Contains(light1.deviceId))
            {
                light1.property1 = true;
                //respond to possible inputs from this ard
                if (indata.Contains(messages.property2))
                {
                    //telemetryDevicesDict[arduino1.deviceId]
                    light1.property2 = Convert.ToBoolean(value);
                }
                else if (indata.Contains(messages.misc))
                {
                    //"False" or "True"
                    light1.Misc = value;
                }
                //I really dont want to put a final else statement
            }
            else if (indata.Contains(light2.deviceId))
            {
                light2.property1 = true;
                //respond to possible inputs from this ard
                if (indata.Contains(messages.property2))
                {
                    //telemetryDevicesDict[arduino1.deviceId]
                    light2.property2 = Convert.ToBoolean(value);
                }
                else if (indata.Contains(messages.misc))
                {
                    //"False" or "True"
                    light2.Misc = value;
                }
                //I really dont want to put a final else statement
            }
            else if (indata.Contains(motionSensor.deviceId))
            {
                motionSensor.property1 = true;
                //respond to possible inputs from this ard
                if (indata.Contains(messages.property2))
                {
                    //telemetryDevicesDict[arduino1.deviceId]
                    motionSensor.property2 = Convert.ToBoolean(value);
                }
                else if (indata.Contains(messages.misc))
                {
                    //"False" or "True"
                    light2.Misc = value;    //arduino should not write to this directly
                }
                //I really dont want to put a final else statement
            }

            Console.ForegroundColor = ConsoleColor.DarkBlue;
            Console.WriteLine("Data Received is:" + indata);
            Console.ForegroundColor = ConsoleColor.White;
        }



#region To receive and respond to device direct method invokation 
        //use string - "0" to convert int in arduino
        private static Task<MethodResponse> ToggleLight(MethodRequest methodRequest, object userContext)
        {
            var data = Encoding.UTF8.GetString(methodRequest.Data); //the data is the payload, the arguement of the methood
            //use a switch statement toknow where to know what exactly to do to device

            // Check the payload is light 1 or light 2{string}
            if (data.Contains(light1.deviceId))
            {
                //toggle indoor light switch here
                serialOPerations.SendSerial("2", $"{messages.myard1};{light1.deviceId}");  //3 means toggle
                light1.property2 = !light1.property2;

                // Acknowlege the direct method call with a 200 success message
                string result = $"{{\"result\":\"Executed direct method: {methodRequest.Name}\"}}";
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine(result);
                Console.ForegroundColor = ConsoleColor.White;
                return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
            }
            else if (data.Contains(light2.deviceId))
            {
                //toggle indoor light switch here
                serialOPerations.SendSerial("3", $"{messages.myard2};{light2.deviceId}");
                light2.property2 = !(light2.property2);

                // Acknowlege the direct method call with a 200 success message
                string result = $"{{\"result\":\"Executed direct method: {methodRequest.Name}\"}}";
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine(result);
                Console.ForegroundColor = ConsoleColor.White;
                return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));

            }
            else
            {
                // Acknowlege the direct method call with a 400 error message
                string result = "{\"result\":\"Invalid parameter\"}";
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(result);
                Console.ForegroundColor = ConsoleColor.White;
                return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 400));
            }
        }
        private static Task<MethodResponse> TogglePresenceMode(MethodRequest methodRequest, object userContext)
        {
            string data = Encoding.UTF8.GetString(methodRequest.Data); //the data is the payload, the arguement of the methood
            //toggle the presence Mode
            motionSensor.Misc = motionSensor.Misc == messages.awayMode ? messages.homeMode : messages.awayMode;
            string result = $"{{\"result\":\"Executed direct method: {methodRequest.Name}\"}}";
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(result);
            Console.ForegroundColor = ConsoleColor.White;
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }
        private static Task<MethodResponse> ToggleMotionSensor(MethodRequest methodRequest, object userContext)
        {
            string data = Encoding.UTF8.GetString(methodRequest.Data); //the data is the payload, the arguement of the methood
            //toggle the motion sensor
            serialOPerations.SendSerial("3", $"{messages.myard2};{motionSensor.deviceId}");
            motionSensor.property2 = !(motionSensor.property2);
            string result = $"{{\"result\":\"Executed direct method: {methodRequest.Name}\"}}";
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(result);
            Console.ForegroundColor = ConsoleColor.White;
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }
        private static Task<MethodResponse> ToggleSleepMode(MethodRequest methodRequest, object userContext)
        {
            string data = Encoding.UTF8.GetString(methodRequest.Data); //the data is the payload, the arguement of the methood
            //toggle the sleep mode, by default, this starts out as Awake on device set-up
            //turn off all the lights and arm the door security system, also check if the front door is closed
            
            raspBerryPi.Misc = raspBerryPi.Misc == messages.sleepMode ? messages.awakeMode : messages.sleepMode;

            string result = $"{{\"result\":\"Executed direct method: {methodRequest.Name}\"}}";
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(result);
            Console.ForegroundColor = ConsoleColor.White;
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }
        private static Task<MethodResponse> ToggleDoor(MethodRequest methodRequest, object userContext)
        {
            string data = Encoding.UTF8.GetString(methodRequest.Data); //the data is the payload, the arguement of the methood
            //signal the arduino to close the door
            serialOPerations.SendSerial("22", $"{messages.myard2};{doorController.deviceId}");
            doorController.property2 = doorController.property2 == 90 ? 0 : 90; 

            string result = $"{{\"result\":\"Executed direct method: {methodRequest.Name}\"}}";
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(result);
            Console.ForegroundColor = ConsoleColor.White;
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }
        private static Task<MethodResponse> ToggleExtension(MethodRequest methodRequest, object userContext)
        {
            string data = Encoding.UTF8.GetString(methodRequest.Data); //the data is the payload, the arguement of the methood
            //signal the arduino to close the door
            serialOPerations.SendSerial("52", $"{messages.myard1};{extension.deviceId}");
            extension.property2 = !extension.property2;

            string result = $"{{\"result\":\"Executed direct method: {methodRequest.Name}\"}}";
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(result);
            Console.ForegroundColor = ConsoleColor.White;
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }
        #endregion


    }
}
