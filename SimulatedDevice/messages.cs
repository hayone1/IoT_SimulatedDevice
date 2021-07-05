using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimulatedDevice
{
    public static class messages
    {
        public const string awayMode = "awayMode";
        public const string homeMode = "homeMode";
        public const string sleepMode = "0";
        public const string awakeMode = "1";
        public const string normalMessage = "normal";
        //both warning and critical message is mailed to user
        public const string warningMessage = "warning";
        public const string criticalMessage = "critical";
        public const string ToggleLight = "ToggleLight";
        public const string UnlockDoor = "UnlockDoor";
        public const string TogglePresenceMode = "TogglePresenceMode";
        public const string ToggleSleepMode = "ToggleSleepMode";


    }
}
