using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Linq;


namespace SimulatedDevice
{

    public class TelemetryDataPoint<T>: TelemetryData
    {
        //RaspberryPiUWP.cl
        public T property2 { get; set;}   //corresponds to RowKey

        //public TelemetryData

        public TelemetryDataPoint(string s_partitionKey, string s_rowKey, string s_myDeviceId, string label1, string label2, bool s_property1, T s_property2, string s_misc = null)
            : base(s_partitionKey, s_rowKey, s_myDeviceId, label1, label2, s_property1, s_misc)
        {
            this.property2 = s_property2;
        }
    }
}
