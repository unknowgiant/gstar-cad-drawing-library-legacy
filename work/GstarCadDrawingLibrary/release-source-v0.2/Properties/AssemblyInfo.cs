using System.Reflection;
using System.Runtime.InteropServices;
using Gssoft.Gscad.Runtime;
using GstarCadDrawingLibrary;

[assembly: AssemblyTitle("浩辰 CAD 图纸库")]
[assembly: AssemblyDescription("浩辰 CAD 2024 图纸库插件")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("浩辰 CAD 图纸库")]
[assembly: AssemblyCopyright("")]
[assembly: ComVisible(false)]
[assembly: Guid("62C59A42-3CC4-4F19-963F-553331F6F7BA")]
[assembly: AssemblyVersion("0.2.0.0")]
[assembly: AssemblyFileVersion("0.2.0.0")]
[assembly: ExtensionApplication(typeof(PluginEntry))]
[assembly: CommandClass(typeof(DrawingLibraryCommands))]
