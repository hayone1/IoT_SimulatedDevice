using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client.Exceptions;



namespace SimulatedDevice
{
    class Program
    {
        //private static RaspberryPiUWP


        private static readonly string RpiConnectionString = "HostName=FinalYearHub.azure-devices.net;DeviceId=RaspberryPi;SharedAccessKey=rwforzwg0XC7eZpARG0bKD+mjoBkX6ebvEOQ26w2RIA=";
        private static TimeSpan telemetryInterval = TimeSpan.FromSeconds(5);
        private static string telemetryDataString;
        //create devices connected to raspberry pi including raspberry pi
        //private static readonly string RpiConnectionString = "HostName=FinalYearHub.azure-devices.net;DeviceId=MyDotnetDevice;SharedAccessKey=c/HyNUWA04EyjH5wdfpOuY4PtlCG+gI3BMuqARb7kww=";

        private static TelemetryDataPoint<bool> raspBerryPi = new TelemetryDataPoint<bool>("microcontroller", "rpi123", "myrpi", "activated", "connected", 0, s_property2: false, messages.awakeMode);
        private static TelemetryDataPoint<bool> arduino1 = new TelemetryDataPoint<bool>("microcontroller", "ard123", "myard1", "connectedassource", "connectedassink", 0, s_property2: false);
        private static TelemetryDataPoint<bool> arduino2 = new TelemetryDataPoint<bool>("microcontroller", "ard124", "myard2", "connectedassource", "connectedassink", 0, s_property2: false);
        private static TelemetryDataPoint<double> temperatueSensor = new TelemetryDataPoint<double>("temperaturesensor", "tempsens123", "mytemperaturesensor", "connected", "currenttemperture", 0, s_property2: 0);
        private static TelemetryDataPoint<double> humiditySensor = new TelemetryDataPoint<double>("humiditysensor", "humidsens123", "myhumiditysensor", "connected", "currenthumidity", 0, s_property2: 0);
        private static TelemetryDataPoint<bool> doorSensor = new TelemetryDataPoint<bool>("doorsensor", "doorsens123", "mydoorsensor", "connected", "currentdoorstate", 0, s_property2: false);
        private static TelemetryDataPoint<double> doorController = new TelemetryDataPoint<double>("doorcontroller", "doorcontrol123", "mydoorcontroller", "connected", "currentdoorrotation", 0, s_property2: 0);
        private static TelemetryDataPoint<bool> motionSensor = new TelemetryDataPoint<bool>("motionsensor", "humidsens123", "mymotionsensor", "connected", "active", 0, s_property2: false, messages.homeMode);
        private static TelemetryDataPoint<bool> light1 = new TelemetryDataPoint<bool>("lightsensor", "lightsens123", "mylightsensor1", "connected", "active", 0, s_property2: false);   //indoor light
        private static TelemetryDataPoint<bool> light2 = new TelemetryDataPoint<bool>("lightsensor", "lightsens124", "mylightsensor2", "connected", "active", 0, s_property2: false);   //outdoor light


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
            {humiditySensor.deviceId, humiditySensor },
            { doorSensor.deviceId, doorSensor },
            { doorController.deviceId, doorController },
            {motionSensor.deviceId, motionSensor },
            { light1.deviceId, light1 },
            { light2.deviceId, light2 }
        };

