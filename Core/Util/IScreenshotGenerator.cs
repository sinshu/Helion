using Helion.Graphics;
namespace Helion.Util;

public interface IScreenshotGenerator
{
    public byte[]? GeneratePngImage();
}
