// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Drawing.Drawing2D;
using System.Reflection;

namespace Ordinaryunus.Host;

/// <summary>Loads the embedded logo.ico for the taskbar/window icon (copied from the old UI\AppAssets.AppIcon,
/// without the excluded UI project's Theme dependency). Falls back to a drawn teal "O" square.</summary>
public static class AppIcon
{
    static Icon? _cached;

    public static Icon Load()
    {
        if (_cached is not null) return _cached;
        try
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Assets.logo.ico");
            if (s is not null) { _cached = new Icon(s); return _cached; }
        }
        catch { }
        _cached = DrawFallback();
        return _cached;
    }

    static Icon DrawFallback()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var path = RoundedRect(new RectangleF(0, 0, 31, 31), 7f);
            using var b = new SolidBrush(ColorTranslator.FromHtml("#0F766E"));
            g.FillPath(b, path);
            using var f = new Font(FontFamily.GenericSansSerif, 15f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("O", f, Brushes.White, new RectangleF(0, 0, 32, 32), sf);
        }
        nint hIcon = bmp.GetHicon();
        try { return (Icon)Icon.FromHandle(hIcon).Clone(); }
        finally { NativeMethods.DestroyIcon(hIcon); }
    }

    static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool DestroyIcon(nint handle);
    }
}
