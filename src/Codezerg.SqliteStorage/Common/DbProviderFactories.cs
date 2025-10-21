using System;
using System.Data.Common;
using System.Linq;
using System.Reflection;

namespace Codezerg.SqliteStorage.Common;

/// <summary>
/// Provides a simple implementation of DbProviderFactories for .NET Standard 2.0 compatibility.
/// Loads ADO.NET providers dynamically at runtime without compile-time dependencies.
/// </summary>
internal static class DbProviderFactories
{
    /// <summary>
    /// Gets a DbProviderFactory instance for the specified provider name.
    /// </summary>
    /// <param name="providerInvariantName">The provider invariant name (e.g., "Microsoft.Data.Sqlite", "System.Data.SQLite").</param>
    /// <returns>A DbProviderFactory instance for creating connections.</returns>
    /// <exception cref="ArgumentException">Thrown when the provider name is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the provider cannot be loaded or factory cannot be found.</exception>
    public static DbProviderFactory GetFactory(string providerInvariantName)
    {
        if (string.IsNullOrWhiteSpace(providerInvariantName))
            throw new ArgumentException("Provider name cannot be null or empty.", nameof(providerInvariantName));

        // Try to load the assembly with the provider name
        Assembly providerAssembly;
        try
        {
            providerAssembly = Assembly.Load(new AssemblyName(providerInvariantName));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to load provider assembly '{providerInvariantName}'. " +
                $"Ensure the provider package is referenced in your application. " +
                $"For example, add <PackageReference Include=\"{providerInvariantName}\" /> to your project file.",
                ex);
        }

        // Look for the factory type
        // Common patterns:
        // - Microsoft.Data.Sqlite.SqliteFactory
        // - System.Data.SQLite.SQLiteFactory
        // - Npgsql.NpgsqlFactory
        var factoryType = FindFactoryType(providerAssembly, providerInvariantName);
        if (factoryType == null)
        {
            throw new InvalidOperationException(
                $"Could not find DbProviderFactory type in assembly '{providerInvariantName}'. " +
                $"Expected a type derived from DbProviderFactory.");
        }

        // Try to get the singleton Instance field/property (common pattern)
        var instanceField = factoryType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
        if (instanceField != null && instanceField.FieldType == factoryType)
        {
            var factory = instanceField.GetValue(null) as DbProviderFactory;
            if (factory != null)
                return factory;
        }

        var instanceProperty = factoryType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        if (instanceProperty != null && instanceProperty.PropertyType == factoryType)
        {
            var factory = instanceProperty.GetValue(null) as DbProviderFactory;
            if (factory != null)
                return factory;
        }

        // Fallback: try to create a new instance
        try
        {
            var factory = Activator.CreateInstance(factoryType) as DbProviderFactory;
            if (factory != null)
                return factory;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to create instance of factory type '{factoryType.FullName}'.",
                ex);
        }

        throw new InvalidOperationException(
            $"Could not instantiate DbProviderFactory from type '{factoryType.FullName}' in assembly '{providerInvariantName}'.");
    }

    /// <summary>
    /// Finds the DbProviderFactory type in the given assembly.
    /// </summary>
    private static Type? FindFactoryType(Assembly assembly, string providerName)
    {
        // Get all public types that derive from DbProviderFactory
        var factoryTypes = assembly.GetTypes()
            .Where(t => t.IsPublic && !t.IsAbstract && typeof(DbProviderFactory).IsAssignableFrom(t))
            .ToList();

        if (factoryTypes.Count == 0)
            return null;

        // If there's only one, use it
        if (factoryTypes.Count == 1)
            return factoryTypes[0];

        // If multiple, try to find the one with "Factory" in the name
        var factoryType = factoryTypes.FirstOrDefault(t => t.Name.EndsWith("Factory", StringComparison.OrdinalIgnoreCase));
        if (factoryType != null)
            return factoryType;

        // Fallback to the first one
        return factoryTypes[0];
    }
}
