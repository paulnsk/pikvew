using System.Windows;
using System.Windows.Controls;
using Pikvew.App.Browser;

namespace Pikvew.App.Controls;

public sealed class ThumbnailImage : Image
{
    private CancellationTokenSource? _request;
    public ThumbnailImage()
    {
        Loaded += (_, _) => Load();
        Unloaded += (_, _) => { Cancel(); Source = null; };
        DataContextChanged += (_, _) => { if (IsLoaded) Load(); };
    }

    private void Cancel() { _request?.Cancel(); _request?.Dispose(); _request = null; }
    private async void Load()
    {
        Cancel();
        Source = null;
        if (DataContext is not FileItem { Supported: true } item) return;
        var request = _request = new CancellationTokenSource();
        var token = request.Token;
        try
        {
            var image = await item.ThumbnailAsync(token);
            if (!token.IsCancellationRequested) Source = image;
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            if (!token.IsCancellationRequested) ToolTip = exception.Message;
        }
    }
}
