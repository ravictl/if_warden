namespace IronFoundry.Warden.PInvoke
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;

    public partial class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateJobObject(IntPtr securityAttributes, string name);

        [DllImport("kernel32.dll", SetLastError=true)]
        [return:MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsProcessInJob(IntPtr processHandle, SafeHandle jobHandle, [MarshalAs(UnmanagedType.Bool)]out bool result);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AssignProcessToJobObject(SafeHandle jobHandle, IntPtr processHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool TerminateJobObject(SafeHandle jobHandle, uint exitCode);
    }
}
