namespace Pikvew.App.Browser;

public enum TreeDirection { Up, Down, Left, Right }

public static class DirectoryNavigation
{
    public static async Task<DirectoryNode?> MoveAsync(IEnumerable<DirectoryNode> roots, string currentPath, TreeDirection direction)
    {
        var visible = new List<(DirectoryNode Node, DirectoryNode? Parent)>();
        void Add(IEnumerable<DirectoryNode> nodes, DirectoryNode? owner)
        {
            foreach (var node in nodes.Where(n => n.Path.Length > 0))
            {
                visible.Add((node, owner));
                if (node.IsExpanded) Add(node.Children, node);
            }
        }
        Add(roots, null);
        if (visible.Count == 0) return null;
        var index = visible.FindIndex(n => string.Equals(n.Node.Path, currentPath, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return visible[0].Node;
        var (current, parent) = visible[index];
        switch (direction)
        {
            case TreeDirection.Up: return visible[Math.Max(0, index - 1)].Node;
            case TreeDirection.Down: return visible[Math.Min(visible.Count - 1, index + 1)].Node;
            case TreeDirection.Left:
                if (current.IsExpanded) { current.IsExpanded = false; return current; }
                return parent ?? current;
            case TreeDirection.Right:
                var wasExpanded = current.IsExpanded;
                await current.LoadAsync();
                var first = current.Children.FirstOrDefault(n => n.Path.Length > 0);
                if (first is null) return current;
                current.IsExpanded = true;
                return wasExpanded ? first : current;
            default: return current;
        }
    }
}
