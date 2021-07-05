using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimulatedDevice
{
    class Program
    {
        //private static RaspberryPiUWP


        private static readonly string RpiConnectionString = "HostName=FinalYearHub.azure-devices.net;DeviceId=RaspberryPi;SharedAccessKey=rwforzwg0XC7eZpARG0bKD+mjoBkX6ebvEOQ26w2RIA=";
        private static TimeSpan telemetryInterval = TimeSpan.FromSeconds(5);
        //create devices connected to raspberry pi including raspberry pi
        //private static readonly string RpiConnectionString = "HostName=FinalYearHub.azure-devices.net;DeviceId=MyDotnetDevice;SharedAccessKey=c/HyNUWA04EyjH5wdfpOuY4PtlCG+gI3BMuqARb7kww=";

        private static TelemetryDataPoint<bool> raspBerryPi = new TelemetryDataPoint<bool>("microcontroller", "rpi123", "myrpi", "activated", "connected", 0, s_property2: false, messages.awakeMode);
        private static TelemetryDataPoint<bool> arduino1 = new TelemetryDataPoint<bool>("microcontroller", "ard123", "myard1", "connectedassource", "connectedassink", 0, s_property2: false);
        private static TelemetryDataPoint<bool> arduino2 = new TelemetryDataPoint<bool>("microcontroller", "ard124", "myard2", "connectedassource", "connectedassink", 0, s_property2: false);
        private static TelemetryDataPoint<double> temperatueSensor = new TelemetryDataPoint<double>("temperaturesensor", "tempsens123", "mytemperaturesensor", "connected", "currenttemperture", 0, s_property2: 0);
        private static TelemetryDataPoint<double> humiditySensor = new TelemetryDataPoint<double>("humiditysensor", "humidsens123", "myhumiditysensor", "connected", "currenthumidity", 0, s_property2: 0);
        private static TelemetryDataPoint<bool> doorSensor = new TelemetryDataPoint<bool>("doorsensor", "doorsens123", "mydoorsensor", "connected", "currentdoorstate", 0, s_property2: false);
        private static TelemetryDataPoint<bool> doorController = new TelemetryDataPoint<bool>("doorcontroller", "doorcontrol123", "mydoorcontroller", "connected", "currentdoorstate", 0, s_property2: false);
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
        public static CancellationTokenSource cts;
        public static async Task Main()
        {
            Console.WriteLine("Routing Practical: Simulated device\n press Ctrl + C to cancel");
            //create conection string
            deviceClient = DeviceClient.CreateFromConnectionString(RpiConnectionString, TransportType.Mqtt);    //connect to device in hub
            //to receive direct commands consider using a switch to select the desired command to invoke


            //new Thread(() => Task.Run(() => { ReceiveDirectCalls(); }));
            //await Task.Run(() => { ReceiveDirectCalls(); });
            await deviceClient.SetMethodHandlerAsync(messages.ToggleLight, ToggleLight, null);
            //I dont know if these multiple await would work out
            await deviceClient.SetMethodHandlerAsync(messages.TogglePresenceMode, TogglePresenceMode, null);
            await deviceClient.SetMethodHandlerAsync(messages.ToggleSleepMode, ToggleSleepMode, null);
            await deviceClient.SetMethodHandlerAsync(messages.UnlockDoor, UnlockDoor, null);
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
                    var currentTelemery = telemetryDevicesDict[telemetrydata.Key];  //this will be of type object
                    var telemetryDataString = JsonConvert.SerializeObject(currentTelemery);


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
                        levelValue = messages.criticalMessage;

                    }
                    #endregion


                    message.Properties.Add("level", levelValue);
                    // add message routing rules.
                    // Add one property to the message.
                    //messages with the levelValue ofnormal get set to the SERVICEBUSQUEUE for processing by azure function and storge

                    // Submit the message to the hub.
                    try
                    {
                        var t_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));   //time canellation token of 10 secs
                        t_cts.Token.ThrowIfCancellationRequested(); //allow process to fall into catch block on cancellation
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
                                                                       // tasks.Add(task); 
                    }
                    Console.WriteLine($"{infoString} > Sent message: parameter {DateTime.UtcNow} : {telemetryDataString}");
                    // Print out the message.

                }
                await Task.Delay(telemetryInterval); //delay before sending next telemetry batch

                // await Task.WhenAll(tasks);  //wait for all telemtry to complete
            }
        }

        private static async Task ReceiveDirectCalls()
        {
            Console.WriteLine("starting a new thread");
            await deviceClient.SetMethodHandlerAsync(messages.ToggleLight, ToggleLight, null);
            //while (true)
            //{
            //    //await deviceClient.CompleteAsync();
            //    //continue;
            //}
            // await Task.Run(() =>
            //{

            //});



        }


        //used to receive direct commands
        private static Task<MethodResponse> ToggleLight(MethodRequest methodRequest, object userContext)
        {
            var data = Encoding.UTF8.GetString(methodRequest.Data); //the data is the payload, the arguement of the methood
            //use a switch statement toknow where to know what exactly to do to device

            // Check the payload is light 1 or light 2{string}
            if (data == light1.deviceId)
            {
                //toggle indoor light switch here
                light1.property2 = !(light1.property2);

                // Acknowlege the direct method call with a 200 success message
                string result = $"{{\"result\":\"Executed direct method: {methodRequest.Name}\"}}";
                return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
            }
            else if (data == light2.deviceId)
            {
                //toggle indoor light switch here
                light2.property2 = !(light2.property2);

                // Acknowlege the direct method call with a 200 success message
                string result = $"{{\"result\":\"Executed direct method: {methodRequest.Name}\"}}";
                return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));

            }
            else
            {
                // Acknowlege the direct method call with a 400 error message
                string result = "{\"result\":\"Invalid parameter\"}";
                return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 400));
            }
        }
        private static Task<MethodResponse> TogglePresenceMode(MethodRequest methodRequest, object userContext)
        {
            string data = Encoding.UTF8.GetString(methodRequest.Data); //the data is the payload, the arguement of the methood
            //toggle the presence Mode
            motionSensor.property2 = !(motionSensor.property2);
            string result = $"{{\"result\":\"Executed direct method: {methodRequest.Name}\"}}";
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }
        private static Task<MethodResponse> ToggleSleepMode(MethodRequest methodRequest, object userContext)
        {
            string data = Encoding.UTF8.GetString(methodRequest.Data); //the data is the payload, the arguement of the methood
            //toggle the presence Mode
            //turn off all the lights and arm the door security system, also check if the front door is closed
            OnTogglePresenceMode();
            raspBerryPi.Misc = raspBerryPi.Misc == messages.sleepMode ? messages.awakeMode : messages.sleepMode;

            string result = $"{{\"result\":\"Executed direct method: {methodRequest.Name}\"}}";
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }
        private static Task<MethodResponse> ToggleDoor(MethodRequest methodRequest, object userContext)
        {
            string data = Encoding.UTF8.GetString(methodRequest.Data); //the data is the payload, the arguement of the methood
            //toggle the presence Mode
            //turn off all the lights and arm the door security system, also check if the front door is closed
            OnTogglePresenceMode();
            raspBerryPi.Misc = raspBerryPi.Misc == messages.sleepMode ? messages.awakeMode : messages.sleepMode;

            string result = $"{{\"result\":\"Executed direct method: {methodRequest.Name}\"}}";
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }


    }
}
