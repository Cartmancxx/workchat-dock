using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using WorkChatDock.Interop;
using WorkChatDock.Models;
using Drawing = System.Drawing;
using Media = System.Windows.Media;

namespace WorkChatDock.Services;

public static class IconFactory
{
    public static Drawing.Icon CreateAppIcon(AppDefinition app, int size = 32)
    {
        if (!string.IsNullOrWhiteSpace(app.ExecutablePath) && File.Exists(app.ExecutablePath))
        {
            try
            {
                using var extracted = Drawing.Icon.ExtractAssociatedIcon(app.ExecutablePath);
                if (extracted is not null)
                {
                    return new Drawing.Icon(extracted, size, size);
                }
            }
            catch
            {
                // Fall through to a deterministic generated icon.
            }
        }

        return CreateFallbackIcon(app, size);
    }

    public static Drawing.Icon CreateAggregateIcon(IReadOnlyList<AppDefinition> apps, int size = 32)
    {
        using var bitmap = NewBitmap(size);
        using var graphics = Drawing.Graphics.FromImage(bitmap);
        Configure(graphics);
        using var background = new Drawing.SolidBrush(Drawing.Color.FromArgb(255, 24, 28, 38));
        using var outline = new Drawing.Pen(Drawing.Color.FromArgb(235, 255, 255, 255), Math.Max(1.5f, size / 16f));
        graphics.FillEllipse(background, 1, 1, size - 2, size - 2);

        var colors = apps.Take(4)
            .Select(app => Drawing.ColorTranslator.FromHtml(app.AccentColor))
            .Concat(new[]
            {
                Drawing.Color.DodgerBlue,
                Drawing.Color.RoyalBlue,
                Drawing.Color.MediumSeaGreen,
                Drawing.Color.DeepPink
            })
            .Take(4)
            .ToArray();
        var radius = size * 0.17f;
        var centers = new[]
        {
            new Drawing.PointF(size * 0.34f, size * 0.34f),
            new Drawing.PointF(size * 0.66f, size * 0.34f),
            new Drawing.PointF(size * 0.34f, size * 0.66f),
            new Drawing.PointF(size * 0.66f, size * 0.66f)
        };

        for (var index = 0; index < 4; index++)
        {
            using var brush = new Drawing.SolidBrush(colors[index]);
            graphics.FillEllipse(brush, centers[index].X - radius, centers[index].Y - radius,
                radius * 2, radius * 2);
        }

        graphics.DrawEllipse(outline, 1, 1, size - 2, size - 2);
        return IconFromBitmap(bitmap);
    }

    public static Drawing.Icon CreateAlertIcon(AppDefinition app, bool brightPhase, int size = 32)
    {
        using var sourceIcon = CreateAppIcon(app, size);
        using var bitmap = NewBitmap(size);
        using var graphics = Drawing.Graphics.FromImage(bitmap);
        Configure(graphics);
        graphics.DrawIcon(sourceIcon, new Drawing.Rectangle(2, 2, size - 4, size - 4));

        var borderColor = brightPhase
            ? Drawing.Color.FromArgb(255, 255, 52, 82)
            : Drawing.Color.FromArgb(130, 255, 52, 82);
        using var border = new Drawing.Pen(borderColor, Math.Max(2f, size / 10f));
        graphics.DrawEllipse(border, 1.5f, 1.5f, size - 3, size - 3);
        using var dot = new Drawing.SolidBrush(Drawing.Color.FromArgb(255, 255, 45, 75));
        using var dotOutline = new Drawing.Pen(Drawing.Color.White, Math.Max(1f, size / 24f));
        var dotSize = size * 0.31f;
        graphics.FillEllipse(dot, size - dotSize - 1, 1, dotSize, dotSize);
        graphics.DrawEllipse(dotOutline, size - dotSize - 1, 1, dotSize, dotSize);
        return IconFromBitmap(bitmap);
    }

    public static Media.ImageSource CreateImageSource(AppDefinition app, int size = 48)
    {
        using var icon = CreateAppIcon(app, size);
        var source = Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromWidthAndHeight(size, size));
        source.Freeze();
        return source;
    }

    private static Drawing.Icon CreateFallbackIcon(AppDefinition app, int size)
    {
        using var bitmap = NewBitmap(size);
        using var graphics = Drawing.Graphics.FromImage(bitmap);
        Configure(graphics);
        var color = Drawing.ColorTranslator.FromHtml(app.AccentColor);
        using var brush = new Drawing.SolidBrush(color);
        using var outline = new Drawing.Pen(Drawing.Color.FromArgb(230, 255, 255, 255), Math.Max(1f, size / 18f));
        graphics.FillEllipse(brush, 1, 1, size - 2, size - 2);
        graphics.DrawEllipse(outline, 1, 1, size - 2, size - 2);

        var label = app.DisplayName switch
        {
            "Zalo" => "Z",
            "钉钉" => "钉",
            "飞书" => "飞",
            "京ME" => "ME",
            _ => app.DisplayName[..Math.Min(1, app.DisplayName.Length)]
        };
        using var font = new Drawing.Font("Segoe UI", label.Length > 1 ? size * 0.25f : size * 0.42f,
            Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel);
        var textSize = graphics.MeasureString(label, font);
        using var textBrush = new Drawing.SolidBrush(Drawing.Color.White);
        graphics.DrawString(label, font, textBrush,
            (size - textSize.Width) / 2f,
            (size - textSize.Height) / 2f - size * 0.02f);
        return IconFromBitmap(bitmap);
    }

    private static Drawing.Bitmap NewBitmap(int size) =>
        new(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

    private static void Configure(Drawing.Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
    }

    private static Drawing.Icon IconFromBitmap(Drawing.Bitmap bitmap)
    {
        var handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Drawing.Icon.FromHandle(handle);
            return (Drawing.Icon)borrowed.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }
}
