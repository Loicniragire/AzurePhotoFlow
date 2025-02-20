using System.Reflection;

namespace UnitTests;
public static class TestHelper
{
    /// <summary>
    /// Retrieves an embedded resource stream based on the provided resource path.
    /// The resource path can be specified relative to the default namespace.
    /// For example, if the default namespace is "MyTestProject" and the file is located at "Images/digital/sample.png",
    /// then passing "Images/digital/sample.png" will be transformed into "MyTestProject.Images.digital.sample.png".
    /// </summary>
    /// <param name="resourcePath">The relative or fully qualified resource name.</param>
    /// <returns>A Stream of the embedded resource.</returns>
    public static Stream GetEmbeddedResource(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            throw new ArgumentNullException(nameof(resourcePath), "Resource path cannot be null or empty.");

        // Get the current executing assembly (assuming the resources are embedded in the test assembly)
        Assembly assembly = Assembly.GetExecutingAssembly();
        string defaultNamespace = assembly.GetName().Name;
        string fullResourceName = resourcePath;

        // If the resourcePath does not start with the default namespace, prefix it.
        if (!resourcePath.StartsWith(defaultNamespace, StringComparison.Ordinal))
        {
            // Replace directory separators with '.' to match the naming convention
            fullResourceName = $"{defaultNamespace}.{resourcePath.Replace("/", ".").Replace("\\", ".")}";
        }

        // Retrieve all resource names for debugging purposes.
        string[] availableResources = assembly.GetManifestResourceNames();

        // Attempt to locate the resource name in a case-insensitive manner.
        string foundResourceName = availableResources
            .FirstOrDefault(r => r.Equals(fullResourceName, StringComparison.OrdinalIgnoreCase));

        if (foundResourceName == null)
        {
            throw new ArgumentException(
                $"Embedded resource '{fullResourceName}' not found. Available resources: {string.Join(", ", availableResources)}",
                nameof(resourcePath));
        }

        // Get the resource stream.
        Stream resourceStream = assembly.GetManifestResourceStream(foundResourceName);
        if (resourceStream == null)
        {
            throw new Exception($"Failed to load the resource stream for '{foundResourceName}'.");
        }

        return resourceStream;
    }

}

