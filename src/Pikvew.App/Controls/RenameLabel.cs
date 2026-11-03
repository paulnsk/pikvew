using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Pikvew.App.Infrastructure;

namespace Pikvew.App.Controls;

public sealed class RenameRequestEventArgs(string name) : EventArgs
{
    public string Name { get; } = name;
    public Task? Operation { get; set; }
}

/// <summary>Explorer-style label editing, with filesystem work supplied by the owner.</summary>
public sealed class RenameLabel : Grid
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(RenameLabel), new PropertyMetadata("", (d, _) => ((RenameLabel)d).RefreshText()));
    public static readonly DependencyProperty IsCurrentProperty = DependencyProperty.Register(nameof(IsCurrent), typeof(bool), typeof(RenameLabel), new PropertyMetadata(false));
    public static readonly DependencyProperty IsDirectoryProperty = DependencyProperty.Register(nameof(IsDirectory), typeof(bool), typeof(RenameLabel), new PropertyMetadata(false));
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool IsCurrent { get => (bool)GetValue(IsCurrentProperty); set => SetValue(IsCurrentProperty, value); }
    public bool IsDirectory { get => (bool)GetValue(IsDirectoryProperty); set => SetValue(IsDirectoryProperty, value); }
    public bool IsEditing => _editor.Visibility == Visibility.Visible;
    public event EventHandler<RenameRequestEventArgs>? RenameRequested;
    private readonly TextBlock _label = new() { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _editor = new() { Visibility = Visibility.Collapsed, Padding = new Thickness(1), MinWidth = 35 };
    private readonly DispatcherTimer _slowClick = new() { Interval = InputTiming.DoubleClickTime };
    private bool _selectedOnPress;
    private bool _committing;

    public RenameLabel()
    {
        Children.Add(_label);
        Children.Add(_editor);
        _slowClick.Tick += (_, _) => { _slowClick.Stop(); if (IsCurrent && IsLoaded) BeginEdit(); };
        _label.MouseLeftButtonDown += (_, e) => { _slowClick.Stop(); _selectedOnPress = IsCurrent && e.ClickCount == 1; };
        _label.MouseLeftButtonUp += (_, e) => { if (_selectedOnPress && e.ClickCount == 1) _slowClick.Start(); };
        _editor.PreviewKeyDown += async (_, e) =>
        {
            if (e.Key == Key.Escape) { e.Handled = true; CancelEdit(); }
            else if (e.Key == Key.Enter) { e.Handled = true; await CommitAsync(); }
        };
        _editor.LostKeyboardFocus += async (_, _) => { if (IsEditing && !_committing) await CommitAsync(); };
        Unloaded += (_, _) => { _slowClick.Stop(); CancelEdit(); };
    }

    private void RefreshText() => _label.Text = Text;
    public void BeginEdit()
    {
        if (_committing) return;
        _slowClick.Stop();
        _editor.Text = Text;
        _editor.ToolTip = null;
        _editor.ClearValue(Control.BorderBrushProperty);
        _label.Visibility = Visibility.Collapsed;
        _editor.Visibility = Visibility.Visible;
        _editor.Focus();
        var stem = System.IO.Path.GetFileNameWithoutExtension(Text).Length;
        _editor.Select(0, IsDirectory || stem == 0 ? Text.Length : stem);
    }
    public void CancelEdit()
    {
        _slowClick.Stop();
        _editor.Visibility = Visibility.Collapsed;
        _label.Visibility = Visibility.Visible;
    }
    public async Task CommitAsync()
    {
        if (!IsEditing || _committing) return;
        if (string.Equals(_editor.Text, Text, StringComparison.Ordinal)) { CancelEdit(); return; }
        _committing = true;
        _editor.IsReadOnly = true;
        try
        {
            var args = new RenameRequestEventArgs(_editor.Text);
            RenameRequested?.Invoke(this, args);
            if (args.Operation is not null) await args.Operation;
            CancelEdit();
        }
        catch (Exception exception)
        {
            if (IsLoaded)
            {
                _editor.ToolTip = exception.Message;
                _editor.BorderBrush = Brushes.IndianRed;
                _editor.Focus();
            }
        }
        finally { _committing = false; _editor.IsReadOnly = false; }
    }
}
