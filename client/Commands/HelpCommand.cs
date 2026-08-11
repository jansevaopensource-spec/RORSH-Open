// RORSH-Gate Help Command

using System;

namespace RorshGate.Commands
{
    public static class HelpCommand
    {
        public static void Execute()
        {
            Console.WriteLine("");
            Console.WriteLine("Available Commands:");
            Console.WriteLine("===================");
            Console.WriteLine("");
            Console.WriteLine("  get-help              - Display this help message");
            Console.WriteLine("  get-cloud-down <file> - Download a file from the cloud server");
            Console.WriteLine("  get-cloud-down all    - Download all files from the cloud server");
            Console.WriteLine("  get-list-cloud        - List files available on the cloud server");
            Console.WriteLine("  get-list-local        - List files downloaded to local machine");
            Console.WriteLine("  get-run <file>        - Execute a downloaded file");
            Console.WriteLine("  get-end               - Disconnect and exit the application");
            Console.WriteLine("");
            Console.WriteLine("Supported File Types:");
            Console.WriteLine("---------------------");
            Console.WriteLine("  Windows: .exe .msi .msix .appx .appxbundle .msixbundle");
            Console.WriteLine("           .bat .cmd .com .ps1 .vbs .js .jse .wsf .wsh");
            Console.WriteLine("           .msc .scr .cpl .lnk .jar .py .pyw .psm1 .psd1");
            Console.WriteLine("           .reg .hta");
            Console.WriteLine("  Linux:   .run .bin .sh .elf .out .AppImage .deb .rpm");
            Console.WriteLine("           .pkg.tar.zst .snap .flatpak");
            Console.WriteLine("  Media:   .mp3 .mp4 .avi .mkv .jpg .png .pdf .doc .txt");
            Console.WriteLine("");
        }
    }
}
