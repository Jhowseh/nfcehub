using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NFCeHub;

public static class Logo
{
    public static BitmapSource Load(bool dark)
    {
        if (!dark)
        {
            var light = Png("pbtec_logo_light.png");
            if (light != null)
            {
                return CropToAlpha(ToBgra(light), 8);
            }
        }
        var source = Png("pbtec_logo.png");
        if (source == null)
        {
            return null;
        }
        var lightColor = Theme.ToColor(dark ? "#FFFFFF" : Theme.Palette["Text"]);
        var bitmap = ToBgra(source);
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        for (var i = 0; i < pixels.Length; i += 4)
        {
            int b = pixels[i], g = pixels[i + 1], r = pixels[i + 2];
            var a = Math.Max(r, Math.Max(g, b));
            if (a < 24)
            {
                pixels[i] = pixels[i + 1] = pixels[i + 2] = pixels[i + 3] = 0;
            }
            else if (r > 90 && r > g * 1.8 && r > b * 1.8)
            {
                pixels[i] = (byte)Math.Round(b * 255.0 / a);
                pixels[i + 1] = (byte)Math.Round(g * 255.0 / a);
                pixels[i + 2] = (byte)Math.Round(r * 255.0 / a);
                pixels[i + 3] = (byte)a;
            }
            else
            {
                pixels[i] = lightColor.B;
                pixels[i + 1] = lightColor.G;
                pixels[i + 2] = lightColor.R;
                pixels[i + 3] = (byte)a;
            }
        }
        var processed = BitmapSource.Create(bitmap.PixelWidth, bitmap.PixelHeight, 96, 96, PixelFormats.Bgra32, null,
            pixels, bitmap.PixelWidth * 4);
        return CropToAlpha(processed, 0);
    }

    private static BitmapSource Png(string name)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri($"pack://application:,,,/Assets/{name}", UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static BitmapSource ToBgra(BitmapSource source) =>
        source.Format == PixelFormats.Bgra32 ? source : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

    private static BitmapSource CropToAlpha(BitmapSource bitmap, byte threshold)
    {
        int w = bitmap.PixelWidth, h = bitmap.PixelHeight;
        var pixels = new byte[w * h * 4];
        bitmap.CopyPixels(pixels, w * 4, 0);
        int left = w, top = h, right = -1, bottom = -1;
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (pixels[(y * w + x) * 4 + 3] > threshold)
                {
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }
        }
        if (right < 0)
        {
            return bitmap;
        }
        var cropped = new CroppedBitmap(bitmap, new Int32Rect(left, top, right - left + 1, bottom - top + 1));
        cropped.Freeze();
        return cropped;
    }
}
