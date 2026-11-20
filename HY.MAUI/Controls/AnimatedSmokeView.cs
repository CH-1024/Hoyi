
using Microsoft.Maui.Graphics;

namespace HY.MAUI.Controls;

/// <summary>
/// 多彩流动烟雾背景
/// </summary>
public class AnimatedSmokeView : GraphicsView, IDrawable
{
    private float _time;
    private bool _started;

    private readonly Color[] _colors =
    [
        Color.FromArgb("#8B5CF6"), // 紫色
        Color.FromArgb("#06B6D4"), // 青色
        Color.FromArgb("#EC4899"), // 粉色
        Color.FromArgb("#3B82F6"), // 蓝色
        Color.FromArgb("#F472B6"), // 浅粉
        Color.FromArgb("#F59E0B")  // 橙色
    ];

    public AnimatedSmokeView()
    {
        Drawable = this;
        InputTransparent = true;
        BackgroundColor = Colors.Transparent;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (_started || Handler == null)
            return;

        _started = true;

        Dispatcher.StartTimer(TimeSpan.FromMilliseconds(33), () =>
        {
            if (Handler == null)
            {
                _started = false;
                return false;
            }

            _time += 0.012f;
            Invalidate();

            return true;
        });
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float width = dirtyRect.Width;
        float height = dirtyRect.Height;

        if (width <= 0 || height <= 0)
            return;

        // 根据屏幕尺寸计算烟雾大小
        float baseSize = Math.Min(width, height) * 0.48f;

        // 六团不同颜色的烟雾
        DrawSmoke(canvas, width, height, baseSize, _colors[0], 0.0f, 0.05f, 0.35f, 1.0f);

        DrawSmoke(canvas, width, height, baseSize * 1.15f, _colors[1], 1.1f, 0.85f, 0.25f, 0.85f);

        DrawSmoke(canvas, width, height, baseSize * 0.9f, _colors[2], 2.2f, 0.25f, 0.85f, 0.8f);

        DrawSmoke(canvas, width, height, baseSize * 1.2f, _colors[3], 3.1f, 0.7f, 0.55f, 0.9f);

        DrawSmoke(canvas, width, height, baseSize, _colors[4], 4.2f, 0.45f, 0.15f, 0.8f);

        DrawSmoke(canvas, width, height, baseSize * 0.8f, _colors[5], 5.1f, 0.95f, 0.8f, 0.65f);
    }

    private void DrawSmoke(ICanvas canvas, float width, float height, float size, Color color, float offset, float centerX, float centerY, float intensity)
    {
        float t = _time + offset;

        // 多方向缓慢流动
        float x = width * (centerX + MathF.Sin(t * 0.65f) * 0.13f + MathF.Cos(t * 0.31f) * 0.08f);

        float y = height * (centerY + MathF.Cos(t * 0.52f) * 0.16f + MathF.Sin(t * 0.37f) * 0.09f);

        // 烟雾缓慢呼吸、扩散
        float scale = 1f + MathF.Sin(t * 0.8f) * 0.16f;

        float radiusX = size * scale;
        float radiusY = size * 0.72f * scale;

        // 通过多层半透明椭圆模拟柔和烟雾
        const int layers = 14;

        for (int i = layers - 1; i >= 0; i--)
        {
            float progress = (float)i / (layers - 1);

            float factor = 0.25f + progress * 0.75f;

            float rx = radiusX * factor;
            float ry = radiusY * factor;

            // 外层透明，内部逐渐增强
            float alpha = MathF.Pow(1f - progress, 2.2f) * 0.105f * intensity;

            byte a = (byte)Math.Clamp(alpha * 255f, 0, 255);

            canvas.FillColor = Color.FromRgba((byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255), a);

            canvas.FillEllipse(x - rx, y - ry, rx * 2, ry * 2);
        }

        // 增加一层横向流动的烟雾
        float wave = MathF.Sin(t * 0.9f);

        float wispyWidth = size * (1.1f + wave * 0.15f);
        float wispyHeight = size * 0.28f;

        byte wispyAlpha = (byte)(intensity * 9);

        canvas.FillColor = Color.FromRgba((byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255), wispyAlpha);

        canvas.FillEllipse(x - wispyWidth / 2, y - wispyHeight / 2, wispyWidth, wispyHeight);
    }
}
