using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace WinFinder.Thumbnailer.Worker.Common {
    public static class WorkerJson {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        public static T DeserializeFile<T>(string path) {
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(path), Settings);
        }

        public static string Serialize(object value) {
            return JsonConvert.SerializeObject(value, Settings);
        }
    }
}
