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
        var window = new MainWindow(model, settings, new RegistrySettingsStore(@"Software\Pikvew.Tests\UI"), new FileOperations(settings), new MetadataReader())
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
            // Restore() runs on SourceInitialized and replaces the initializer's position.
            // Keep the test away from the real pointer so hover routing is deterministic.
            window.Left = -20000;
            window.Top = -20000;
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
            Assert.Equal(80, browser.Files.Count(f => !f.IsDirectory));
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
            IEnumerable<DirectoryNode> Branch(IEnumerable<DirectoryNode> nodes)
            {
                foreach (var node in nodes.Where(n => subdirectory.StartsWith(n.Path, StringComparison.OrdinalIgnoreCase) && n.Path.Length > 0))
                {
                    yield return node;
                    foreach (var child in Branch(node.Children)) yield return child;
                }
            }
            Assert.True(tree.SelectedItem is not null, "Current=" + browser.DirectoryPath + "; " + string.Join(" | ", Branch(browser.Roots).Select(n => $"{n.Name}: expanded={n.IsExpanded}, selected={n.IsSelected}, children={n.Children.Count}, container={FindTreeItem(tree, n.Path) is not null}")));
            Assert.Equal(subdirectory, ((DirectoryNode)tree.SelectedItem).Path);
            Assert.True(FindVisual<ScrollViewer>(tree)!.VerticalOffset > 0);
            var siblingPath = Path.Combine(directory, "folder498");
            var sibling = FindTreeItem(tree, siblingPath);
            Assert.NotNull(sibling);
            var treeScroll = FindVisual<ScrollViewer>(tree)!;
            treeScroll.ScrollToHorizontalOffset(60);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var horizontal = treeScroll.HorizontalOffset;
            var vertical = treeScroll.VerticalOffset;
            var moves = new List<double>();
            ScrollChangedEventHandler track = (_, change) => { if (change.HorizontalChange != 0) moves.Add(change.HorizontalChange); };
            treeScroll.ScrollChanged += track;
            var navigateTree = typeof(MainWindow).GetMethod("NavigateTreeAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            await (Task)navigateTree.Invoke(window, [TreeDirection.Up])!;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.Equal(siblingPath, browser.DirectoryPath);
            Assert.Equal(horizontal, treeScroll.HorizontalOffset, 2);
            Assert.Equal(vertical, treeScroll.VerticalOffset, 2);
            Assert.Empty(moves);
            treeScroll.ScrollChanged -= track;
            sibling.IsSelected = true;
            sibling.Focus();
            await Task.Delay(150);
            Assert.Equal(siblingPath, browser.DirectoryPath);
            Assert.Equal(siblingPath, ((DirectoryNode)tree.SelectedItem).Path);
            Assert.True(tree.IsKeyboardFocusWithin);
            Assert.Empty(browser.Files);
            await window.OpenArgumentsAsync([directory]);
            Assert.Equal(80, browser.Files.Count(f => !f.IsDirectory));
            Assert.True((await WindowsShell.GetFolderIconAsync()).PixelWidth >= 128);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            list.SelectedItems.Clear();
            list.SelectedItem = browser.Files.First(f => f.Path == siblingPath);
            window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            await Task.Delay(100);
            Assert.Equal(siblingPath, browser.DirectoryPath);
            Assert.False(model.IsViewing);
            await (Task)typeof(MainWindow).GetMethod("GoUpAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, null)!;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.Equal(directory, browser.DirectoryPath);

            Assert.Equal(siblingPath, ((FileItem)list.SelectedItem).Path);
            Capture((FrameworkElement)window.Content, Path.Combine(screenshotRoot, "browser-folders.png"));
            await (Task)typeof(MainWindow).GetMethod("GoHistoryAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, [-1])!;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.Equal(siblingPath, browser.DirectoryPath);
            await (Task)typeof(MainWindow).GetMethod("GoHistoryAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, [1])!;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.Equal(directory, browser.DirectoryPath);

            // F2 edits just the filename stem in place and preserves selection after refresh.
            var originalFile = browser.Files.First(file => file.Path == path);
            list.SelectedItems.Clear();
            list.SelectedItem = originalFile;
            list.ScrollIntoView(originalFile);
            list.UpdateLayout();
            list.Focus();
            window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, Key.F2) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            var fileLabel = FindVisual<RenameLabel>((DependencyObject)list.ItemContainerGenerator.ContainerFromItem(originalFile)!)!;
            Assert.True(fileLabel.IsEditing);
            var editor = fileLabel.Children.OfType<TextBox>().Single();
            Assert.Equal("1", editor.SelectedText);
            editor.Text = "renamed.gif";
            await fileLabel.CommitAsync();
            Assert.True(File.Exists(Path.Combine(directory, "renamed.gif")));
            Assert.Equal(Path.Combine(directory, "renamed.gif"), ((FileItem)list.SelectedItem).Path);
            list.UpdateLayout();
            var renamedFileLabel = FindVisual<RenameLabel>((DependencyObject)list.ItemContainerGenerator.ContainerFromItem(list.SelectedItem)!)!;
            renamedFileLabel.BeginEdit();
            renamedFileLabel.Children.OfType<TextBox>().Single().Text = "1.gif";
            await renamedFileLabel.CommitAsync();
            Assert.True(File.Exists(path));

            // Renaming the currently opened directory in the tree remaps the browser path.
            await window.OpenArgumentsAsync([siblingPath]);
            var directoryNode = (DirectoryNode)tree.SelectedItem;
            var directoryItem = FindTreeItem(tree, directoryNode.Path)!;
            Assert.True(directoryItem.Focus(), $"Cannot focus tree item: {Keyboard.FocusedElement}");
            window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, Key.F2) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var directoryLabel = FindVisual<RenameLabel>(FindTreeItem(tree, directoryNode.Path)!)!;
            Assert.True(directoryLabel.IsEditing, $"Focus={Keyboard.FocusedElement}, tree focus={tree.IsKeyboardFocusWithin}, tree hover={tree.IsMouseOver}, files hover={list.IsMouseOver}, browser={browser.DirectoryPath}, viewing={model.IsViewing}");
            directoryLabel.Children.OfType<TextBox>().Single().Text = "renamed-folder";
            await directoryLabel.CommitAsync();
            Assert.Equal(Path.Combine(directory, "renamed-folder"), browser.DirectoryPath);
            Assert.True(Directory.Exists(browser.DirectoryPath));
            directoryLabel = FindVisual<RenameLabel>(FindTreeItem(tree, browser.DirectoryPath)!)!;
            directoryLabel.BeginEdit();
            directoryLabel.Children.OfType<TextBox>().Single().Text = "folder498";
            await directoryLabel.CommitAsync();
            Assert.Equal(siblingPath, browser.DirectoryPath);
            await window.OpenArgumentsAsync([directory]);

            foreach (var permanent in new[] { false, true })
            {
                var defaultConfirm = false;
                double iconWidth = 0;
                _ = Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                {
                    var dialog = window.OwnedWindows.Cast<Window>().Single();
                    var content = (DockPanel)((Border)dialog.Content).Child;
                    var buttons = content.Children.OfType<StackPanel>().Single().Children.OfType<Button>().ToArray();
                    defaultConfirm = buttons[0].IsDefault && !buttons[0].IsCancel && buttons[1].IsCancel && !buttons[1].IsDefault;
                    iconWidth = content.Children.OfType<Grid>().Single().Width;
                    Capture(dialog, Path.Combine(screenshotRoot, permanent ? "delete-permanent.png" : "delete-recycle.png"));
                    dialog.DialogResult = false;
                }, DispatcherPriority.Loaded);
                Assert.False(DeleteConfirmation.Show(window, 1, "example.jpg", permanent));
                Assert.True(defaultConfirm);
                Assert.Equal(permanent ? 64 : 46, iconWidth);
            }

            await model.ShowImageAsync(path);
            window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, Key.I) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            await Task.Delay(100);
            var metadataPanel = (FrameworkElement)window.FindName("MetadataPanel");
            Assert.True(metadataPanel.IsVisible);
            Assert.NotEmpty(((DataGrid)window.FindName("MetadataTable")).Items);
            Capture((FrameworkElement)window.Content, Path.Combine(screenshotRoot, "metadata.png"));
            var infoPin = (CheckBox)window.FindName("InfoPin");
            infoPin.IsChecked = true;
            infoPin.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(settings.MetadataPinned);
            window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, Key.I) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Assert.False(metadataPanel.IsVisible);
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
        if (Environment.GetEnvironmentVariable("PIKVEW_UI_SCREENSHOTS") != "1") return;
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
