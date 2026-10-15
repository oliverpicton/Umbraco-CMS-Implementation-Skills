using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Skills.Examples.BackofficeLocalization.NetLocalization;

/// <summary>
/// This example's own harness, not part of the skill.
///
/// A real user creates config/lang/ at the project root and drops the .user.xml files in. Here the host
/// project is shared, so the same files are written from the embedded copies at start-up, before the
/// text service reads its file sources. config/lang is git-ignored in the host project.
/// </summary>
public class NetLocalizationExampleComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // The host's content root is in configuration under "contentRoot"; the builder has no hosting environment.
        var contentRoot = builder.Config["contentRoot"]
            ?? throw new InvalidOperationException("The host configuration has no contentRoot.");
        var langFolder = Path.Combine(contentRoot, "config", "lang");
        Directory.CreateDirectory(langFolder);

        foreach (var name in new[] { "en.user.xml", "da.user.xml" })
        {
            using Stream stream = typeof(NetLocalizationExampleComposer).Assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Embedded asset '{name}' is missing from the example.");
            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();

            var path = Path.Combine(langFolder, name);
            if (!File.Exists(path) || File.ReadAllText(path) != content)
            {
                File.WriteAllText(path, content);
            }
        }
    }
}
