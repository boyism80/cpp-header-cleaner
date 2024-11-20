using System.Text.RegularExpressions;

namespace cpp_header_cleaner
{
    public class Node
    {
        private static readonly Regex _includeFileRegEx = new Regex("\\s*#include\\s*[<\"\"](?<path>.+)[>\"\"]");
        private bool _isRoot = false;
        private Dictionary<string, Node> _children = new Dictionary<string, Node>();

        public string Path { get; private set; }
        public string RelativeDir { get; private set; }
        public string FileName => System.IO.Path.GetFileName(Path);
        private string RelativePath => System.IO.Path.Join(RelativeDir, FileName);

        private Node()
        { }

        private static string GetRelationPath(string path, string root)
        {
            return System.IO.Path.GetDirectoryName(path[(root.Length + 1)..]) ?? string.Empty;
        }

        private Node? FindCircularDependency()
        {
            var queue = new Queue<Node>();
            foreach (var child in _children.Values)
            {
                queue.Enqueue(child);
            }
            while (queue.TryDequeue(out var node))
            {
                if (node == this)
                    return node;

                foreach (var child in node._children.Values)
                {
                    queue.Enqueue(child);
                }
            }

            return null;
        }

        private IEnumerable<Node> InternalScan(Action<Node, IReadOnlyList<Node>> callback, Dictionary<Node, HashSet<Node>> dp)
        {
            if (dp.TryGetValue(this, out var children))
            {
                return children;
            }

            var allHeaderFiles = new List<Node>(_children.Values);
            foreach (var child in _children.Values)
            {
                allHeaderFiles.AddRange(child.InternalScan(callback, dp));
            }

            if (_isRoot)
                return [];

            var duplicatedHeaderFiles = allHeaderFiles.GroupBy(x => x).Where(x => x.Count() > 1).Select(x => x.First()).ToList();
            var found = _children.Values.Intersect(duplicatedHeaderFiles).ToList();
            if (found.Count != 0)
                callback(this, found);

            dp.Add(this, allHeaderFiles.Distinct().Except(found).ToHashSet());
            return dp[this];
        }

        public void Scan(Action<Node, IReadOnlyList<Node>> callback)
        {
            InternalScan(callback, new Dictionary<Node, HashSet<Node>>());
        }

        private static string? FindAbsoluteFilePath(string fileName, string rootDir, IEnumerable<string> includePaths)
        {
            foreach (var includePath in includePaths)
            {
                var fullPath = System.IO.Path.GetFullPath(System.IO.Path.Join(rootDir, includePath, fileName));
                if (File.Exists(fullPath))
                    return fullPath;
            }

            return null;
        }

        private static Node? FindNode(IReadOnlyDictionary<string, Node> nodes, string fileName, string rootDir, IEnumerable<string> includeDirs)
        {
            if (nodes.TryGetValue(fileName, out var node))
            {
                return node;
            }

            if (nodes.TryGetValue(System.IO.Path.Join(rootDir, fileName).Replace('/', '\\'), out node))
            {
                return node;
            }

            foreach (var includePath in includeDirs)
            {
                var fullPath = System.IO.Path.GetFullPath(System.IO.Path.Join(rootDir, includePath, fileName));
                if (nodes.TryGetValue(fullPath, out node))
                    return node;
            }

            return null;
        }

        public static Node Build(string dir, IReadOnlyList<string> includeDirs, IReadOnlyList<string> excludeDirs)
        {
            dir = System.IO.Path.GetFullPath(dir);
            var files = Directory.GetFiles(dir, "*.h", SearchOption.AllDirectories);
            var relations = new Dictionary<string, List<string>>();
            foreach (var absFilePath in files)
            {
                var relationDir = GetRelationPath(absFilePath, dir);
                if (excludeDirs.Any(relationDir.StartsWith))
                    continue;

                relations[absFilePath] = new List<string>();

                foreach (Match match in _includeFileRegEx.Matches(File.ReadAllText(absFilePath)))
                {
                    var includeFileName = match.Groups["path"].Value;
                    var absChildPath = FindAbsoluteFilePath(includeFileName, dir, includeDirs.Concat([relationDir]));
                    if (absChildPath != null)
                    {
                        relations[absFilePath].Add(absChildPath);
                    }
                    else
                    {
                        relations[absFilePath].Add(includeFileName);
                    }
                }
            }

            var nodes = new Dictionary<string, Node>();
            foreach (var (k, v) in relations)
            {
                nodes[k] = new Node
                {
                    Path = k,
                    RelativeDir = System.IO.Path.GetDirectoryName(k[(dir.Length + 1)..]) ?? string.Empty
                };
            }

            var notRoot = new HashSet<string>();
            foreach (var (currentFilePath, includeFilenames) in relations)
            {
                var relationDir = GetRelationPath(currentFilePath, dir);
                foreach (var includeFileName in includeFilenames)
                {
                    var node = FindNode(nodes, includeFileName, dir, includeDirs.Concat([relationDir]));
                    if (node == null)
                    {
                        node = new Node
                        {
                            Path = includeFileName,
                            RelativeDir = string.Empty,
                        };
                        nodes.Add(node.Path, node);
                    }

                    nodes[currentFilePath]._children.Add(includeFileName, node);
                    var circularDependency = nodes[currentFilePath].FindCircularDependency();
                    if (circularDependency != null)
                        throw new Exception($"circular dependency detected. {node.RelativePath} <-> {circularDependency.RelativePath}");

                    notRoot.Add(node.Path);
                }
            }

            foreach (var path in notRoot)
            {
                nodes.Remove(path);
            }

            return new Node
            {
                Path = string.Empty,
                RelativeDir = string.Empty,
                _children = nodes,
                _isRoot = true
            };
        }

        public override string ToString()
        {
            return System.IO.Path.Join(RelativeDir, FileName);
        }
    }
}
