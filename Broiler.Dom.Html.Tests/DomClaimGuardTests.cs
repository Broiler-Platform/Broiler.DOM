using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Broiler.Dom.Html.Tests;

/// <summary>
/// Guards the claims the DOM rights register makes about this component.
/// </summary>
/// <remarks>
/// <para>
/// The register's rows about other parties' patents cannot be tested by anything,
/// which is why they need a person. Its rows about <em>this repository</em> can,
/// and those are what these bind: what the tree contains, what it depends on, and
/// where the one artifact worth copying actually lives.
/// </para>
/// <para>
/// The last of those is the reason this suite exists at all. DOM-IP-003 is the
/// row this register was opened for, and a guard that fails when it stops being
/// true is worth more than the paragraph asserting it.
/// </para>
/// </remarks>
public sealed class DomClaimGuardTests
{
    /// <summary>The two libraries whose provenance the register records.</summary>
    public static TheoryData<string> Libraries => new() { "Broiler.Dom", "Broiler.Dom.Html" };

    /// <summary>
    /// Named character references that appear in the HTML Standard's table and
    /// essentially nowhere else.
    /// </summary>
    /// <remarks>
    /// Chosen so a hand-rolled handful cannot trip them. A parser that special-cases
    /// a few references handles <c>amp</c>, <c>lt</c>, <c>gt</c>, <c>quot</c> and
    /// <c>nbsp</c>; nobody writes <c>boxdl</c> or <c>zwnj</c> by hand without
    /// copying the table they came from.
    /// </remarks>
    private static readonly string[] TableOnlyReferences =
    [
        "zwnj", "hellip", "thinsp", "boxdl", "rsaquo", "dagger", "permil", "oline",
    ];

    [Fact(Timeout = 600000)]
    public void The_Named_Character_Reference_Table_Is_Still_The_Platforms()
    {
        // DOM-IP-003. The HTML Standard defines roughly 2,231 named references and
        // this component implements none of them: two call sites hand the work to
        // the runtime. That is what keeps the largest transcribable artifact in
        // HTML out of this repository, and it is one edit away from not being true.
        string root = ComponentRoot();

        string tokenizer = File.ReadAllText(Path.Combine(root, "Broiler.Dom.Html", "HtmlTokenizer.cs"));
        string serializer = File.ReadAllText(Path.Combine(root, "Broiler.Dom.Html", "HtmlSerializer.cs"));

        Assert.Contains("WebUtility.HtmlDecode", tokenizer, StringComparison.Ordinal);
        Assert.Contains("WebUtility.HtmlEncode", serializer, StringComparison.Ordinal);

        string[] transcribed = SourceFiles(root)
            .SelectMany(path => TableOnlyReferences
                .Where(name => Regex.IsMatch(
                    File.ReadAllText(path),
                    "\"" + name + "\"",
                    RegexOptions.CultureInvariant))
                .Select(name => Path.GetRelativePath(root, path) + ": " + name))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(transcribed);
    }

    [Theory(Timeout = 600000)]
    [MemberData(nameof(Libraries))]
    public void No_Data_File_Sits_Beside_A_Library(string library)
    {
        // DOM-IP-004. A committed table or fixture is the shape of the thing the
        // provenance row rules out.
        string root = ComponentRoot();
        string[] dataFiles = Directory
            .EnumerateFiles(Path.Combine(root, library), "*", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path => Path.GetExtension(path) is not (".cs" or ".csproj"))
            .Select(path => Path.GetRelativePath(root, path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(dataFiles);
    }

    [Theory(Timeout = 600000)]
    [MemberData(nameof(Libraries))]
    public void No_Library_Takes_A_Package_Reference(string library)
    {
        // "Dependency-free" is the first word of this component's own description,
        // and the provenance row rests on it: there is no third-party parser here
        // to account for because there is nothing here at all.
        XDocument project = XDocument.Load(
            Path.Combine(ComponentRoot(), library, library + ".csproj"));

        Assert.Empty(project.Descendants("PackageReference"));
    }

    [Fact(Timeout = 600000)]
    public void No_Third_Party_Conformance_Suite_Is_Committed()
    {
        // DOM-IP-005. html5lib-tests ships as .dat and .json and is designed to be
        // vendored, which makes it the realistic way third-party material enters an
        // HTML parser. Importing it is allowed; importing it without recording its
        // licence is what this refuses.
        string root = ComponentRoot();
        string[] extensions = [".html", ".htm", ".json", ".dat", ".xml"];

        string[] committed = Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path => !IsToolingDirectory(path))
            .Where(path => extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(root, path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(committed);
    }

    [Fact(Timeout = 600000)]
    public void The_Register_Exists_And_Bounds_The_Components_Wording()
    {
        string text = File.ReadAllText(
            Path.Combine(ComponentRoot(), "docs", "dom-ip-licensing-register.md"));

        Assert.Contains("## Approved wording", text, StringComparison.Ordinal);
        Assert.Contains("NO LAWYER HAS REVIEWED ANY OF THIS", text, StringComparison.Ordinal);
    }

    /// <summary>The same two libraries, as plain strings for the non-theory guards.</summary>
    private static readonly string[] LibraryNames = ["Broiler.Dom", "Broiler.Dom.Html"];

    private static IEnumerable<string> SourceFiles(string root) =>
        LibraryNames
            .SelectMany(library => Directory.EnumerateFiles(
                Path.Combine(root, library), "*.cs", SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(path));

    private static bool IsBuildOutput(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj");

    /// <summary>
    /// Whether <paramref name="path"/> sits in a directory the version control system or the IDE
    /// owns rather than the repository.
    /// </summary>
    /// <remarks>
    /// Nothing in either is tracked, so a file found there says nothing about what was committed —
    /// which is the whole question these guards ask. <c>.git</c> was already skipped; Visual Studio
    /// writes <c>.vs/&lt;solution&gt;/v18/DocumentLayout.json</c>, which the extension scan above
    /// read as a vendored conformance fixture and failed on, on every machine that had opened the
    /// solution and nowhere else.
    /// </remarks>
    private static bool IsToolingDirectory(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is ".git" or ".vs");

    /// <summary>
    /// The Broiler.DOM repository root: the directory owning
    /// <c>Directory.Build.props</c> and holding <c>Broiler.Dom</c>. Found by
    /// walking up from the test binary, so it resolves the same way standalone and
    /// when this component is checked out inside a consumer.
    /// </summary>
    private static string ComponentRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")) &&
                File.Exists(Path.Combine(directory.FullName, "Broiler.Dom", "Broiler.Dom.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Broiler.DOM component root not found.");
    }
}
