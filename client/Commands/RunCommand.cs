// RORSH-Gate Run Command

using System;
using RorshGate.Core;

namespace RorshGate.Commands
{
    public static class RunCommand
    {
        public static void Execute(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                Console.WriteLine("Error: Please specify a filename.");
                Console.WriteLine("Usage: get-run <filename>");
                return;
            }

            Console.WriteLine($"Attempting to run: {filename}");
            bool success = Runner.RunFile(filename);

            if (!success)
            {
                Console.WriteLine("Run failed. Check logs for details.");
            }
        }
    }
}
