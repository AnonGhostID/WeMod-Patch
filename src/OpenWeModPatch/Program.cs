using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace OpenWeModPatch
{
    internal class Program
    {
        private static bool YesNoQuestion(string text)
        {
            ConsoleKey response;
            do
            {
                Console.Write($"{text} [y/n]: ");
                response = Console.ReadKey(false).Key;
                if (response != ConsoleKey.Enter)
                    Console.WriteLine();
            } while (response != ConsoleKey.Y && response != ConsoleKey.N);

            return response == ConsoleKey.Y;
        }

        private static bool ProcessStatus(List<WeModPatcher.PatchResult> status)
        {
            var fatal = false;

            var color = Console.ForegroundColor;
            foreach (var result in status)
            {
                switch (result.State)
                {
                    case WeModPatcher.PatchState.Ok:
                        Console.ForegroundColor = ConsoleColor.Green;
                        break;
                    case WeModPatcher.PatchState.Error:
                    case WeModPatcher.PatchState.Exception:
                        fatal = true;
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                Console.WriteLine(result.Message);
            }

            Console.ForegroundColor = color;
            return !fatal;
        }

        private static void TryKillWeMod()
        {
            var processes = Process.GetProcessesByName("WeMod");
            if (processes.Length > 0)
            {
                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // Failed
                    }
                }
            }
        }

        private static void Main()
        {
            TryKillWeMod();
            Console.WriteLine("OpenWeModPatch v1.2");
           
            var patcher = WeModPatcher.Create();
            while (patcher == null)
            {
                Console.WriteLine("WeMod.exe not found");
                Console.Write("Enter path: ");
                patcher = WeModPatcher.Create(Console.ReadLine());
            }

            Console.WriteLine($"Executable: {patcher.Executable}");
            Console.WriteLine($"Version: {patcher.Version.FileVersion}");

            var status = patcher.DisableAsarIntegrityValidation();
            if (ProcessStatus(status))
            {
                patcher.EnablePatch(WeModPatcher.Patches.EnablePro, YesNoQuestion("Enable PRO?"));
                patcher.EnablePatch(WeModPatcher.Patches.DisableUpdates, YesNoQuestion("Disable updates?"));

                status = patcher.Patch(false);
                if (status.First().State == WeModPatcher.PatchState.HasBackup)
                {
                    if (YesNoQuestion("Asar backup found. Override?")) status = patcher.Patch(true);
                    else return;
                }

                ProcessStatus(status);
            }

            Console.WriteLine("Press any key to exit . . .");
            Console.ReadKey(true);
        }
    }
}