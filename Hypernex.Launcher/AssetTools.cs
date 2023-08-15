using System.IO;
using System.Reflection;
using Avalonia.Media.Imaging;

namespace Hypernex.Launcher;

public class AssetTools
{
    public static IBitmap? Icon { get; private set; }

    public static void Init()
    {
        Icon = GetBitmap("hypernex_emb.ico");
    }
    
    private static IBitmap GetBitmap(string fileName)
    {
        using (Stream stream =
               Assembly.GetExecutingAssembly().GetManifestResourceStream("Hypernex.Launcher.Assets." + fileName)!)
        {
            return new Bitmap(stream);
        }
    }
}