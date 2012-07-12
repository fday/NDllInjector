using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace NDllInjector
{
    public class ProcessInjector
    {
        public static bool Is64BitProcess( int pid )
        {
            SYSTEM_INFO si = new SYSTEM_INFO();
            UnsafeFunctions.GetNativeSystemInfo(ref si);
            if (si.processorArchitecture == 0)
            {
                return false;
            }

            IntPtr hProcess = UnsafeFunctions.OpenProcess(ProcessAccessFlags.QueryInformation, false, pid);
            if (IntPtr.Zero == hProcess)
            {
                throw new Exception("Cann't open process.");
            }
            
            bool result;
            if (!UnsafeFunctions.IsWow64Process(hProcess, out result))
            {
                UnsafeFunctions.CloseHandle(hProcess);
                throw new InvalidOperationException();
            }

            UnsafeFunctions.CloseHandle(hProcess);

            return !result;
        }

        protected void adjustDebugPriv( int pid )
        {
            IntPtr hProcess = UnsafeFunctions.OpenProcess(ProcessAccessFlags.All, false, pid);

            if (IntPtr.Zero == hProcess)
            {
                throw new Exception("Cann't open process.");
            }

            TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES
                                      {
                                          PrivilegeCount = 1,
                                          Attributes = SE_NAMES.SE_PRIVILEGE_ENABLED
                                      };
            
            if (!UnsafeFunctions.LookupPrivilegeValue(null, SE_NAMES.SE_DEBUG_NAME, out tp.Luid))
            {
                UnsafeFunctions.CloseHandle(hProcess);
                throw new Exception("Cann't lookup value");                
            }

            IntPtr hToken;
            if (!UnsafeFunctions.OpenProcessToken(hProcess, TOKEN_ACCESS.TOKEN_ADJUST_PRIVILEGES, out hToken))
            {
                UnsafeFunctions.CloseHandle(hProcess);
                throw new Exception("Cann't open process token value");                                
            }

            if (!UnsafeFunctions.AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
            {
                UnsafeFunctions.CloseHandle(hProcess);
                UnsafeFunctions.CloseHandle(hToken);
                throw new Exception("Cann't AdjustTokenPrivileges");
            }
            UnsafeFunctions.CloseHandle(hProcess);
            UnsafeFunctions.CloseHandle(hToken);
        }

        private ProcessModule GetKernel32Module( Process process )
        {
            var kernel32 = process.Modules.Cast<ProcessModule>().FirstOrDefault(pm => pm.ModuleName.ToLower() == "kernel32.dll");
            if (kernel32 == null)
            {
                throw new Exception(string.Format("kernel32.dll not present in process with pid = {0}", process.Id));
            }
            return kernel32;
        }

        private IntPtr GetFunctionAddress( ProcessModule remoteKernel32, string name )
        {
            Process process = Process.GetCurrentProcess();
            ProcessModule kernel32 = GetKernel32Module(process);

            IntPtr proc = UnsafeFunctions.GetProcAddress(kernel32.BaseAddress, name);

            if (IntPtr.Zero == proc)
            {
                throw new Exception("Cann't get process address.");
            }

            return new IntPtr(remoteKernel32.BaseAddress.ToInt64() + (proc.ToInt64() - kernel32.BaseAddress.ToInt64()));
        }

        public int Inject( int pid, string bootstrapPath, string runtimeVersion, string injecteePath, string injecteeClass, string injecteeFunc )
        {
            adjustDebugPriv(Process.GetCurrentProcess().Id);

            ProcessModule remoteKernel32 = GetKernel32Module(Process.GetProcessById(pid));

            IntPtr loadLibraryAddress = GetFunctionAddress(remoteKernel32, "LoadLibraryA");
            IntPtr getProcAddressAddress = GetFunctionAddress(remoteKernel32, "GetProcAddress");

            IntPtr hProcess = UnsafeFunctions.OpenProcess(
                ProcessAccessFlags.CreateThread | ProcessAccessFlags.VMWrite | ProcessAccessFlags.VMOperation
                | ProcessAccessFlags.VMRead | ProcessAccessFlags.QueryInformation,
                false, pid);
            
            if (IntPtr.Zero == hProcess)
            {
                throw new Exception("Cann't open process.");
            }

            bool is64BitTargetProcess = Is64BitProcess(pid);

            byte[] bootstrap = File.ReadAllBytes(bootstrapPath);

            List<byte[]> param = new List<byte[]>(6)
                                     {
                                         Encoding.Unicode.GetBytes(runtimeVersion + "\0"),
                                         Encoding.Unicode.GetBytes(injecteePath + "\0"),
                                         Encoding.Unicode.GetBytes(injecteeClass + "\0"),
                                         Encoding.Unicode.GetBytes(injecteeFunc + "\0")
                                     };

            int totalSize = bootstrap.Length                            // code
                + (param.Count + 2) * (is64BitTargetProcess ? 8 : 4)    // addresses of params
                + param.Sum(p => p.Length);                             // params

            IntPtr memory = UnsafeFunctions.VirtualAllocEx(hProcess, IntPtr.Zero, (uint) totalSize, AllocationType.Commit, MemoryProtection.ExecuteReadWrite);

            if (IntPtr.Zero == memory)
            {
                UnsafeFunctions.CloseHandle(hProcess);
                throw new Exception("Cann't alloc memory.");
            }

            int length = param.Count;

            if (is64BitTargetProcess)
            {
                param.Add(BitConverter.GetBytes(loadLibraryAddress.ToInt64()));
                param.Add(BitConverter.GetBytes(getProcAddressAddress.ToInt64()));
            }
            else
            {
                param.Add(BitConverter.GetBytes(loadLibraryAddress.ToInt32()));
                param.Add(BitConverter.GetBytes(getProcAddressAddress.ToInt32()));
            }


            long address = memory.ToInt64() + bootstrap.Length;

            for (int i = 0; i < length; i++)
            {
                byte[] b;
                if (is64BitTargetProcess)
                {
                    b = BitConverter.GetBytes(address);
                }
                else
                {
                    b = BitConverter.GetBytes((int) address);                    
                }
                param.Add(b);
                address += param[i].Length;
            }


            byte[] injectedData = new byte[totalSize];
            Array.Copy(bootstrap, 0, injectedData, 0, bootstrap.Length);
            int position = bootstrap.Length;

            foreach (var p in param)
            {
                Array.Copy(p, 0, injectedData, position, p.Length);
                position += p.Length;
            }

            UIntPtr written;

            if (!UnsafeFunctions.WriteProcessMemory(hProcess, memory, injectedData, (uint) injectedData.Length, out written) || injectedData.Length != written.ToUInt32())
            {
                UnsafeFunctions.CloseHandle(hProcess);
                throw new Exception("Cann't write memory.");
            }

            IntPtr threadId;
            IntPtr thread = UnsafeFunctions.CreateRemoteThread(hProcess, IntPtr.Zero, 0x20000 /*at least 0x10000*/, memory, new IntPtr(address), 0, out threadId);

            if (IntPtr.Zero == thread)
            {
                Debug.WriteLine(Marshal.GetLastWin32Error().ToString("X"));
                UnsafeFunctions.CloseHandle(hProcess);
                throw new Exception("Cann't create thread.");
            }

            UnsafeFunctions.CloseHandle(hProcess);
            UnsafeFunctions.CloseHandle(thread);

            return 0;
        }

    }
}
