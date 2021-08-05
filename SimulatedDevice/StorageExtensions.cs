using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimulatedDevice
{
    public static class StorageExtentions
    {
        public static T Get<T>(this Dictionary<string, object> instance, string name)
        {
            return (T)instance[name];
        }

        public static T Get<T>(this KeyValuePair<string, object> instance, string name)
        {
            return (T)instance.Value;
        }
    }
}
