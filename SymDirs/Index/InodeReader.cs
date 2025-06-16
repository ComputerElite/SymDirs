using System.Runtime.InteropServices;

namespace SymDirs.Index;

class InodeReader
{
    public static bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    // Define stat structure for Unix-based systems
    [StructLayout(LayoutKind.Sequential)]
    struct Stat
    {
        public ulong st_dev;
        public ulong st_ino;   // Inode number
        public ulong st_nlink;
        public uint st_mode;
        public uint st_uid;
        public uint st_gid;
        public uint __pad0;
        public ulong st_rdev;
        public long st_size;
        public long st_blksize;
        public long st_blocks;
        public Timespec st_atim;
        public Timespec st_mtim;
        public Timespec st_ctim;
        private long __unused1;
        private long __unused2;
        private long __unused3;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Timespec
    {
        public long tv_sec;  // seconds
        public long tv_nsec; // nanoseconds
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int stat(string path, out Stat buf);

    public static ulong? GetInodeNumber(string path)
    {
        if (IsWindows)
        {
            Console.WriteLine("Inode numbers are not available on Windows.");
            return null; // or throw or return dummy value
        }

        if (stat(path, out Stat statBuf) == 0)
        {
            return statBuf.st_ino;
        }
        Console.WriteLine($"Failed to stat file '{path}'. Error: {Marshal.GetLastWin32Error()}");
        return null;
    }
}
