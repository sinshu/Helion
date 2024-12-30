using Helion.Graphics;
using Helion.Render;
using System.Diagnostics;
using System.IO;

namespace Helion.Util;

public class SaveGameScreenshotGenerator(Renderer renderer) : IScreenshotGenerator
{
    private readonly Renderer m_renderer = renderer;

    public Image GetImage() => m_renderer.GetScreenshotFrameBufferData();

    public byte[]? GeneratePngImage(Image image)
    {
        var sw = Stopwatch.StartNew();
        using var ms = new MemoryStream();
        if (!image.SavePng(ms, null))
            return null;
        var imageTime = sw.Elapsed.TotalMilliseconds;
        return ms.ToArray();
    }
}
