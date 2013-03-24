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

using System;
using System.IO;

namespace Cbonnell.Aufs4Win
{
    /// <summary>
    /// Represents a storage member of the file system
    /// </summary>
    public class Member
    {
        /// <summary>
        /// Constructor for the class. Instantiates a <see cref="Member"/> object with ReadOnly set to false.
        /// </summary>
        /// <param name="path">The absolute path to the <see cref="Member"/> root</param>
        public Member(string path) : this(path, false) { }

        /// <summary>
        /// Constructor for the class. Instantiates a <see cref="Member"/>.
        /// </summary>
        /// <param name="path">The absolute path to the <see cref="Member"/> root</param>
        /// <param name="readOnly">Whether or not write operations are prohibited on objects within the <see cref="Member"/></param>
        public Member(string path, bool readOnly)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }
            if (!Path.IsPathRooted(path))
            {
                throw new ArgumentException(String.Format("The path {0} is not an absolute path", path), "path");
            }
            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException(path);
            }
            this.memberPath = Member.trimPath(path);
            this.readOnly = readOnly;

            foreach (DriveInfo di in DriveInfo.GetDrives()) // find the physical drive on which the member resides
            {
                if (di.Name.StartsWith(this.MemberDriveLetter.ToString()))
                {
                    this.memberDriveInfo = di;
                    break;
                }
            }
        }

        private readonly string memberPath;
        /// <summary>
        /// The absolute path to the root of the <see cref="Member"/>
        /// </summary>
        public string MemberPath
        {
            get
            {
                return this.memberPath;
            }
        }

        /// <summary>
        /// The drive letter on which the root of the <see cref="Member"/> resides
        /// </summary>
        public char MemberDriveLetter
        {
            get
            {
                string pathAllUpper = this.memberPath.ToUpperInvariant(); // convert the root path to uppercase
                return pathAllUpper.ToCharArray()[0]; // then return the first character (will be the drive letter)
            }
        }

        private readonly DriveInfo memberDriveInfo;
        /// <summary>
        /// Returns a <see cref="DriveInfo"/> object containing data about the physical drive on which the <see cref="Member"/> resides
        /// </summary>
        public DriveInfo MemberDriveInfo
        {
            get
            {
                return this.memberDriveInfo;
            }
        }

        private readonly bool readOnly;
        /// <summary>
        /// Returns whether or not write operations are prohibited on objects contained in the <see cref="Member"/>
        /// </summary>
        public bool ReadOnly
        {
            get
            {
                return this.readOnly;
            }
        }

        /// <summary>
        /// Determines the absolute path of a file system object within the <see cref="Member"/>
        /// </summary>
        /// <param name="path">The path relative to the root of the <see cref="Member"/></param>
        /// <returns>The absolute path of the file system object within the <see cref="Member"/></returns>
        public string GetRootedPath(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }
            string trimmedPath = Member.trimPath(path);
            return Path.Combine(this.memberPath, trimmedPath);
        }

        /// <summary>
        /// Determines the file system object kind (file, directory, unknown/none) at the given path
        /// </summary>
        /// <param name="path">The path relative to the root of the <see cref="Member"/></param>
        /// <returns>A <see cref="FileSystemObjectKind"/> which represents the object kind at the given path</returns>
        public FileSystemObjectKind GetFileSystemObjectKind(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }
            string trimmedPath = Member.trimPath(path);
            string realPath = Path.Combine(this.memberPath, trimmedPath);
            if (File.Exists(realPath))
            {
                return FileSystemObjectKind.File;
            }
            if (Directory.Exists(realPath))
            {
                return FileSystemObjectKind.Directory;
            }
            return FileSystemObjectKind.None;
        }

        /// <summary>
        /// Overridden GetHashCode
        /// </summary>
        public override int GetHashCode()
        {
            return this.memberPath.GetHashCode();
        }

        /// <summary>
        /// Overridden Equals
        /// </summary>
        public override bool Equals(object obj)
        {
            Member otherMember = obj as Member;
            if (otherMember == null)
            {
                return false;
            }
            return (this.memberPath == otherMember.memberPath);
        }

        private static string trimPath(string path)
        {
            return path.Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}