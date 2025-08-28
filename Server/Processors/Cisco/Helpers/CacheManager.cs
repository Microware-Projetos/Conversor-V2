using System.IO;
using Newtonsoft.Json.Linq;

namespace eCommerce.Server.Processors.Cisco.Helpers;

public class CacheManagerCisco
{
    
    public static void EnsureCacheDir(string cacheDir)
    {
        if (!Directory.Exists(cacheDir))
        {
            Directory.CreateDirectory(cacheDir);
        }
    }

    public static JObject LoadCache(string cachePathFile)
    {
        if (!File.Exists(cachePathFile))
        {
            return new JObject();
        }

        var cacheData = File.ReadAllText(cachePathFile);
        return JObject.Parse(cacheData);
    }
} 