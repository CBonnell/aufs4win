/* Copyright 2013 Corey Bonnell

   This file is part of Aufs4Win.

    Aufs4Win is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Aufs4Win is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Aufs4Win.  If not, see <http://www.gnu.org/licenses/>.
*/


using Dokan;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Cbonnell.Aufs4Win
{
    internal class AufsImpl : DokanOperations
    {
        private readonly MergedVolumeInfo volumeInfo;
        private readonly Dictionary<ulong, FileStream> openHandles = new Dictionary<ulong, FileStream>();
        private readonly ReadOnlyCollection<Member> members;
        private readonly CreationPolicy policy;
        private readonly object contextWriteLock = new object();

        private ulong context = 0;

        private delegate void FileStreamOperationDelegate(FileStream stream);
        private delegate void GenericFileSystemOperationDelegate();
        private delegate void GenericPathInformationOperationDelegate(PathInformation pathInfo);

        internal AufsImpl(MergedVolumeInfo volumeInfo)
        {
            if (volumeInfo == null)
            {
                throw new ArgumentNullException("volumeInfo");
            }

            this.volumeInfo = volumeInfo;
            this.members = volumeInfo.Members;
            this.policy = volumeInfo.Policy;
        }

        public int CreateFile(string path, FileAccess access, FileShare sharing, FileMode mode, FileOptions options, DokanFileInfo info)
        {
            Trace.TraceInformation("CreateFile called for file " + path);
            lock (this.contextWriteLock)
            {
                info.Context = this.context++;
            }
            Trace.TraceInformation("Context ID for handle is: " + info.Context);

            PathInformation pathInfo = this.getPathInformation(path);
            if (pathInfo != null && pathInfo.ObjectKind == FileSystemObjectKind.Directory)
            {
                Trace.TraceInformation("It's a directory at " + pathInfo.RealPath);

                info.IsDirectory = true;
                return 0;
            }
            info.IsDirectory = false;

            if (pathInfo == null && mode != FileMode.Create && mode != FileMode.CreateNew && mode != FileMode.OpenOrCreate)
            {
                Trace.TraceInformation(String.Format("File system object not found and file mode did not specify creation of new file (file mode was {0}), returning error", mode));
                return -DokanNet.ERROR_FILE_NOT_FOUND;
            }

            if (pathInfo != null && pathInfo.ObjectKind == FileSystemObjectKind.File && pathInfo.ContainingMember.ReadOnly && ((access & FileAccess.Write) != 0))
            {
                Trace.TraceInformation(String.Format("Write access (access mode was {0}) was specified for a file ({1}) on read-only member at {2}, returning error", access, pathInfo.RealPath, pathInfo.ContainingMember.MemberPath));
                return -DokanNet.ERROR_ACCESS_DENIED;
            }

            if (pathInfo == null)
            {
                pathInfo = this.policy.GetPathInformationForNewFile(path);
                if (pathInfo != null && pathInfo.ContainingMember.ReadOnly)
                {
                    Trace.TraceError(String.Format("Creation policy specified the creation of file {0} on read-only member at {1}", pathInfo.RealPath, pathInfo.ContainingMember.MemberPath));
                    return -DokanNet.ERROR_ACCESS_DENIED;
                }
                if (pathInfo == null)
                {
                    Trace.TraceInformation(String.Format("No suitable member for new file \"{0}\" found", path));
                    return -DokanNet.ERROR_ACCESS_DENIED;
                }
            }

            SafeFileHandle handle = NativeMethods.CreateFile(pathInfo.RealPath, access, sharing, IntPtr.Zero, mode, (uint)options, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                int errorCode = AufsImpl.getLastError();
                Trace.TraceInformation(String.Format("CreateFile on file {0} failed with error code of {1}", pathInfo.RealPath, errorCode));
                return errorCode;
            }

            FileStream stream;
            try
            {
                stream = new FileStream(handle, access);
            }
            catch (ArgumentException e)
            {
                Trace.TraceWarning(String.Format("Could not instantiate FileStream object for {0} because: {1} (managed exception; no Win32 error code)", pathInfo.RealPath, e.Message));
                return -2;
            }
            catch (Exception e)
            {
                int errorCode = AufsImpl.getLastError();
                Trace.TraceWarning(String.Format("Could not instantiate FileStream object for {0} because: {1} (error code {2})", pathInfo.RealPath, e.Message, errorCode));
                return errorCode;
            }

            lock (this.openHandles)
            {
                this.openHandles.Add((ulong)info.Context, stream);
            }
            Trace.TraceInformation("CreateFile succeeded");
            return 0; // there's the right way, and then there's the wrong way... the right way busts Notepad, so we're gonna use the wrong way. The commented line below is supposedly the right way
            // return (alreadyExists && (mode == FileMode.Create || mode == FileMode.OpenOrCreate)) ? -DokanNet.ERROR_ALREADY_EXISTS : 0;
        }

        public int OpenDirectory(string path, DokanFileInfo info)
        {
            Trace.TraceInformation("Entering OpenDirectory for path " + path);
            lock (this.contextWriteLock)
            {
                info.Context = this.context++;
            }
            PathInformation pathInfo = this.getPathInformation(path);

            if (pathInfo == null || pathInfo.ObjectKind != FileSystemObjectKind.Directory)
            {
                return -DokanNet.ERROR_PATH_NOT_FOUND;
            }

            return 0;
        }

        public int CreateDirectory(string path, DokanFileInfo info)
        {
            Trace.TraceInformation("Entering CreateDirectory for path " + path);
            PathInformation pathInfo = this.getPathInformation(path);
            if (pathInfo != null)
            {
                Trace.TraceInformation(String.Format("Could not create the directory at {0} because it already exists", path));
                return -DokanNet.ERROR_ALREADY_EXISTS;
            }

            PathInformation newPathInfo = this.policy.GetPathInformationForNewDirectory(path);
            if (newPathInfo == null)
            {
                return -DokanNet.ERROR_ACCESS_DENIED;
            }

            return this.executeGenericFileSystemOperation(newPathInfo, true, delegate()
            {
                Directory.CreateDirectory(newPathInfo.RealPath);
            });
        }

        public int GetDiskFreeSpace(ref ulong freeBytesAvailable, ref ulong totalBytes, ref ulong totalFreeBytes, DokanFileInfo info)
        {
            Trace.TraceInformation("Entering GetDiskFreeSpace");
            freeBytesAvailable = 0;
            totalBytes = 0;
            totalFreeBytes = 0;
            foreach (DriveInfo driveInfo in DriveInfo.GetDrives())
            {
                foreach(Member m in this.members)
                {
                    if(driveInfo.Name.StartsWith(m.MemberDriveLetter.ToString()))
                    {
                        freeBytesAvailable += m.ReadOnly ? 0 : (ulong)driveInfo.AvailableFreeSpace;
                        totalBytes += (ulong)driveInfo.TotalSize;
                        totalFreeBytes += (ulong)driveInfo.TotalFreeSpace;
                        continue;
                    }
                }
            }
            
            return 0;
        }

        public int ReadFile(string path, byte[] buffer, ref uint readBytes, long offset, DokanFileInfo info)
        {
            Trace.TraceInformation("Entering ReadFile for context ID " + info.Context);
            readBytes = 0;
            uint readBytesTemp = 0;
            int result = this.executeFileStreamOperation(info, delegate(FileStream stream)
            {
                if (stream.Position != offset)
                {
                    stream.Seek(offset, SeekOrigin.Begin);
                }
                readBytesTemp = (uint)stream.Read(buffer, 0, buffer.Length);
            });
            if (result == 0)
            {
                readBytes = readBytesTemp;
            }
            return result;
        }

        public int WriteFile(string path, byte[] buffer, ref uint writtenBytes, long offset, DokanFileInfo info)
        {
            Trace.TraceInformation("Entering WriteFile for context ID " + info.Context);
            writtenBytes = 0;
            uint writtenBytesTemp = 0;
            int result = this.executeFileStreamOperation(info, delegate(FileStream stream)
            {
                if (stream.Position != offset)
                {
                    stream.Seek(offset, SeekOrigin.Begin);
                }
                stream.Write(buffer, 0, buffer.Length);
                writtenBytesTemp = (uint)buffer.Length;
            });
            if (result == 0)
            {
                writtenBytes = writtenBytesTemp;
            }
            return result;
        }

        public int FlushFileBuffers(string path, DokanFileInfo info)
        {
            Trace.TraceInformation("Entering FlushFileBuffers for context ID " + info.Context);
            return this.executeFileStreamOperation(info, delegate(FileStream stream)
            {
                stream.Flush();
            });
        }

        public int GetFileInformation(string path, Dokan.FileInformation realFileInfo, Dokan.DokanFileInfo info)
        {
            Trace.TraceInformation("Entering GetFileInformation for path " + path);
            PathInformation pathInfo = this.getPathInformation(path);
            if (pathInfo == null)
            {
                Trace.TraceInformation(String.Format("The path {0} does not exist, cannot get file information", path));
                return -DokanNet.ERROR_PATH_NOT_FOUND;
            }

            GenericPathInformationOperationDelegate operation = delegate(PathInformation pInfo)
            {
                FileSystemInfo objectInfo = AufsImpl.getFileSystemInfoForPathInfo(pInfo);

                if (pInfo.ObjectKind == FileSystemObjectKind.File)
                {
                    realFileInfo.Length = (objectInfo as FileInfo).Length;
                }
                else
                {
                    realFileInfo.Length = 0;
                }

                realFileInfo.FileName = objectInfo.Name;
                realFileInfo.Attributes = objectInfo.Attributes;
                realFileInfo.CreationTime = objectInfo.CreationTime;
                realFileInfo.LastAccessTime = objectInfo.LastAccessTime;
                realFileInfo.LastWriteTime = objectInfo.LastWriteTime;
            };
            
            return this.executeGenericPathInformationOperation(pathInfo, false, operation);
        }

        public int FindFiles(string path, ArrayList fileInfoArrList, DokanFileInfo info)
        {
            Trace.TraceInformation("Entering FindFiles for path " + path);
            if (!this.mergeDirectoryListing(path, fileInfoArrList))
            {
                Trace.TraceInformation(String.Format("The path {0} does not exist, cannot enumerate directory objects", path));
                return -DokanNet.ERROR_PATH_NOT_FOUND;
            }
            return 0;
        }

        public int SetFileAttributes(string path, FileAttributes attributes, DokanFileInfo info)
        {
            Trace.TraceInformation("Entering SetFileAttributes for path " + path);
            PathInformation pathInfo = this.getPathInformation(path);
            if (pathInfo == null)
            {
                Trace.TraceInformation(String.Format("The path {0} does not exist, cannot change file time", path));
                return -DokanNet.ERROR_PATH_NOT_FOUND;
            }

            GenericPathInformationOperationDelegate operation = delegate(PathInformation pInfo)
            {
                FileSystemInfo objectInfo = AufsImpl.getFileSystemInfoForPathInfo(pInfo);
                objectInfo.Attributes = attributes;
            };

            if (pathInfo.ObjectKind == FileSystemObjectKind.File)
            {
                return this.executeGenericPathInformationOperation(pathInfo, true, operation);
            }

            PathInformation[] dirsToModify = this.getAllDirectoriesAtPath(path);
            return this.executeGenericMultipleFileObjectOperation(dirsToModify, true, operation);
        }

        public int SetFileTime(string path, DateTime creationTime, DateTime accessTime, DateTime modifiedTime, DokanFileInfo info)
        {
            Trace.TraceInformation("Entering SetFileTime for path " + path);
            PathInformation pathInfo = this.getPathInformation(path);
            if (pathInfo == null)
            {
                Trace.TraceInformation(String.Format("The path {0} does not exist, cannot change file time", path));
                return -DokanNet.ERROR_PATH_NOT_FOUND;
            }

            GenericPathInformationOperationDelegate operation = delegate(PathInformation pInfo)
            {
                FileSystemInfo objectInfo = AufsImpl.getFileSystemInfoForPathInfo(pInfo);
                objectInfo.CreationTime = creationTime;
                objectInfo.LastAccessTime = accessTime;
                objectInfo.LastWriteTime = modifiedTime;
            };

            if (pathInfo.ObjectKind == FileSystemObjectKind.File)
            {
                return this.executeGenericPathInformationOperation(pathInfo, true, operation);
            }

            PathInformation[] dirsToModify = this.getAllDirectoriesAtPath(path);
            return this.executeGenericMultipleFileObjectOperation(dirsToModify, true, operation);
        }

        public int DeleteFile(string path, DokanFileInfo info)
        {
            Trace.TraceInformation("Entering DeleteFile for path " + path);
            PathInformation pathInfo = this.getPathInformation(path);
            if (pathInfo == null || pathInfo.ObjectKind != FileSystemObjectKind.File)
            {
                Trace.TraceInformation(String.Format("The path {0} is not a file, cannot delete", path));
                return -DokanNet.ERROR_FILE_NOT_FOUND;
            }
            return this.executeGenericFileSystemOperation(pathInfo, true, delegate()
            {
                File.Delete(pathInfo.RealPath);
            });
        }

        public int DeleteDirectory(string path, DokanFileInfo info)
        {
            Trace.TraceInformation("Entering DeleteDirectory for path " + path);
            PathInformation[] dirsToDelete = this.getAllDirectoriesAtPath(path);
            if (dirsToDelete.Length == 0)
            {
                Trace.TraceInformation(String.Format("The path {0} is not a directory, cannot delete", path));
                return -DokanNet.ERROR_PATH_NOT_FOUND;
            }

            return this.executeGenericMultipleFileObjectOperation(dirsToDelete, true, delegate(PathInformation pathInfo)
            {
                Directory.Delete(pathInfo.RealPath);
            });
        }

        public int SetEndOfFile(string path, long length, DokanFileInfo info)
        {
            Trace.TraceInformation("Entering SetEndOfFile for context ID " + info.Context);
            return this.executeFileStreamOperation(info, delegate(FileStream stream)
            {
                stream.SetLength(length);
            });
        }

        public int LockFile(string file, long offset, long length, DokanFileInfo info)
        {
            Trace.TraceInformation("Entering LockFile for context ID " + info.Context);
            return this.executeFileStreamOperation(info, delegate(FileStream stream)
            {
                stream.Lock(offset, length);
            });
        }

        public int UnlockFile(string file, long offset, long length, DokanFileInfo info)
        {
            Trace.TraceInformation("Entering UnlockFile for context ID " + info.Context);
            return this.executeFileStreamOperation(info, delegate(FileStream stream)
            {
                stream.Unlock(offset, length);
            });
        }

        public int MoveFile(string oldPath, string newPath, bool replace, DokanFileInfo info)
        {
            Trace.TraceInformation("EnteringMoveFile for path " + oldPath);

            PathInformation oldPathInfo = this.getPathInformation(oldPath);
            if (oldPathInfo == null)
            {
                Trace.TraceInformation(String.Format("The file {0} does not exist, could not move", oldPath));
                return -DokanNet.ERROR_FILE_NOT_FOUND;
            }

            PathInformation newPathInfo = this.getPathInformation(newPath);
            if (!replace && newPathInfo != null)
            {
                Trace.TraceInformation(String.Format("The file {0} exists and the replace file flag was off, could not move", newPath));
                return -DokanNet.ERROR_FILE_EXISTS;
            }

            Func<PathInformation, int> directoryCreator = delegate(PathInformation pInfo)
            {
                if (pInfo.ContainingMember.ReadOnly)
                {
                    Trace.TraceInformation(String.Format("The path \"{0}\" is contained on a read-only member, cannot move", pInfo.RealPath));
                    return -DokanNet.ERROR_ACCESS_DENIED;
                }
                string destinationDirectory = Path.GetDirectoryName(pInfo.ContainingMember.GetRootedPath(newPath));
                int error = this.executeGenericFileSystemOperation(pInfo, true, delegate()
                {
                    if (!Directory.Exists(destinationDirectory))
                    {
                        Trace.TraceInformation(String.Format("The destination directory {0} does not exist, creating directory", destinationDirectory));
                        Directory.CreateDirectory(destinationDirectory);
                    }
                });
                bool success = NativeMethods.MoveFileEx(pInfo.RealPath, pInfo.ContainingMember.GetRootedPath(newPath), replace ? NativeMethods.MOVEFILE_REPLACE_EXISTING : 0);
                return success ? 0 : AufsImpl.getLastError();
            };

            if(oldPathInfo.ObjectKind == FileSystemObjectKind.File)
            {
                int error = directoryCreator.Invoke(oldPathInfo);
                if(error != 0) 
                {
                    Trace.TraceInformation(String.Format("MoveFile failed, old path: {0}, new path: {1}, error: {2}", oldPath, newPath, error));
                }
                return error;
            }
            
            // if the code is executing here, then we are moving a directory

            PathInformation[] dirsToMove = this.getAllDirectoriesAtPath(oldPath);
            foreach(PathInformation pInfo in dirsToMove)
            {
                int error = directoryCreator.Invoke(pInfo);
                if(error != 0) 
                {
                    Trace.TraceInformation(String.Format("MoveFile failed, old path: {0}, new path: {1}, error: {2}", oldPath, newPath, error));
                    return error;
                }
            }

            return 0;   
        }

        public int CloseFile(string path, DokanFileInfo info)
        {
            Trace.TraceInformation("Entering CloseFile for context ID " + info.Context);
            FileStream str = null;
            bool foundStream = false;

            lock (this.openHandles)
            {
                if (info.Context != null && this.openHandles.TryGetValue((ulong)info.Context, out str))
                {
                    foundStream = true;
                    this.openHandles.Remove((ulong)info.Context);
                }
            }

            Trace.TraceInformation("Found handle: " + foundStream);

            try
            {
                if (foundStream)
                {
                    str.Close();
                }
            }
            catch (ArgumentException e)
            {
                Trace.TraceWarning(String.Format("Could not close stream because: {0} (managed exception; no Win32 error code)", e.Message));
                return -2;
            }
            catch (Exception e)
            {
                int errorCode = AufsImpl.getLastError();
                Trace.TraceWarning(String.Format("Could not close stream because: {0} (error code {1})", e.Message, errorCode));
                return errorCode;
            }
            return 0;    
        }

        public int Cleanup(string path, DokanFileInfo info)
        {
            Trace.TraceInformation("Entering Cleanup for context ID " + info.Context);
            return 0;
        }

        public int Unmount(DokanFileInfo info)
        {
            Trace.TraceInformation("Unmounting drive");
            lock (this.openHandles)
            {
                if (this.openHandles.Count == 0)
                {
                    Trace.TraceInformation("No open handles, drive unmounted successfully");
                    return 0;
                }
            }
            Trace.TraceError("One or more open handles, could not unmount drive");
            return -1;
        }

        public int SetAllocationSize(string path, long allocationSize, DokanFileInfo info)
        {
            Trace.TraceInformation(String.Format("SetAllocationSize of {0} for context ID {1} called, ignoring and returning success", allocationSize, info.Context));
            return 0;
        }

        private int executeGenericMultipleFileObjectOperation(IEnumerable<PathInformation> pathInfos, bool mutableOperation, GenericPathInformationOperationDelegate operation)
        {
            if (pathInfos == null)
            {
                throw new ArgumentNullException("pathInfos");
            }
            if (operation == null)
            {
                throw new ArgumentNullException("operation");
            }
            foreach (PathInformation pathInfo in pathInfos)
            {
                int result = this.executeGenericPathInformationOperation(pathInfo, mutableOperation, operation);
                if (result != 0)
                {
                    return result;
                }
            }
            return 0;
        }

        private int executeGenericPathInformationOperation(PathInformation pathInfo, bool mutableOperation, GenericPathInformationOperationDelegate operation)
        {
            if (pathInfo == null)
            {
                throw new ArgumentNullException("pathInfo");
            }
            if (operation == null)
            {
                throw new ArgumentNullException("operation");
            }
            Trace.TraceInformation("Executing operation on path " + pathInfo.RealPath);

            if (mutableOperation && pathInfo.ContainingMember.ReadOnly)
            {
                Trace.TraceInformation("Attempted to modify read-only object");
                return -DokanNet.ERROR_ACCESS_DENIED;
            }

            try
            {
                operation.Invoke(pathInfo);
            }
            catch (ArgumentException e)
            {
                Trace.TraceWarning(String.Format("Operation on path {0} failed because: {1} (managed exception; no Win32 error code)", pathInfo.RealPath, e.Message));
                return -2;
            }
            catch (Exception e)
            {
                int errorCode = AufsImpl.getLastError();
                Trace.TraceWarning(String.Format("Operation on path {0} failed because: {1} (error code of {2})", pathInfo.RealPath, e.Message, errorCode));
                return errorCode;
            }
            Trace.TraceInformation("Operation completed successfully");
            return 0;

        }

        private int executeGenericFileSystemOperation(PathInformation pathInfo, bool mutableOperation, GenericFileSystemOperationDelegate operation)
        {
            return this.executeGenericPathInformationOperation(pathInfo, mutableOperation, delegate(PathInformation pInfo)
            {
                operation.Invoke();
            });
        }

        private int executeFileStreamOperation(DokanFileInfo info, FileStreamOperationDelegate operation)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }
            if (operation == null)
            {
                throw new ArgumentNullException("operation");
            }

            Trace.TraceInformation("Executing generic stream operation on handle with context ID " + info.Context);
            FileStream stream;
            lock (this.openHandles)
            {
                if (!this.openHandles.TryGetValue((ulong)info.Context, out stream))
                {
                    Trace.TraceWarning("Could not find corresponding handle for context ID " + info.Context);
                    return -DokanNet.ERROR_ACCESS_DENIED;
                }
            }

            try
            {
                operation.Invoke(stream);
            }
            catch (ArgumentException e)
            {
                Trace.TraceWarning(String.Format("Operation on handle for context ID {0} failed because: {1} (managed exception; no Win32 error code)", info.Context, e.Message));
                return -2;
            }
            catch (Exception e)
            {
                int errorCode = AufsImpl.getLastError();
                Trace.TraceWarning(String.Format("Operation on handle for context ID {0} failed because: {1} (error code of {2})", info.Context, e.Message, errorCode));
                return errorCode;
            }
            Trace.TraceInformation("Operation completed successfully");
            return 0;
        }

        private bool mergeDirectoryListing(string path, ArrayList fileArrList)
        {
            bool found = false;
            KeyedCollectionImpl<string, FileInformation> directoryItems = new KeyedCollectionImpl<string, FileInformation>(delegate(FileInformation itemInfo)
            {
                return itemInfo.FileName;
            });

            Trace.TraceInformation(String.Format("Creating merged directory listing for " + path));
            foreach (Member m in this.members)
            {
                if(m.GetFileSystemObjectKind(path) != FileSystemObjectKind.Directory)
                {
                    continue;
                }
                string realPath = m.GetRootedPath(path);
                found = true;
                foreach (FileSystemInfo fsInfo in new DirectoryInfo(realPath).GetFileSystemInfos())
                {
                    FileInformation fi = new FileInformation();
                    fi.Attributes = fsInfo.Attributes;
                    fi.CreationTime = fsInfo.CreationTime;
                    fi.FileName = fsInfo.Name;
                    fi.LastAccessTime = fsInfo.LastAccessTime;
                    fi.LastWriteTime = fsInfo.LastWriteTime;
                    if (fsInfo is FileInfo)
                    {
                        fi.Length = (fsInfo as FileInfo).Length;
                    }
                    else
                    {
                        fi.Length = 0;
                    }
                    if (!directoryItems.Contains(fi.FileName))
                    {
                        directoryItems.Add(fi);
                        Trace.TraceInformation(String.Format("Found object {0}, size {1} bytes", fi.FileName, fi.Length));
                    }
                }
            }
            foreach (FileInformation fi in directoryItems)
            {
                fileArrList.Add(fi);
            }
            return found;
        }

        private PathInformation[] getAllDirectoriesAtPath(string fakePath)
        {
            List<PathInformation> dirPathInfos = new List<PathInformation>();
            foreach (Member m in this.members)
            {
                if (m.GetFileSystemObjectKind(fakePath) == FileSystemObjectKind.Directory)
                {
                    dirPathInfos.Add(new PathInformation(m, FileSystemObjectKind.Directory, m.GetRootedPath(fakePath)));
                }
            }
            return dirPathInfos.ToArray();
        }

        private PathInformation getPathInformation(string fakePath)
        {
            Trace.TraceInformation(String.Format("Searching for path {0} in members", fakePath));
            foreach (Member m in this.members)
            {
                Trace.TraceInformation(String.Format("Checking existance of object in member at {0}", m.MemberPath));
                FileSystemObjectKind objectKind = m.GetFileSystemObjectKind(fakePath);
                if (objectKind == FileSystemObjectKind.None)
                {
                    continue;
                }
                string realPath = m.GetRootedPath(fakePath);
                PathInformation pathInfo = new PathInformation(m, objectKind, realPath);
                Trace.TraceInformation(String.Format("Object {0} found on member at {1}", pathInfo, pathInfo.ContainingMember.MemberPath));
                return pathInfo;
            }
            Trace.TraceInformation("Could not find object in members");
            return null;
        }

        private static FileSystemInfo getFileSystemInfoForPathInfo(PathInformation pathInfo)
        {
            if (pathInfo == null)
            {
                throw new ArgumentNullException("pathInfo");
            }
            switch (pathInfo.ObjectKind)
            {
                case FileSystemObjectKind.File:
                    return new FileInfo(pathInfo.RealPath);
                case FileSystemObjectKind.Directory:
                    return new DirectoryInfo(pathInfo.RealPath);
                default:
                    return null;
            }
        }

        private static int getLastError()
        {
            return -Marshal.GetLastWin32Error();
        }

        private class NativeMethods
        {
            private NativeMethods() { }

            [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern SafeFileHandle CreateFile(
                string fileName,
                [MarshalAs(UnmanagedType.U4)] FileAccess fileAccess,
                [MarshalAs(UnmanagedType.U4)] FileShare fileShare,
                IntPtr securityAttributes,
                [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
                [MarshalAs(UnmanagedType.U4)] uint flags,
                IntPtr template);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern bool MoveFileEx(
                string lpExistingFileName,
                string lpNewFileName,
                int dwFlags);

            public const int MOVEFILE_REPLACE_EXISTING = 1;
        }
    }

}

