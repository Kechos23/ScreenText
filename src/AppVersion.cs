using System.Reflection;
[assembly: AssemblyTitle("ScreenText")]
[assembly: AssemblyDescription("Screen capture, OCR and AI translation")]
[assembly: AssemblyCompany("Kechos")]
[assembly: AssemblyProduct("ScreenText")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Ioannis Kechagias")]
namespace ScreenText { public static class AppVersion { public static string Current { get { return typeof(AppVersion).Assembly.GetName().Version.ToString(3); } } } }
