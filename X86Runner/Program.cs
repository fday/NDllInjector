using System;
using System.Linq;

namespace X86Runner
{
    class Program
    {
        static void Main( string[] args )
        {
            Environment.ExitCode = AppDomain.CurrentDomain.ExecuteAssembly(args[0], AppDomain.CurrentDomain.Evidence, args.Skip(1).ToArray());
        }
    }
}
