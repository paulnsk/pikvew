using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ImageMagick;
using Pikvew.App;
using Pikvew.App.Browser;
using Pikvew.App.Controls;
using Pikvew.App.Imaging;
using Pikvew.App.Infrastructure;
using Pikvew.Core;
using Xunit;

namespace Pikvew.Tests;

public sealed class UiSmokeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(80)]
    public Task FolderTreeKeepsHorizontalPositionWhileExpandingAndRevealing(double offset) => RunStaAsync(async () =>
    {
        var tree = new TreeView
        {
            ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(DirectoryTreePanel)))
        };
        VirtualizingPanel.SetIsVirtualizing(tree, true);
        for (var i = 0; i < 120; i++) tree.Items.Add(new TreeViewItem { Header = $"Folder {i} " + new string('w', 110) });
        var branch = (TreeViewItem)tree.Items[0];
        branch.Items.Add(new TreeViewItem { Header = "Child folder" });
        var selected = (TreeViewItem)tree.Items[1];
        selected.IsSelected = true;
        var window = new Window { Content = tree, Width = 260, Height = 240, ShowActivated = false, ShowInTaskbar = false, Left = -20000, Top = -20000 };
        try
        {
            window.Show();
            window.UpdateLayout();
            var scroll = FindVisual<ScrollViewer>(tree)!;
            Assert.True(scroll.ScrollableWidth > offset);
            scroll.ScrollToHorizontalOffset(offset);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var horizontalMoves = new List<double>();
            scroll.ScrollChanged += (_, e) => { if (e.HorizontalChange != 0) horizontalMoves.Add(e.HorizontalChange); };

            // Expanding another branch must leave both the selection and horizontal view alone.
            branch.IsExpanded = true;
            branch.BringIntoView();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.Same(selected, tree.SelectedItem);
            Assert.Equal(offset, scroll.HorizontalOffset, 2);

            var panel = FindVisual<DirectoryTreePanel>(tree)!;
            panel.Realize(110);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var distant = (TreeViewItem)tree.Items[110];
            distant.IsSelected = true;
            distant.BringIntoView();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.True(scroll.VerticalOffset > 0);
            Assert.Equal(offset, scroll.HorizontalOffset, 2);
            Assert.Empty(horizontalMoves); // Catch intermediate jumps, even if the final offset was restored.
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task BrowserViewerAnimationAndFullscreenWorkTogether() => RunStaAsync(async () =>
    {
        var application = new App.App();
        application.InitializeComponent();
        var directory = Path.Combine(Path.GetTempPath(), "PikvewUI-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var settings = new UserSettings { LastDirectory = directory };
        var loader = new ImageLoader();
        var cache = new ThumbnailCache(loader);
        var browser = new BrowserViewModel(loader, cache, settings, new FileCatalog());
        var playback = new PlaybackController();
        var model = new MainViewModel(loader, settings, browser, playback, new ImagePreloader(loader));
        var window = new MainWindow(model, settings, new RegistrySettingsStore(@"Software\Pikvew.Tests\UI"), new FileOperations(settings))
        { ShowActivated = false, ShowInTaskbar = false, Left = -20000, Top = -20000 };
        try
        {
            var path = Path.Combine(directory, "1.gif");
            using (var frames = new MagickImageCollection())
            {
                frames.Add(new MagickImage(MagickColors.Red, 32, 24) { AnimationDelay = 10 });
                frames.Add(new MagickImage(MagickColors.Blue, 32, 24) { AnimationDelay = 30 });
                frames.Write(path);
            }
            for (var i = 2; i <= 80; i++) File.Copy(path, Path.Combine(directory, $"{i}.gif"));
            window.Show();
            await window.InitializeAsync([path]);
            Assert.True(model.IsViewing);
            Assert.True(playback.IsAnimated);
            Assert.Equal(EscapeAction.CloseApplication, model.Escape);
            await Task.Delay(100);
            var canvas = (ImageViewport)window.FindName("ImageCanvas");
            Assert.NotNull(canvas.Source);
            Assert.Equal(1, canvas.Scale);
            playback.Toggle();
            playback.Seek(0.2);
            await Task.Delay(100);
            Assert.Equal(1, playback.CurrentFrame);
            playback.Step(-1);
            Assert.Equal(0, playback.CurrentFrame);
            canvas.ActualSize();
            Assert.Equal(1, canvas.Scale);
            canvas.Fit();
            Assert.True(canvas.Scale > 1);

            window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, Key.Enter)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            await Task.Delay(100);
            Assert.False(model.IsViewing);
            Assert.Equal(80, browser.Files.Count);
            var list = (ListBox)window.FindName("Files");
            list.SelectedIndex = 0;
            window.UpdateLayout();
            Assert.NotNull(list.ItemContainerGenerator.ContainerFromIndex(0));
            Assert.Null(list.ItemContainerGenerator.ContainerFromIndex(79));
            var screenshotRoot = Path.Combine(InternetCorpusTests.SampleDirectory, "..", "screenshots");
            Directory.CreateDirectory(screenshotRoot);
            Capture((FrameworkElement)window.Content, Path.Combine(screenshotRoot, "browser.png"));

            // A real selection event must navigate once and retain keyboard focus in the tree.
            for (var folder = 0; folder < 500; folder++) Directory.CreateDirectory(Path.Combine(directory, $"folder{folder:D3}"));
            var subdirectory = Path.Combine(directory, "folder499");
            await window.OpenArgumentsAsync([subdirectory]);
            await Task.Delay(150);
            var tree = (TreeView)window.FindName("FolderTree");
            Assert.Equal(subdirectory, ((DirectoryNode)tree.SelectedItem).Path);
            Assert.True(FindVisual<ScrollViewer>(tree)!.VerticalOffset > 0);
            var siblingPath = Path.Combine(directory, "folder498");
            var sibling = FindTreeItem(tree, siblingPath);
            Assert.NotNull(sibling);
            sibling.IsSelected = true;
            sibling.Focus();
            await Task.Delay(150);
            Assert.Equal(siblingPath, browser.DirectoryPath);
            Assert.Equal(siblingPath, ((DirectoryNode)tree.SelectedItem).Path);
            Assert.True(tree.IsKeyboardFocusWithin);
            Assert.Empty(browser.Files);
            await window.OpenArgumentsAsync([directory]);
            Assert.Equal(80, browser.Files.Count);

            await model.ShowImageAsync(path);
            Assert.Equal(EscapeAction.ShowBrowser, model.Escape);
            window.UpdateLayout();
            Capture((FrameworkElement)window.Content, Path.Combine(screenshotRoot, "viewer-windowed.png"));
            settings.Fullscreen = true;
            await window.OpenArgumentsAsync([path]);
            Assert.True(model.IsViewing);
            Assert.Equal(WindowStyle.None, window.WindowStyle);
            Capture((FrameworkElement)window.Content, Path.Combine(screenshotRoot, "viewer.png"));
            Assert.Equal(EscapeAction.CloseApplication, model.Escape);
            await model.NavigateAsync(1);
            Assert.Equal("2.gif", model.FileName);
            Assert.True(canvas.Scale > 1); // * preference applies to subsequent small images.
            await model.NavigateAsync(0, last: true);
            Assert.Equal("80.gif", model.FileName);
            await model.NavigateAsync(1);
            Assert.Equal("80.gif", model.FileName);
            var unsupported = Path.Combine(directory, "notes.txt");
            await File.WriteAllTextAsync(unsupported, "not an image");
            await window.OpenArgumentsAsync([unsupported]);
            Assert.Null(model.Playback.Document);
            var viewer = (FrameworkElement)window.FindName("ViewerRoot");
            var menu = viewer.ContextMenu;
            Assert.NotNull(menu);
            menu.PlacementTarget = viewer;
            menu.IsOpen = true;
            await Task.Delay(50);
            Assert.False(((MenuItem)menu.Items[0]).IsEnabled);
            Assert.True(((MenuItem)menu.Items[2]).IsEnabled);
            Capture(menu, Path.Combine(screenshotRoot, "menu.png"));
            menu.IsOpen = false;
        }
        finally
        {
            window.Close();
            await model.DisposeAsync();
            Directory.Delete(directory, true);
            application.Shutdown();
        }
    });

    private static T? FindVisual<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            if (FindVisual<T>(child) is { } found) return found;
        }
        return null;
    }

    private static TreeViewItem? FindTreeItem(DependencyObject root, string path)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TreeViewItem { DataContext: DirectoryNode node } item && node.Path == path) return item;
            if (FindTreeItem(child, path) is { } found) return found;
        }
        return null;
    }

    private static void Capture(FrameworkElement element, string path)
    {
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap(Math.Max(1, (int)element.ActualWidth), Math.Max(1, (int)element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    private static Task RunStaAsync(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            Dispatcher.CurrentDispatcher.InvokeAsync(async () =>
            {
                try { await action(); completion.TrySetResult(); }
                catch (Exception exception) { completion.TrySetException(exception); }
                finally { Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(45));
    }
}
