using Helion.Graphics;
namespace Helion.Util;

public interface IScreenshotGenerator
{
    Image? GetImage();
    byte[]? GeneratePngImage(Image image);
}
