using System.Windows.Controls;

namespace Pikvew.App.Controls;

public sealed class DirectoryTreePanel : VirtualizingStackPanel
{
    public void Realize(int index) => BringIndexIntoView(index);
}
