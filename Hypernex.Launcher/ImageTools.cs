using System.Net.Http;
using System.Text;

namespace Hypernex.Launcher;

public class ImageTools
{
    private static HttpClient client = new ();

    public static byte[] GetRandomImage(string server) =>
        client.GetByteArrayAsync($"https://{server}/api/v1/randomImage").Result;
    
    public static bool IsGif(byte[] data)
    {
        if (data.Length < 3)
            return false;
        byte[] r = {
            data[0],
            data[1],
            data[2]
        };
        string s = Encoding.Default.GetString(r);
        return s.ToLower() == "gif";
    }

    public static string SaveGif(byte[] data) => LauncherCache.SaveFile(data, "a.gif");
}