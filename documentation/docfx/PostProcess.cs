// Turns DocFX's markdown API output into a self-contained, GitHub-browsable documentation folder.
//
//   docfx metadata docfx.json          -> api/*.md   (raw DocFX output)
//   dotnet run PostProcess.cs          -> _site/     (what the Documentation workflow publishes)
//
// DocFX's markdown output format leaves every <see cref="..."/> from the XML comments as an
// unresolved <xref href="UID"></xref> tag, which GitHub renders as nothing, so the referenced name
// silently disappears from the sentence. This tool:
//   1. Rewrites each <xref> into a markdown link: to the page (and heading anchor) for types and
//      members in this solution, to learn.microsoft.com for System.* / Microsoft.* types, and to
//      inline code for anything else.
//   2. Writes _site/README.md from landing-page.md, replacing <!-- namespaces --> with a link to
//      every namespace page, and copies ../images (if present) so images on the landing page resolve.
//   3. Fails (exit code 1) if any <xref> is left unresolved or any relative .md link or anchor is
//      broken, so the workflow never publishes a page with dead links.
//
// Usage: dotnet run PostProcess.cs [-- <apiDir> <landingTemplate> <imagesDir> <outputDir>]
// Defaults: api  landing-page.md  ../images  _site   (relative to the current directory)

#:property TreatWarningsAsErrors=true

using System.Text.RegularExpressions;

var apiDir = args.ElementAtOrDefault(0) ?? "api";
var landingTemplate = args.ElementAtOrDefault(1) ?? "landing-page.md";
var imagesDir = args.ElementAtOrDefault(2) ?? Path.Combine("..", "images");
var outputDir = args.ElementAtOrDefault(3) ?? "_site";

if (!Directory.Exists(apiDir))
{
    Console.Error.WriteLine($"error: '{apiDir}' not found. Run 'docfx metadata docfx.json' first.");
    return 1;
}

var pages = Directory.GetFiles(apiDir, "*.md")
    .ToDictionary(p => Path.GetFileName(p), File.ReadAllText, StringComparer.Ordinal);

var headingRegex = new Regex(@"^(#+) <a id=""([^""]+)""></a> (.+?)\r?$", RegexOptions.Multiline);
var anchors = new Dictionary<string, Heading>(StringComparer.Ordinal);
foreach (var (file, text) in pages)
{
    foreach (Match m in headingRegex.Matches(text))
    {
        anchors.TryAdd(m.Groups[2].Value, new Heading(file, m.Groups[3].Value, IsPage: m.Groups[1].Value == "#"));
    }
}

var errors = new List<string>();

// 1. Resolve <xref> tags.
var xrefRegex = new Regex(@"<xref href=""([^""]*)""[^>]*?(?:/>|>(.*?)</xref>)", RegexOptions.Singleline);
var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
foreach (var file in pages.Keys.ToList())
{
    pages[file] = xrefRegex.Replace(pages[file], m => ResolveXref(Uri.UnescapeDataString(m.Groups[1].Value), m.Groups[2].Value));
    if (pages[file].Contains("<xref", StringComparison.Ordinal))
    {
        errors.Add($"{file}: unresolved <xref> tag remains");
    }
}

// 2. Write the output folder: api pages (DocFX's toc.yml is only meaningful to a DocFX site).
if (Directory.Exists(outputDir))
{
    Directory.Delete(outputDir, recursive: true);
}
var outputApiDir = Path.Combine(outputDir, "api");
Directory.CreateDirectory(outputApiDir);
foreach (var (file, text) in pages)
{
    File.WriteAllText(Path.Combine(outputApiDir, file), text);
}

if (Directory.Exists(imagesDir))
{
    var outputImagesDir = Path.Combine(outputDir, "images");
    Directory.CreateDirectory(outputImagesDir);
    foreach (var image in Directory.GetFiles(imagesDir))
    {
        File.Copy(image, Path.Combine(outputImagesDir, Path.GetFileName(image)));
    }
}

var namespaceList = string.Join('\n', anchors.Values
    .Where(h => h.IsPage && h.Title.StartsWith("Namespace ", StringComparison.Ordinal))
    .Select(h => (Name: StripKind(h.Title), h.File))
    .OrderBy(n => n.Name, StringComparer.Ordinal)
    .Select(n => $"- [{n.Name}](api/{n.File})"));

var landing = File.Exists(landingTemplate)
    ? File.ReadAllText(landingTemplate).Replace("<!-- namespaces -->", namespaceList, StringComparison.Ordinal)
    : $"# API Reference\n\n{namespaceList}\n";
