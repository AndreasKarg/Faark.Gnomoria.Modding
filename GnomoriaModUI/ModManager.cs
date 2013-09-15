namespace GnomoriaModUI
{
    internal static class ModManager
    {
        public const string ConfigFileName = "GnomoriaModConfig.xml";
        public const string OriginalExecutable = "Gnomoria.exe";
        public const string ModdedExecutable = "GnomoriaModded.dll";
        public const string OriginalLibrary = "gnomorialib.dll";
        public const string ModdedLibrary = "gnomorialibModded.dll";
        public const string ModController = "GnomoriaModController.dll";
        public static readonly string[] Dependencies = { /*"GnomoriaModController.dll", */"Gnomoria.exe", "gnomorialib.dll", "SevenZipSharp.dll" };
        public static readonly System.IO.DirectoryInfo GameDirectory = new System.IO.DirectoryInfo(System.IO.Directory.GetCurrentDirectory());
    }
}
