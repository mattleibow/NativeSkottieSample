using Android.Content;
using Android.Runtime;
using Android.Util;
using SkiaSharp;
using SkiaSharp.Skottie;
using SkiaSharp.Views.Android;

using SkottieAnimation = SkiaSharp.Skottie.Animation;

namespace NativeSkottieSample;

public class LottieAnimationView : SKCanvasView
{
    // 60 frames per second (1000ms)
    private const int DelayMilliseconds = 1000 / 60;

    private string? fileName;
    private SkottieAnimation? currentAnimation;
    private TimeSpan progress = TimeSpan.Zero;
    private long lastFrameTime = 0;

    public LottieAnimationView(Context context)
        : base(context)
    {
        Initialize();
    }

    public LottieAnimationView(Context context, IAttributeSet attrs)
        : base(context, attrs)
    {
        Initialize(attrs);
    }

    public LottieAnimationView(Context context, IAttributeSet attrs, int defStyleAttr)
        : base(context, attrs, defStyleAttr)
    {
        Initialize(attrs);
    }

    protected LottieAnimationView(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
        Initialize();
    }

    private void Initialize(IAttributeSet? attrs = null)
    {
        if (attrs is null || Context is null)
            return;

        using var a = Context.ObtainStyledAttributes(attrs, Resource.Styleable.LottieAnimationView);

        var N = a.IndexCount;
        for (var i = 0; i < N; ++i)
        {
            var attr = a.GetIndex(i);
            if (attr == Resource.Styleable.LottieAnimationView_lottie_fileName)
                FileName = a.GetString(attr);
        }

        a.Recycle();
    }

    public string? FileName
    {
        get => fileName;
        set
        {
            fileName = value;
            LoadLottieFile();
            Invalidate();
        }
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var canvasBounds = e.Info.Rect;

        canvas.Clear(SKColors.Transparent);

        if (currentAnimation is null)
            return;

        // get progress
        var currentFrameTime = Environment.TickCount64;
        if (lastFrameTime == 0)
            lastFrameTime = currentFrameTime;
        var delta = TimeSpan.FromMilliseconds(currentFrameTime - lastFrameTime);
        lastFrameTime = currentFrameTime;
        progress += delta;

        // loop
        if (progress > currentAnimation.Duration)
            progress = TimeSpan.Zero;

        currentAnimation.SeekFrameTime(progress);

        currentAnimation.Render(canvas, canvasBounds);

        PostInvalidateDelayed(DelayMilliseconds);
    }

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        var size = currentAnimation?.Size ?? new SKSize(100, 100);
        var desiredWidth = (int)size.Width;
        var desiredHeight = (int)size.Height;

        //// extract the mode and size from the measure spec
        //var widthMode = MeasureSpec.GetMode(widthMeasureSpec);
        //var widthSize = MeasureSpec.GetSize(widthMeasureSpec);
        //var heightMode = MeasureSpec.GetMode(heightMeasureSpec);
        //var heightSize = MeasureSpec.GetSize(heightMeasureSpec);

        // set the measured dimensions based on the desired dimensions
        int measuredWidth = ResolveSize(desiredWidth, widthMeasureSpec);
        int measuredHeight = ResolveSize(desiredHeight, heightMeasureSpec);

        SetMeasuredDimension(measuredWidth, measuredHeight);
    }

    private void LoadLottieFile()
    {
        // reset current animation
        lastFrameTime = 0;
        progress = TimeSpan.Zero;
        if (currentAnimation is SkottieAnimation anim)
        {
            currentAnimation = null;
            anim.Dispose();
        }

        if (FileName is not string file || string.IsNullOrWhiteSpace(file))
            return;

        // load the new animation if there is any
        using var json = OpenFile(file);
        if (json is null)
            throw new FileLoadException($"Unable to load Lottie animation file \"{file}\".");

        var animation = SkottieAnimation.Create(json);
        if (animation is null)
            throw new FileLoadException($"Unable to parse Lottie animation file\"{file}\".");

        currentAnimation = animation;

        Invalidate();
    }

    private Stream? OpenFile(string file)
    {
        if (string.IsNullOrWhiteSpace(file))
            throw new ArgumentNullException(nameof(file));

        // try raw resources
        if (Resources is not null && Context is not null)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var id = Resources.GetIdentifier(name, "raw", Context.PackageName);
            if (id != 0)
                return Resources.OpenRawResource(id);
        }

        // try file system
        if (File.Exists(file))
            return File.OpenRead(file);

        // something is wrong
        return null;
    }
}
