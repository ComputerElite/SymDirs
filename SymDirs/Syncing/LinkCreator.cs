using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SymDirs.Syncing;
using System.Runtime.CompilerServices;

using static LinuxNativeMethods.LinkErrors;

class LinkCreator
{
    public static bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static bool IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    public static void CreateLink(string existingPath, string targetPath)
    {
        if (IsWindows)
        {
            throw new PlatformNotSupportedException("Hard link creation on windows is currently not supported");
        }
        // Most likely windows
        if (IsLinux)
        {
            LinuxNativeMethods.CreateHardLink(existingPath, targetPath);
            return;
        }
    }
}

// Source https://github.com/ProfessionalCSharp/ProfessionalCSharp2021/blob/main/5_More/PInvoke/PInvokeSampleLib/Linux/LinuxNativeMethods.cs#L10

//MIT License
// 
// Copyright (c) 2020 Professional C#
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

internal static partial class LinuxNativeMethods
{
    internal enum LinkErrors
    {
        EPERM = 1,
        ENOENT = 2,
        EIO = 5,
        EACCES = 13,
        EEXIST = 17,
        EXDEV = 18,
        ENOSPC = 28,
        EROFS = 30,
        EMLINK = 31
    }

    private static readonly Dictionary<LinkErrors, string> _errorMessages = new()
    {
        { EPERM, "On GNU/Linux and GNU/Hurd systems and some others, you cannot make links to directories.Many systems allow only privileged users to do so." },
        { ENOENT, "The file named by oldname doesn’t exist. You can’t make a link to a file that doesn’t exist." },
        { EIO, "A hardware error occurred while trying to read or write to the filesystem." },
        { EACCES, "You are not allowed to write to the directory in which the new link is to be written." },
        { EEXIST, "There is already a file named newname. If you want to replace this link with a new link, you must remove the old link explicitly first." },
        { EXDEV, "The directory specified in newname is on a different file system than the existing file." },
        { ENOSPC, "The directory or file system that would contain the new link is full and cannot be extended." },
        { EROFS, "The directory containing the new link can’t be modified because it’s on a read - only file system." },
        { EMLINK, "There are already too many links to the file named by oldname. (The maximum number of links to a file is LINK_MAX; see Section 32.6 [Limits on File System Capacity], page 904.)" }
    };


    //[LibraryImport("libc", EntryPoint = "link", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    //[UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    [DllImport("libc", SetLastError = true)]
    private static extern int link(string oldpath, string newpath);

    internal static void CreateHardLink(string oldFileName,
                                        string newFileName)
    {
        Console.WriteLine($"{oldFileName} -> {newFileName}");
        if(File.Exists(newFileName)) File.Delete(newFileName);
        string? parentDirectoryPath = Directory.GetParent(newFileName)?.FullName;
        if (parentDirectoryPath == null) throw new Exception("Parent Directory not found");
        if (!Directory.Exists(parentDirectoryPath)) Directory.CreateDirectory(parentDirectoryPath);
        int result = link(oldFileName, newFileName);
        if (result != 0)
        {
            int errorCode = Marshal.GetLastPInvokeError();
            if (!_errorMessages.TryGetValue((LinkErrors)errorCode, out string? errorText))
            {
                errorText = "No error message defined";
            }
            throw new IOException(errorText.Replace("newname", newFileName).Replace("oldname", oldFileName), errorCode);
        }
    }
}