        private static DeviceClient deviceClient;
        public static  CancellationTokenSource cts;
        private static MqttHandler LocalNetworkHandler = new MqttHandler();
        //work on storing devices file to use on startup and test auth with mqttX
        public static async Task Main()
        {
            Console.WriteLine("Routing Practical: Simulated device\n press Ctrl + C to cancel");
            #region if device has not paired with client yet
#if DEBUG
            Dictionary<string, object> storedDeviceTelemetry = LocalNetworkHandler.CheckExistingUser();
            if (storedDeviceTelemetry == null)    //if device has not been athenticated b4
            {
                LocalNetworkHandler.Initialize();
                LocalNetworkHandler.BroadcastID(raspBerryPi.deviceId);
                while (!LocalNetworkHandler.waitCts.Token.IsCancellationRequested)
                {
                    //wait infinitely for client to send his own ID, the cancellation is called in LocalNetworkHandler 
                    //when system received ID
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("waiting to recieve client ID");
                    var cancelled = LocalNetworkHandler.waitCts.Token.WaitHandle.WaitOne(Timeout.InfiniteTimeSpan);
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
            await deviceClient.SetMethodHandlerAsync(messages.ToggleLight, ToggleLight, null);
            await deviceClient.SetMethodHandlerAsync(messages.TogglePresenceMode, TogglePresenceMode, null);
            await deviceClient.SetMethodHandlerAsync(messages.ToggleSleepMode, ToggleSleepMode, null);
            await deviceClient.SetMethodHandlerAsync(messages.UnlockDoor, ToggleDoor, null);
            Console.WriteLine("back to main thread");
            //ReceiveDirectCalls();  //start on a separate thread

            //while (true)
            //{
            //    await deviceClient.SetMethodHandlerAsync("ToggleLight", ToggleLight, null);
            //    continue;
            //}

            cts = new CancellationTokenSource();    //create cancellation token
            //set to cancel on key press
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
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

                string infoString = "this is a warning message";
                string levelValue = "normal";



                //perfrom actions here

                foreach (KeyValuePair<string, object> telemetrydata in telemetryDevicesDict)
                {
                    //send telemetry from all the connected devices including rpi
                    // serialize the telemetry data and convert it to JSON.
                    var currentTelemery = telemetrydata.Value;  //this will be of type object
                    JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
                    telemetryDataString = JsonConvert.SerializeObject(currentTelemery, settings);
                    //Console.WriteLine("telemetry type is " + currentTelemery.GetType() + " : " + );


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
                    if (telemetrydata.Key == humiditySensor.deviceId && ((TelemetryDataPoint<double>)currentTelemery).property2 > 60)
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
                        levelValue = messages.warningMessage;
                    }
                    if (telemetrydata.Key == doorSensor.deviceId && ((TelemetryDataPoint<bool>)currentTelemery).property2 == false &&
                        doorController.property2 == 0)
                    {
                        //if the contact sensor senses the door open but the servo didnt open the door
                        infoString = "breach detected at house door, please act immediately";
                        levelValue = messages.criticalMessage;
                    }
                    #endregion


                    message.Properties.Add("level", levelValue);
                    // add message routing rules.
                    // Add one property to the message.
                    //messages with the levelValue ofnormal get set to the SERVICEBUSQUEUE for processing by azure function and storge

                    // Submit the message to the hub.
                    var t_cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));   //time canellation token of 10 secs
                    //t_cts.Token.ThrowIfCancellationRequested();
                    //t_cts.CancelAfter(TimeSpan.FromSeconds(5)); //allow process to fall into catch block on cancellation
                    t_cts.Token.Register(() =>
                    {
                        //deviceClient.CloseAsync();
                        Console.WriteLine("Request cancelled!");

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
                        deviceClient.SendEventAsync(message);    //send but dont await                    }
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

        private static async Task ReceiveDirectCalls()
        {
            Console.WriteLine("starting a new thread");
            await deviceClient.SetMethodHandlerAsync(messages.ToggleLight, ToggleLight, null);



        }


        //used to receive direct commands
        private static Task<MethodResponse> ToggleLight(MethodRequest methodRequest, object userContext)
        {
            var data = Encoding.UTF8.GetString(methodRequest.Data); //the data is the payload, the arguement of the methood
            //use a switch statement toknow where to know what exactly to do to device

            // Check the payload is light 1 or light 2{string}
            if (data.Contains(light1.deviceId))
            {
                //toggle indoor light switch here
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
            //toggle the presence Mode
            //turn off all the lights and arm the door security system, also check if the front door is closed
            //OnTogglePresenceMode();
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
            //OnTogglePresenceMode();
            doorController.property2 = 0;

            string result = $"{{\"result\":\"Executed direct method: {methodRequest.Name}\"}}";
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(result);
            Console.ForegroundColor = ConsoleColor.White;
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }


    }
}
