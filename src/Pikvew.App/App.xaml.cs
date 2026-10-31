using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pikvew.App.Browser;
using Pikvew.App.Imaging;
using Pikvew.App.Infrastructure;

namespace Pikvew.App;

public partial class App
{
    private IHost? _host;
    private bool _isStopping;
    private SingleInstance? _instance;
    private ThemeManager? _theme;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var arguments = e.Args.Select(Path.GetFullPath).ToArray();
            _instance = new SingleInstance();
            if (!_instance.IsPrimary)
            {
                await _instance.ForwardAsync(arguments);
                Shutdown();
                return;
            }
            _theme = new ThemeManager(this);
            // Do not read appsettings.json or use the caller's working directory
            // for configuration. User preferences will live in HKCU\Software\Pikvew.
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                Args = e.Args,
                DisableDefaults = true
            });

            builder.Services.AddSingleton<MainWindow>();
            builder.Services.AddSingleton<RegistrySettingsStore>();
            builder.Services.AddSingleton(services => services.GetRequiredService<RegistrySettingsStore>().Load());
            builder.Services.AddSingleton<ImageLoader>();
            builder.Services.AddSingleton<ThumbnailCache>();
            builder.Services.AddSingleton<ImagePreloader>();
            builder.Services.AddSingleton<BrowserViewModel>();
            builder.Services.AddSingleton<IFileCatalog, FileCatalog>();
            builder.Services.AddSingleton<PlaybackController>();
            builder.Services.AddSingleton<MainViewModel>();
            builder.Services.AddSingleton<FileOperations>();

            _host = builder.Build();
            await _host.StartAsync();

            var window = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Closed += OnMainWindowClosed;
            _instance.OpenRequested += args => Dispatcher.InvokeAsync(() => window.OpenArgumentsAsync(args));
            _ = _instance.ListenAsync();
            window.Show();
            await window.InitializeAsync(arguments);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Pikvew could not start",
                MessageBoxButton.OK, MessageBoxImage.Error);
            await StopApplicationAsync(1);
        }
    }

    private async void OnMainWindowClosed(object? sender, EventArgs e)
    {
        await StopApplicationAsync(0);
    }

    private async Task StopApplicationAsync(int exitCode)
    {
        if (_isStopping)
            return;

        _isStopping = true;

        try
        {
            if (_host is not null)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _host.StopAsync(timeout.Token);
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError("Pikvew shutdown failed: {0}", exception);
            exitCode = 1;
        }
        finally
        {
            if (_host is IAsyncDisposable asyncHost) await asyncHost.DisposeAsync();
            else _host?.Dispose();
            _host = null;
            Shutdown(exitCode);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        _theme?.Dispose();
        base.OnExit(e);
    }

}