File.WriteAllText(Path.Combine(outputDir, "README.md"), landing);

// 3. Validate every relative .md link (DocFX backslash-escapes punctuation in link targets).
var linkRegex = new Regex(@"\]\(((?:[^()\s\\]|\\.)+?\.md)(?:#([^)\s]+))?\)");
foreach (var path in Directory.GetFiles(outputDir, "*.md", SearchOption.AllDirectories))
{
    var baseDir = Path.GetDirectoryName(path)!;
    foreach (Match m in linkRegex.Matches(File.ReadAllText(path)))
    {
        var target = Regex.Replace(m.Groups[1].Value, @"\\(.)", "$1");
        if (target.Contains("://", StringComparison.Ordinal))
        {
            continue;
        }
        var targetPath = Path.GetFullPath(Path.Combine(baseDir, target));
        if (!File.Exists(targetPath))
        {
            errors.Add($"{Path.GetRelativePath(outputDir, path)}: broken link to {target}");
        }
        else if (m.Groups[2].Success && !(anchors.TryGetValue(m.Groups[2].Value, out var h) && h.File == Path.GetFileName(targetPath)))
        {
            errors.Add($"{Path.GetRelativePath(outputDir, path)}: broken anchor {target}#{m.Groups[2].Value}");
        }
    }
}

foreach (var error in errors)
{
    Console.Error.WriteLine($"error: {error}");
}
if (errors.Count > 0)
{
    return 1;
}

Console.WriteLine($"Wrote {pages.Count} API pages, README.md, and images to {outputDir}/ ({resolved.Count} distinct cross-references resolved).");
return 0;

string ResolveXref(string uid, string innerText)
{
    var link = resolved.TryGetValue(uid, out var cached) ? cached : resolved[uid] = BuildLink(uid);
    if (string.IsNullOrWhiteSpace(innerText))
    {
        return link;
    }

    // <see cref="X">custom text</see>: keep the author's text, reuse the link target if there is one.
    var target = Regex.Match(link, @"\]\(([^)]+)\)$");
    return target.Success ? $"[{innerText}]({target.Groups[1].Value})" : innerText;
}

string BuildLink(string uid)
{
    // Exact match: a page (type/namespace) or a member heading in this solution.
    if (anchors.TryGetValue(AnchorOf(uid), out var heading))
    {
        return heading.IsPage
            ? $"[{StripKind(heading.Title)}]({heading.File})"
            : $"[{OwnerName(heading.File)}.{MemberName(heading.Title)}]({heading.File}#{AnchorOf(uid)})";
    }

    // Members without their own heading (enum fields, method groups): link to the owning page.
    var symbol = StripParameters(uid).TrimEnd('*');
    for (var dot = symbol.LastIndexOf('.'); dot > 0; dot = symbol.LastIndexOf('.', dot - 1))
    {
        if (anchors.TryGetValue(AnchorOf(symbol[..dot]), out var owner) && owner.IsPage)
        {
            return $"[{OwnerName(owner.File)}.{StripArity(symbol[(dot + 1)..])}]({owner.File})";
        }
    }

    var shortName = StripArity(symbol[(symbol.LastIndexOf('.') + 1)..]);
    if (symbol.StartsWith("System.", StringComparison.Ordinal) || symbol.StartsWith("Microsoft.", StringComparison.Ordinal))
    {
        var learnPath = symbol.ToLowerInvariant().Replace("``", "-", StringComparison.Ordinal).Replace('`', '-');
        return $"[{shortName}](https://learn.microsoft.com/dotnet/api/{learnPath})";
    }

    return $"`{shortName}`";
}

static string AnchorOf(string uid) => Regex.Replace(uid, "[^A-Za-z0-9]", "_");

static string StripParameters(string uid) => uid.Split('(')[0];

static string StripArity(string name) => Regex.Replace(name, "`+[0-9]+", "");

// "Interface ISender" -> "ISender"; "Struct Result<T\>" -> "Result<T\>"
static string StripKind(string title) =>
    Regex.Replace(title, "^(Namespace|Class|Struct|Interface|Enum|Delegate|Record) ", "");

// "Send<TResponse\>\(IAppRequest<TResponse\>, CancellationToken\)" -> "Send<TResponse\>"
static string MemberName(string title) => title.Split(@"\(")[0];

// The owning type's short name without generic parameters, e.g. "Result" for Result<T>.
string OwnerName(string file)
{
    var page = anchors.Values.First(h => h.IsPage && h.File == file);
    return StripKind(page.Title).Split('<')[0];
}

record Heading(string File, string Title, bool IsPage);
