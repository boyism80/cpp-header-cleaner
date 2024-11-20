using cpp_header_cleaner;
using NDesk.Options;

try
{
    var dir = string.Empty;
    var includeDirs = new List<string>();
    var excludeDirs = new List<string>();
    var options = new OptionSet
    {
        { "p|path=", "input directory", v => dir = v },
        { "i|include_dirs=", "include directories", v => includeDirs = [.. v.Split('|')] },
        { "e|exclude_dirs=", "exclude directories", v => excludeDirs = [.. v.Split('|')] },
    };
    options.Parse(args);

    var root = Node.Build(dir, includeDirs, excludeDirs);
    root.Scan((parent, children) =>
    {
        Console.WriteLine(parent.Path);
        foreach (var child in children)
        {
            if (parent.RelativeDir == child.RelativeDir)
            {
                Console.WriteLine($" - {child.FileName}");
            }
            else if (string.IsNullOrEmpty(child.RelativeDir))
            {
                Console.WriteLine($" - {child.Path}");
            }
            else
            {
                Console.WriteLine($" - {Path.Join(child.RelativeDir, child.FileName).Replace('\\', '/')}");
            }
        }
        Console.WriteLine();
    });
}
catch (Exception e)
{
    Console.WriteLine($"error : {e.Message}");
}
