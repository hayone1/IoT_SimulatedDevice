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
    class DeviceCommands
    {
        //Rpi to arduino commands scheme |a|b|
        //|a| : 1-subdevice; 2-devicecommand
        //|b| : 0-off; 1-on; 2-toggle
        //ard1|a| : 1-mytemperaturesensor; 2-myhumiditysensor; 3-mymotionsensor; 4-mylightsensor1; 5-myAlarm; 6-homeAwayMode
        //ard2|b| : 1-mydoorsensor; 2-read mydoorcontroller(servoPos); 3-mylightsensor2 4-respond to passCode or just toggle
        internal SystemIOperations serialOPerations;

        public DeviceCommands(SystemIOperations _serialOperations)
        {
            //initialize
            serialOPerations = _serialOperations;
        }

        #region To receive and respond to device direct method invokation 
        //use string - "0" to convert int in arduino
        //SendSerial take in the arguement and the device to send the commad to
        internal  Task<MethodResponse> ToggleLight(MethodRequest methodRequest = null, object userContext = null)
        {
            var data = Encoding.UTF8.GetString(methodRequest.Data); //the data is the payload, the arguement of the methood
            //use a switch statement toknow where to know what exactly to do to device

            // Check the payload is light 1 or light 2{string}
            if (data.Contains(Program.light1.deviceId))
            {
                //toggle indoor light switch here
                serialOPerations.SendSerial("42", $"{messages.myard1};{Program.light1.deviceId}");  //3 means toggle
                Program.light1.property2 = !Program.light1.property2;

                // Acknowlege the direct method call with a 200 success message
                string result = $"{{\"result\":\"Executed direct method: {methodRequest.Name}\"}}";
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine(result);
                Console.ForegroundColor = ConsoleColor.White;
                return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
            }
            else if (data.Contains(Program.light2.deviceId))
            {
                //toggle indoor light switch here
                serialOPerations.SendSerial("32", $"{messages.myard2};{Program.light2.deviceId}");
                Program.light2.property2 = !(Program.light2.property2);

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
                Console.WriteLine($"{result}; data: {data}");
                Console.ForegroundColor = ConsoleColor.White;
                return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 400));
            }
        }
        internal  Task<MethodResponse> TogglePresenceMode(MethodRequest methodRequest = null, object userContext = null)
        {
            //does nothing for now, can be expanded on in future

            serialOPerations.SendSerial("72", $"{messages.myard1};{Program.motionSensor.Misc}");
            Program.motionSensor.Misc = Program.motionSensor.Misc == messages.awayMode ? messages.homeMode : messages.awayMode;
            string result = $"{{\"result\":\"Executed direct method: {methodRequest.Name}\"}}";
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(result);
            Console.ForegroundColor = ConsoleColor.White;
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }
        internal  Task<MethodResponse> ToggleMotionSensor(MethodRequest methodRequest = null, object userContext = null)
        {
            //this currrently wont do anything but can be implemented somehow in the future
            serialOPerations.SendSerial("52", $"{messages.myard1};{Program.motionSensor.deviceId}");
            Program.motionSensor.property2 = !(Program.motionSensor.property2);
            string result = $"{{\"result\":\"Executed direct method: {methodRequest.Name}\"}}";
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(result);
            Console.ForegroundColor = ConsoleColor.White;
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }
        internal  Task<MethodResponse> ToggleSleepMode(MethodRequest methodRequest = null, object userContext = null)
        {
            //string data = Encoding.UTF8.GetString(methodRequest.Data); //the data is the payload, the arguement of the methood
            //toggle the sleep mode, by default, this starts out as Awake on device set-up
            //turn off all the lights, extension, check if the front door is closed and close it if it isnt
            if (Program.light1.property2 == true) { ToggleLight1(); }   //if its on
            if (Program.light1.property2 == true) { ToggleLight2(); }   //if its on
            if (Program.doorSensor.property2 == true) { ToggleDoor(); } //if its open

            Program.raspBerryPi.Misc = Program.raspBerryPi.Misc == messages.sleepMode ? messages.awakeMode : messages.sleepMode;

            string result = $"{{\"result\":\"Executed direct method: {methodRequest.Name}\"}}";
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(result);
            Console.ForegroundColor = ConsoleColor.White;
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }
        internal  Task<MethodResponse> ToggleDoor(MethodRequest methodRequest = null, object userContext = null)
        {
            //string data = Encoding.UTF8.GetString(methodRequest.Data); //the data is the payload, the arguement of the methood
            //signal the arduino to open/close the door, make the lcd always signify what's going on
            if (Program.doorSensor.property2 == true || Program.doorController.property2 < (Program.doorRotMax - 50)) //if door is opened
            {
                /*< (Program.doorRotMax - 50) if the angle is less than the lock angle(opened) with allowance of
                50 in case I later change some things in the arcuino code*/
                LockDoor();
            }
            else
            {
                UnlockDoor();
            }
            //Program.doorController.property2 = Program.doorController.property2 == 175 ? 45 : 175;

            string result = $"{{\"result\":\"Executed direct method: {methodRequest.Name}\"}}";
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(result);
            Console.ForegroundColor = ConsoleColor.White;
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }
        internal Task<MethodResponse> LockDoor(MethodRequest methodRequest = null, object userContext = null)
        {
            //string data = Encoding.UTF8.GetString(methodRequest.Data); //the data is the payload, the arguement of the methood
            //signal the arduino to open/close the door, make the lcd always signify what's going on
            LockDoor();

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine("Force close door");
            Console.ForegroundColor = ConsoleColor.White;

            if (methodRequest != null)
            {
            string result = $"{{\"result\":\"Executed direct method: {methodRequest.Name}\"}}";
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(result);
            Console.ForegroundColor = ConsoleColor.White;
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
            }
            return null;
        }
        internal  Task<MethodResponse> ToggleExtension(MethodRequest methodRequest = null, object userContext = null)
        {
            //also doesnt do anything but can be expanded upon later on
            serialOPerations.SendSerial("62", $"{messages.myard1};{Program.extension.deviceId}");
            Program.extension.property2 = !Program.extension.property2;

            string result = $"{{\"result\":\"Executed direct method: {methodRequest.Name}\"}}";
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(result);
            Console.ForegroundColor = ConsoleColor.White;
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }
        internal  void ToggleLight1()   
        {
                //toggle indoor light switch here
                serialOPerations.SendSerial("42", $"{messages.myard1};{Program.light1.deviceId}");  //2 means toggle
                Program.light2.property2 = !(Program.light2.property2);

        }
        internal void ToggleLight2()   
        {
            //toggle indoor light switch here
            serialOPerations.SendSerial("32", $"{messages.myard2};{Program.light2.deviceId}");
            Program.light2.property2 = !(Program.light2.property2);
        }
        internal void WrongDoorCode()
        {
            //string data = Encoding.UTF8.GetString(methodRequest.Data); //the data is the payload, the arguement of the methood
            //signal the arduino to open/close the door, make the lcd always signify what's going on
            LockDoor();

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine("wrong password, Force close door");
            Console.ForegroundColor = ConsoleColor.White;
        }
        internal void ToggleDoor()
        {
            //string data = Encoding.UTF8.GetString(methodRequest.Data); //the data is the payload, the arguement of the methood
            //signal the arduino to open/close the door, make the lcd always signify what's going on
            if (Program.doorSensor.property2 == true || Program.doorController.property2 > 5) //if door is opened
            {
                serialOPerations.SendSerial("40", $"{messages.myard2};{Program.doorController.deviceId}");  //close it
            }
            else
            {
                serialOPerations.SendSerial("41", $"{messages.myard2};{Program.doorController.deviceId}");  //open it
            }
            Program.doorController.property2 = Program.doorController.property2 == 175 ? 45 : 175;

        }
        internal void LockDoor()    //made this regular method for performance reasons
        {
            serialOPerations.SendSerial("40", $"{messages.myard2};{Program.doorController.deviceId}");
            Program.doorController.property2 = Program.doorRotMax;   //should be zero after
        }
        internal void UnlockDoor()//made this regular method for performance reasons
        {
            serialOPerations.SendSerial("41", $"{messages.myard2};{Program.doorController.deviceId}");  //open it
            Program.doorController.property2 = Program.doorRotMin;   //should be zero after
        }
        #endregion

        #region request telemetry from Arduinos
        internal void RequestTemperature()
        {
            //request temperature from Arduino 1
            serialOPerations.SendSerial("10", $"{messages.myard1};{Program.temperatueSensor.deviceId}");

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Read House Temperature");
            Console.ForegroundColor = ConsoleColor.White;
        }
        internal void RequestHumidity()
        {
            //request temperature from Arduino 1
            serialOPerations.SendSerial("20", $"{messages.myard1};{Program.humiditySensor.deviceId}");

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Read House Humidity");
            Console.ForegroundColor = ConsoleColor.White;
        }
        internal void RequestMotionSensorState()
        {
            //request temperature from Arduino 1
            serialOPerations.SendSerial("30", $"{messages.myard1};{Program.humiditySensor.deviceId}");

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Read House Motion Sensor State");
            Console.ForegroundColor = ConsoleColor.White;
        }
        internal void RequestDoorSensorState()
        {
            //request temperature from Arduino 1
            serialOPerations.SendSerial("10", $"{messages.myard2};{Program.doorSensor.deviceId}");

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Read House Door Sensor State");
            Console.ForegroundColor = ConsoleColor.White;
        }
        internal void RequestDoorControllerState()
        {
            //request temperature from Arduino 1
            serialOPerations.SendSerial("20", $"{messages.myard2};{Program.doorController.deviceId}");

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Read House Door Controller State");
            Console.ForegroundColor = ConsoleColor.White;
        }
        #endregion
    }
}
