using Robust.Client.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._PS.Lobby.UI;

/// <summary>
/// Palmtree: CPU-side frosted-glass generator for the lobby menu. It samples the lobby background
/// image, blurs the part hidden by the card and applies the same corner cuts as the <c>AngleRect</c>
/// style. No shaders and no screen reads are involved.
/// </summary>
public static class LobbyMenuBackdropBuilder
{
    public readonly struct CropRect
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Width;
        public readonly int Height;

        public CropRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// Builds a card-sized blurred crop.
    /// </summary>
    /// <param name="source">Background image (typically the small cached copy).</param>
    /// <param name="crop">Region of <paramref name="source"/> that sits behind the card.</param>
    /// <param name="outputSize">Card size in physical pixels.</param>
    /// <param name="downscale">How much the crop is reduced before blurring.</param>
    /// <param name="blurRadius">Box-blur radius in reduced-image pixels.</param>
    /// <param name="chamfer">Corner cut size in physical pixels.</param>
    public static Image<Rgba32> Build(
        Image<Rgba32> source,
        CropRect crop,
        Vector2i outputSize,
        int downscale,
        int blurRadius,
        float chamfer)
    {
        var smallWidth = Math.Max(1, outputSize.X / Math.Max(1, downscale));
        var smallHeight = Math.Max(1, outputSize.Y / Math.Max(1, downscale));
        var small = Downscale(source, crop, smallWidth, smallHeight);
        var blurred = BoxBlur(small, smallWidth, smallHeight, blurRadius);

        var image = new Image<Rgba32>(outputSize.X, outputSize.Y);
        var output = image.GetPixelSpan();
        var cut = Math.Clamp(
            (int)MathF.Ceiling(chamfer),
            0,
            Math.Min(outputSize.X, outputSize.Y));

        for (var y = 0; y < outputSize.Y; y++)
        {
            var sourceY = ((y + 0.5f) / outputSize.Y) * smallHeight - 0.5f;
            var y0 = (int)MathF.Floor(sourceY);
            var fy = sourceY - y0;
            var y1 = Math.Clamp(y0 + 1, 0, smallHeight - 1);
            y0 = Math.Clamp(y0, 0, smallHeight - 1);

            for (var x = 0; x < outputSize.X; x++)
            {
                var sourceX = ((x + 0.5f) / outputSize.X) * smallWidth - 0.5f;
                var x0 = (int)MathF.Floor(sourceX);
                var fx = sourceX - x0;
                var x1 = Math.Clamp(x0 + 1, 0, smallWidth - 1);
                x0 = Math.Clamp(x0, 0, smallWidth - 1);

                var c00 = blurred[y0 * smallWidth + x0];
                var c10 = blurred[y0 * smallWidth + x1];
                var c01 = blurred[y1 * smallWidth + x0];
                var c11 = blurred[y1 * smallWidth + x1];

                var red = Lerp(Lerp(c00.R, c10.R, fx), Lerp(c01.R, c11.R, fx), fy);
                var green = Lerp(Lerp(c00.G, c10.G, fx), Lerp(c01.G, c11.G, fx), fy);
                var blue = Lerp(Lerp(c00.B, c10.B, fx), Lerp(c01.B, c11.B, fx), fy);
                var alpha = Lerp(Lerp(c00.A, c10.A, fx), Lerp(c01.A, c11.A, fx), fy);

                if (IsChamfered(x, y, outputSize.X, outputSize.Y, cut))
                    alpha = 0f;

                output[y * outputSize.X + x] = new Rgba32(
                    (byte)Math.Clamp(red, 0f, 255f),
                    (byte)Math.Clamp(green, 0f, 255f),
                    (byte)Math.Clamp(blue, 0f, 255f),
                    (byte)Math.Clamp(alpha, 0f, 255f));
            }
        }

        return image;
    }

    /// <summary>
    /// Area-averages a region of the source image into a smaller pixel buffer.
    /// </summary>
    public static Rgba32[] Downscale(Image<Rgba32> source, CropRect crop, int width, int height)
    {
        var sourcePixels = source.GetPixelSpan();
        var sourceWidth = source.Width;
        var cropX = Math.Clamp(crop.X, 0, source.Width - 1);
        var cropY = Math.Clamp(crop.Y, 0, source.Height - 1);
        var cropWidth = Math.Max(1, Math.Min(crop.Width, source.Width - cropX));
        var cropHeight = Math.Max(1, Math.Min(crop.Height, source.Height - cropY));
        var cropRight = cropX + cropWidth;
        var cropBottom = cropY + cropHeight;
        var result = new Rgba32[width * height];

        for (var y = 0; y < height; y++)
        {
            var y0 = cropY + y * cropHeight / height;
            var y1 = cropY + (y + 1) * cropHeight / height;
            y0 = Math.Clamp(y0, cropY, cropBottom - 1);
            y1 = Math.Clamp(y1, y0 + 1, cropBottom);

            for (var x = 0; x < width; x++)
            {
                var x0 = cropX + x * cropWidth / width;
                var x1 = cropX + (x + 1) * cropWidth / width;
                x0 = Math.Clamp(x0, cropX, cropRight - 1);
                x1 = Math.Clamp(x1, x0 + 1, cropRight);

                long red = 0;
                long green = 0;
                long blue = 0;
                long alpha = 0;
                var count = 0;

                for (var sy = y0; sy < y1; sy++)
                {
                    var row = sy * sourceWidth;
                    for (var sx = x0; sx < x1; sx++)
                    {
                        var pixel = sourcePixels[row + sx];
                        red += pixel.R;
                        green += pixel.G;
                        blue += pixel.B;
                        alpha += pixel.A;
                        count++;
                    }
                }

                count = Math.Max(1, count);
                result[y * width + x] = new Rgba32(
                    (byte)(red / count),
                    (byte)(green / count),
                    (byte)(blue / count),
                    (byte)(alpha / count));
            }
        }

        return result;
    }

    private static Rgba32[] BoxBlur(Rgba32[] source, int width, int height, int radius)
    {
        if (radius <= 0)
            return source;

        var result = new Rgba32[source.Length];

        for (var y = 0; y < height; y++)
        {
            var yStart = Math.Max(0, y - radius);
            var yEnd = Math.Min(height - 1, y + radius);

            for (var x = 0; x < width; x++)
            {
                var xStart = Math.Max(0, x - radius);
                var xEnd = Math.Min(width - 1, x + radius);
                var count = (xEnd - xStart + 1) * (yEnd - yStart + 1);

                long red = 0;
                long green = 0;
                long blue = 0;
                long alpha = 0;

                for (var sy = yStart; sy <= yEnd; sy++)
                {
                    var row = sy * width;
                    for (var sx = xStart; sx <= xEnd; sx++)
                    {
                        var pixel = source[row + sx];
                        red += pixel.R;
                        green += pixel.G;
                        blue += pixel.B;
                        alpha += pixel.A;
                    }
                }

                result[y * width + x] = new Rgba32(
                    (byte)(red / count),
                    (byte)(green / count),
                    (byte)(blue / count),
                    (byte)(alpha / count));
            }
        }

        return result;
    }

    private static float Lerp(float from, float to, float t)
    {
        return from + (to - from) * t;
    }

    private static bool IsChamfered(int x, int y, int width, int height, int cut)
    {
        if (cut <= 0)
            return false;

        // The AngleRect texture cuts the top-right and bottom-left corners.
        if (y < cut && x >= width - cut + y)
            return true;

        return y >= height - cut && x <= cut - (height - y);
    }
}
