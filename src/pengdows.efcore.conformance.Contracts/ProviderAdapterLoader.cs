using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace pengdows.efcore.conformance;

/// <summary>Loads adapter dependencies from their own deployment folder while sharing EF Core and contract assemblies with the host.</summary>
public static class ProviderAdapterLoader
{
    private static readonly object Gate = new();
    private static readonly List<AssemblyDependencyResolver> Resolvers = [];
    private static bool handlersRegistered;

    public static IProviderAdapter Load(string assemblyPath, string typeName)
    {
        var fullPath = Path.GetFullPath(assemblyPath);
        lock (Gate)
        {
            Resolvers.Add(new AssemblyDependencyResolver(fullPath));
            if (!handlersRegistered)
            {
                AssemblyLoadContext.Default.Resolving += ResolveAssembly;
                AssemblyLoadContext.Default.ResolvingUnmanagedDll += ResolveUnmanagedAssembly;
                handlersRegistered = true;
            }
        }

        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
        var adapterType = assembly.GetType(typeName, throwOnError: true, ignoreCase: false)!;
        if (!typeof(IProviderAdapter).IsAssignableFrom(adapterType) || adapterType.IsAbstract)
        {
            throw new InvalidOperationException($"{adapterType.FullName} must be a concrete {nameof(IProviderAdapter)} implementation.");
        }

        var adapter = Activator.CreateInstance(adapterType) as IProviderAdapter
            ?? throw new InvalidOperationException($"{adapterType.FullName} needs a public parameterless constructor.");
        adapter.Capabilities.Validate();
        return adapter;
    }

    private static Assembly? ResolveAssembly(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        var loaded = context.Assemblies
            .Where(assembly => StringComparer.Ordinal.Equals(assembly.GetName().Name, assemblyName.Name))
            .OrderByDescending(assembly => assembly.GetName().Version)
            .FirstOrDefault(assembly => assembly.GetName().Version >= assemblyName.Version);
        if (loaded is not null)
        {
            return loaded;
        }

        lock (Gate)
        {
            foreach (var resolver in Resolvers)
            {
                var dependencyPath = resolver.ResolveAssemblyToPath(assemblyName);
                if (dependencyPath is not null)
                {
                    return context.LoadFromAssemblyPath(dependencyPath);
                }
            }
        }

        return null;
    }

    private static IntPtr ResolveUnmanagedAssembly(Assembly assembly, string libraryName)
    {
        lock (Gate)
        {
            foreach (var resolver in Resolvers)
            {
                var libraryPath = resolver.ResolveUnmanagedDllToPath(libraryName);
                if (libraryPath is not null)
                {
                    return NativeLibrary.Load(libraryPath);
                }
            }
        }

        return IntPtr.Zero;
    }
}
