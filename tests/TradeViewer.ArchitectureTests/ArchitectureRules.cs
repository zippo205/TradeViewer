using System.Reflection;
using NetArchTest.Rules;

namespace TradeViewer.ArchitectureTests;

/// <summary>The rules of architecture chapters 3 and 4 as reusable checks; the tests call them on the real assemblies, the probes on deliberate violations.</summary>
internal static class ArchitectureRules
{
    /// <summary>Types in the namespace (and below) must not depend on any of the forbidden namespaces.</summary>
    public static TestResult NoDependency(Assembly assembly, string fromNamespace, params string[] forbiddenNamespaces)
    {
        // NetArchTest matches a dependency by the start of its namespace, so a forbidden name must not be
        // the beginning of another name (a trailing dot does not work: it matches nothing).
        var forbidden = forbiddenNamespaces;

        return Types.InAssembly(assembly)
            .That().ResideInNamespaceMatching("^" + EscapeNamespace(fromNamespace) + "(\\..*)?$")
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();
    }

    /// <summary>No feature folder below the root namespace may depend on another feature folder.</summary>
    public static IReadOnlyList<string> FeatureViolations(Assembly assembly, string featuresRootNamespace)
    {
        var features = FeatureNames(assembly, featuresRootNamespace);
        var violations = new List<string>();

        foreach (var feature in features)
        {
            if (features.Any(other => other != feature && other.StartsWith(feature, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "The feature name " + feature + " is the beginning of another feature name; the rule cannot tell them apart.");
            }
        }

        foreach (var feature in features)
        {
            var others = features
                .Where(other => other != feature)
                .Select(other => featuresRootNamespace + "." + other)
                .ToArray();

            if (others.Length == 0)
            {
                continue;
            }

            var result = NoDependency(assembly, featuresRootNamespace + "." + feature, others);
            if (!result.IsSuccessful)
            {
                foreach (var type in result.FailingTypeNames ?? Array.Empty<string>())
                {
                    violations.Add(feature + ": " + type);
                }
            }
        }

        return violations;
    }

    public static IReadOnlyList<string> FeatureNames(Assembly assembly, string featuresRootNamespace)
    {
        var prefix = featuresRootNamespace + ".";

        return assembly.GetTypes()
            .Where(type => type.Namespace != null && type.Namespace.StartsWith(prefix, StringComparison.Ordinal))
            .Select(type => type.Namespace!.Substring(prefix.Length).Split('.')[0])
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    private static string EscapeNamespace(string value)
    {
        return value.Replace(".", "\\.");
    }
}
