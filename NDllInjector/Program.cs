using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace NDllInjector
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 5)
            {
                PrintUsage();
                Environment.Exit(-1);

            }

            int pid = Int32.Parse(args[0]);

            bool is64BitCurrentProcess = ProcessInjector.Is64BitProcess(Process.GetCurrentProcess().Id);
            bool is64BitTargetProcess = ProcessInjector.Is64BitProcess(pid);

            if (is64BitCurrentProcess ^ is64BitTargetProcess)
            {
                StringBuilder sb = new StringBuilder(1024);

                foreach(var arg in Concat(Process.GetCurrentProcess().MainModule.FileName, args))
                {
                    sb.AppendFormat(arg.Contains(" ") ? "\"{0}\" " : "{0} ", arg);
                }

                Process process = Process.Start(Path.Combine(Directory.GetCurrentDirectory(), "X86Runner.exe"),  sb.ToString());
                process.WaitForExit();
                Environment.ExitCode = process.ExitCode;
            }
            else
            {
                try
                {
                    var pi = new ProcessInjector();
                    Environment.ExitCode = pi.Inject(pid,
                                                Path.Combine(Directory.GetCurrentDirectory(), string.Format(@"bootstrap{0}.bin", (is64BitCurrentProcess ? "64" : "32"))),
                                                args[1],
                                                args[2],
                                                args[3],
                                                args[4]);
                }
                catch( Exception e)
                {
                    Console.WriteLine("Error occured: ", e.ToString());
                    Environment.ExitCode = -1;
                }
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: ndllinjector <pid> <runtime version> <dll path> <class name> <function name>");
        }

        private static IEnumerable<T> Concat<T>(T first, IEnumerable<T> other )
        {
            yield return first;
            foreach (var t in other)
            {
                yield return t;
            }
        } 
    }
}
