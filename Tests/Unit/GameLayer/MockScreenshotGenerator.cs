using Helion.Graphics;
using Helion.Util;

namespace Helion.Tests.Unit.GameLayer;

public class MockScreenshotGenerator : IScreenshotGenerator
{
    public Image? GetImage() => new((1, 1), ImageType.Argb);
    public byte[]? GeneratePngImage(Image image) => null;
}
