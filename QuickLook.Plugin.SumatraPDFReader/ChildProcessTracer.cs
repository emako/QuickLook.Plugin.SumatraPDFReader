using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace QuickLook.Plugin.SumatraPDFReader;

/// <summary>
/// Make sures that child processes are automatically terminated
/// if the parent process exits unexpectedly.
/// </summary>
internal sealed class ChildProcessTracer
{
    public static ChildProcessTracer Default { get; } = new();

    private static class Kernel32
    {
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AssignProcessToJobObject([In] nint hJob, [In] nint hProcess);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern nint CreateJobObject([In, Optional] SECURITY_ATTRIBUTES? lpJobAttributes, [In, Optional] string? lpName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool SetInformationJobObject(nint hJob, JOBOBJECTINFOCLASS JobObjectInfoClass, nint lpJobObjectInfo, uint cbJobObjectInfoLength);

        [StructLayout(LayoutKind.Sequential)]
        public class SECURITY_ATTRIBUTES
        {
            public int nLength = Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES));

            public nint lpSecurityDescriptor;

            [MarshalAs(UnmanagedType.Bool)]
            public bool bInheritHandle;
        }

        public enum JOBOBJECTINFOCLASS
        {
            JobObjectBasicAccountingInformation = 1,
            JobObjectBasicLimitInformation,
            JobObjectBasicProcessIdList,
            JobObjectBasicUIRestrictions,
            JobObjectSecurityLimitInformation,
            JobObjectEndOfJobTimeInformation,
            JobObjectAssociateCompletionPortInformation,
            JobObjectBasicAndIoAccountingInformation,
            JobObjectExtendedLimitInformation,
            JobObjectJobSetInformation,
            JobObjectGroupInformation,
            JobObjectNotificationLimitInformation,
            JobObjectLimitViolationInformation,
            JobObjectGroupInformationEx,
            JobObjectCpuRateControlInformation,
            JobObjectCompletionFilter,
            JobObjectCompletionCounter,
            JobObjectReserved1Information = 18,
            JobObjectReserved2Information,
            JobObjectReserved3Information,
            JobObjectReserved4Information,
            JobObjectReserved5Information,
            JobObjectReserved6Information,
            JobObjectReserved7Information,
            JobObjectReserved8Information,
            JobObjectReserved9Information,
            JobObjectReserved10Information,
            JobObjectReserved11Information,
            JobObjectReserved12Information,
            JobObjectReserved13Information,
            JobObjectReserved14Information = 31,
            JobObjectNetRateControlInformation,
            JobObjectNotificationLimitInformation2,
            JobObjectLimitViolationInformation2,
            JobObjectCreateSilo,
            JobObjectSiloBasicInformation,
            JobObjectReserved15Information = 37,
            JobObjectReserved16Information = 38,
            JobObjectReserved17Information = 39,
            JobObjectReserved18Information = 40,
            JobObjectReserved19Information = 41,
            JobObjectReserved20Information = 42,
            JobObjectReserved21Information = 43,
            JobObjectReserved22Information = 44,
            JobObjectReserved23Information = 45,
            JobObjectReserved24Information = 46,
            JobObjectReserved25Information = 47,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public SizeT ProcessMemoryLimit;
            public SizeT JobMemoryLimit;
            public SizeT PeakProcessMemoryUsed;
            public SizeT PeakJobMemoryUsed;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public TimeSpan PerProcessUserTimeLimit;
            public TimeSpan PerJobUserTimeLimit;
            public JOBOBJECT_LIMIT_FLAGS LimitFlags;
            public SizeT MinimumWorkingSetSize;
            public SizeT MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public nuint Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [Flags]
        public enum JOBOBJECT_LIMIT_FLAGS
        {
            JOB_OBJECT_LIMIT_WORKINGSET = 0x00000001,
            JOB_OBJECT_LIMIT_PROCESS_TIME = 0x00000002,
            JOB_OBJECT_LIMIT_JOB_TIME = 0x00000004,
            JOB_OBJECT_LIMIT_ACTIVE_PROCESS = 0x00000008,
            JOB_OBJECT_LIMIT_AFFINITY = 0x00000010,
            JOB_OBJECT_LIMIT_PRIORITY_CLASS = 0x00000020,
            JOB_OBJECT_LIMIT_PRESERVE_JOB_TIME = 0x00000040,
            JOB_OBJECT_LIMIT_SCHEDULING_CLASS = 0x00000080,
            JOB_OBJECT_LIMIT_PROCESS_MEMORY = 0x00000100,
            JOB_OBJECT_LIMIT_JOB_MEMORY = 0x00000200,
            JOB_OBJECT_LIMIT_DIE_ON_UNHANDLED_EXCEPTION = 0x00000400,
            JOB_OBJECT_LIMIT_BREAKAWAY_OK = 0x00000800,
            JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK = 0x00001000,
            JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000,
            JOB_OBJECT_LIMIT_SUBSET_AFFINITY = 0x00004000,
            JOB_OBJECT_LIMIT_JOB_MEMORY_LOW = 0x00008000,
            JOB_OBJECT_LIMIT_JOB_READ_BYTES = 0x00010000,
            JOB_OBJECT_LIMIT_JOB_WRITE_BYTES = 0x00020000,
            JOB_OBJECT_LIMIT_RATE_CONTROL = 0x00040000,
            JOB_OBJECT_LIMIT_CPU_RATE_CONTROL = JOB_OBJECT_LIMIT_RATE_CONTROL,
            JOB_OBJECT_LIMIT_IO_RATE_CONTROL = 0x00080000,
            JOB_OBJECT_LIMIT_NET_RATE_CONTROL = 0x00100000,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        public struct SizeT
        {
            private nuint val;

            public ulong Value
            {
                readonly get => val;
                private set => val = new UIntPtr(value);
            }
        }
    }

    private readonly nint hJob;

    public ChildProcessTracer()
    {
        if (Environment.OSVersion.Version < new Version(6, 2))
        {
            return;
        }

        hJob = Kernel32.CreateJobObject(null, $"{nameof(ChildProcessTracer)}-{Process.GetCurrentProcess().Id}");

        Kernel32.JOBOBJECT_EXTENDED_LIMIT_INFORMATION extendedInfo = new()
        {
            BasicLimitInformation = new Kernel32.JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = Kernel32.JOBOBJECT_LIMIT_FLAGS.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            }
        };

        int length = Marshal.SizeOf(extendedInfo);
        nint extendedInfoPtr = Marshal.AllocHGlobal(length);

        try
        {
            Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);

            if (!Kernel32.SetInformationJobObject(hJob, Kernel32.JOBOBJECTINFOCLASS.JobObjectExtendedLimitInformation, extendedInfoPtr, (uint)length))
            {
                Debug.WriteLine($"Failed to set information for job object. Error: {Marshal.GetLastWin32Error()}");
            }
        }
        finally
        {
            // Free allocated memory after job object is set.
            Marshal.FreeHGlobal(extendedInfoPtr);
        }
    }

    /// <summary>
    /// Adds a process to the tracking job. If the parent process is terminated, this process will also be automatically terminated.
    /// </summary>
    /// <param name="hProcess">The child process to be tracked.</param>
    /// <exception cref="ArgumentNullException">Thrown when the process argument is null.</exception>
    public void AddChildProcess(nint hProcess)
    {
        if (!Kernel32.AssignProcessToJobObject(hJob, hProcess))
        {
            Debug.WriteLine($"Failed to assign process to job object. Error: {Marshal.GetLastWin32Error()}");
        }
    }
}
