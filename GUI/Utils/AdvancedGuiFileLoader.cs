using System;
using System.Collections.Generic;
using System.Linq;
using SteamDatabase.ValvePak;
using ValveResourceFormat;
using ValveResourceFormat.CompiledShader;
using ValveResourceFormat.IO;

namespace GUI.Utils
{
    class AdvancedGuiFileLoader : GameFileLoader
    {
        private readonly Dictionary<string, Resource> CachedResources = new();
        private readonly VrfGuiContext GuiContext;

        public AdvancedGuiFileLoader(VrfGuiContext guiContext) : base(guiContext.CurrentPackage, guiContext.FileName)
        {
            GuiContext = guiContext;

            var paths = Settings.Config.GameSearchPaths.ToList();

            // Find any gameinfo files specified by the user in settings
            foreach (var searchPath in paths.Where(searchPath => searchPath.EndsWith("gameinfo.gi", StringComparison.InvariantCulture)).ToList())
            {
                paths.Remove(searchPath);

                FindAndLoadSearchPaths(searchPath);
            }

            // Find any .vpk files specified by the user
            foreach (var searchPath in paths.Where(searchPath => searchPath.EndsWith(".vpk", StringComparison.InvariantCulture)).ToList())
            {
                paths.Remove(searchPath);

                AddPackageToSearch(searchPath);
            }

            // Add remaining paths specified by the user
            foreach (var searchPath in paths)
            {
                AddDiskPathToSearch(searchPath);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ClearCache();

                base.Dispose(disposing);
            }
        }

        public void ClearCache()
        {
            foreach (var resource in CachedResources.Values)
            {
                resource.Dispose();
            }

            CachedResources.Clear();
        }

        public (VrfGuiContext Context, PackageEntry PackageEntry) FindFileWithContext(string file)
        {
            var foundFile = FindFileWithContextRecursive(file);

            if (foundFile.PackageEntry == null && foundFile.PathOnDisk == null)
            {
                return (null, null);
            }

            VrfGuiContext newContext = null;

            if (foundFile.PathOnDisk != null)
            {
                newContext = new VrfGuiContext(foundFile.PathOnDisk, null);
            }

            if (foundFile.Package != null)
            {
                var parentContext = foundFile.Context?.ParentGuiContext;
                parentContext ??= new VrfGuiContext(foundFile.Package.FileName, null)
                {
                    CurrentPackage = foundFile.Package
                };

                newContext = new VrfGuiContext(foundFile.PackageEntry.GetFullPath(), parentContext);
            }

            return (newContext, foundFile.PackageEntry);
        }

        // Same as FindFile, but also returns the context and package that the file was found in
        private (string PathOnDisk, VrfGuiContext Context, Package Package, PackageEntry PackageEntry) FindFileWithContextRecursive(string file)
        {
            var entry = GuiContext.CurrentPackage?.FindEntry(file);

            if (entry != null)
            {
                return (null, GuiContext, GuiContext.CurrentPackage, entry);
            }

            if (GuiContext.ParentGuiContext != null)
            {
                return GuiContext.ParentGuiContext.FileLoader.FindFileWithContextRecursive(file);
            }

            var (pathOnDisk, package, packageEntry) = base.FindFile(file);
            return (pathOnDisk, null, package, packageEntry);
        }

        // Override to add support for parent file loaders
        public override (string PathOnDisk, Package Package, PackageEntry PackageEntry) FindFile(string file, bool logNotFound = true)
        {
            var entry = GuiContext.CurrentPackage?.FindEntry(file);

            if (entry != null)
            {
                return (null, GuiContext.CurrentPackage, entry);
            }

            if (GuiContext.ParentGuiContext != null)
            {
                return GuiContext.ParentGuiContext.FileLoader.FindFile(file, logNotFound);
            }

            return base.FindFile(file, logNotFound);
        }

        // Override to add support for parent file loaders
        protected override ShaderCollection LoadShaderFromDisk(string shaderName)
        {
            if (GuiContext.ParentGuiContext != null)
            {
                return GuiContext.ParentGuiContext.FileLoader.LoadShader(shaderName);
            }

            return base.LoadShaderFromDisk(shaderName);
        }

        // Override to add support for caching resources
        public override Resource LoadFile(string file)
        {
            // TODO: Might conflict where same file name is available in different paths
            if (CachedResources.TryGetValue(file, out var resource) && resource.Reader != null)
            {
                return resource;
            }

            resource = base.LoadFile(file);

            if (resource != null)
            {
                CachedResources[file] = resource;
            }

            return resource;
        }
    }
}
