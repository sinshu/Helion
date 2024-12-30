using Helion.Graphics;
using Helion.Render;
using System.IO;

namespace Helion.Util;

public class SaveGameScreenshotGenerator(Renderer renderer) : IScreenshotGenerator
{
    private readonly Renderer m_renderer = renderer;

    public byte[]? GeneratePngImage()
    {
        var image = m_renderer.GetScreenshotFrameBufferData();
        using var ms = new MemoryStream();
        if (!image.SavePng(ms, null))
            return null;
        return ms.ToArray();
    }
}
