using System.Windows;
using System.Windows.Controls;
using Pikvew.App.Infrastructure;

namespace Pikvew.App.Controls;

public sealed class FolderIcon : Image
{
    public FolderIcon()
    {
        Loaded += async (_, _) =>
        {
            try { var icon = await WindowsShell.GetFolderIconAsync(); if (IsLoaded) Source = icon; }
            catch (Exception exception) { System.Diagnostics.Trace.TraceInformation("Folder icon: {0}", exception.Message); }
        };
        Unloaded += (_, _) => Source = null;
    }
}
