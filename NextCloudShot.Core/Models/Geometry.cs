namespace NextCloudShot.Core.Models;

public readonly record struct PixelPoint(double X, double Y);

public readonly record struct PixelSize(int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

public readonly record struct PixelRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public PixelRect Normalize()
    {
        double left = Math.Min(X, Right);
        double top = Math.Min(Y, Bottom);
        double right = Math.Max(X, Right);
        double bottom = Math.Max(Y, Bottom);
        return new PixelRect(left, top, right - left, bottom - top);
    }
}
