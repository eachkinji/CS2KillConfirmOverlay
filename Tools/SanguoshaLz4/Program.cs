global using Colors = Microsoft.UI.Colors;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KillConfirmCompatibility.Desktop.Runtime;
using Microsoft.Graphics.Canvas;
using Windows.Graphics.DirectX;
using Windows.Graphics.Imaging;
using Windows.Storage;

internal static partial class Program
{
    private const int Width = 1600, Height = 900, Fps = 60, ShakeSeed = 20261107;
    private static readonly byte[] Lz4Magic = Encoding.ASCII.GetBytes("KCLZ4F01");
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static string Output;

    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 3 || (args[0] != "bake-set" && args[0] != "validate"))
        {
            Console.Error.WriteLine("Usage: SanguoshaLz4Baker bake-set|validate <specification.json> <new-output-directory>");
            return 2;
        }
        Output = Path.GetFullPath(args[2]);
        try
        {
            await BakeSet(Path.GetFullPath(args[1]), validateOnly: args[0] == "validate");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            // A validation-only request never creates output directories or GPU devices.
            if (args[0] == "bake-set" && Directory.Exists(Output))
                Save("bake-set-failure", new { Error = error.ToString() });
            return 1;
        }
    }

    private static void Save(string name, object value) => File.WriteAllText(
        Path.Combine(Output, name + ".json"), JsonSerializer.Serialize(value, Json));

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static async Task<CanvasBitmap> LoadPage(CanvasDevice device, string path)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var pixels = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
        return CanvasBitmap.CreateFromBytes(device, pixels.DetachPixelData(), (int)decoder.PixelWidth,
            (int)decoder.PixelHeight, DirectXPixelFormat.B8G8R8A8UIntNormalized, 96, CanvasAlphaMode.Premultiplied);
    }

    private static object Stats(List<double> values) => new
    {
        Count = values.Count,
        MeanMs = values.Count == 0 ? 0 : values.Average(),
        MaximumMs = values.Count == 0 ? 0 : values.Max()
    };
}
