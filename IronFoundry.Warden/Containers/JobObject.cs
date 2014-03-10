namespace IronFoundry.Warden.Containers
{
    using IronFoundry.Warden.PInvoke;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Win32.SafeHandles;
    using System.Diagnostics;

    public class JobObject : IDisposable
    {
        SafeJobObjectHandle handle;

        public JobObject()
        {
            handle = new SafeJobObjectHandle(NativeMethods.CreateJobObject(IntPtr.Zero, null));
        }

        public SafeJobObjectHandle Handle
        {
            get
            {
                return handle;
            }
        }

        public void Dispose()
        {
            if (handle == null) { return; }
            handle.Dispose();
            handle = null;
        }

        public void AssignProcessToJob(Process p)
        {
            NativeMethods.AssignProcessToJobObject(handle, p.Handle);
        }

        public void TerminateProcesses()
        {
            if (handle == null) { throw new ObjectDisposedException("JobObject"); }
            NativeMethods.TerminateJobObject(handle, 0);
        }
    }
}
