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

namespace Cbonnell.Aufs4Win
{
    /// <summary>
    /// Represents the kinds of file system objects
    /// </summary>
    public enum FileSystemObjectKind
    {
        /// <summary>
        /// Represents a file system object that does not exist or is unknown
        /// </summary>
        None = 0,
        /// <summary>
        /// Represents a file system object that is a file
        /// </summary>
        File,
        /// <summary>
        /// Represents a file system object that is a directory
        /// </summary>
        Directory,
    }

    /// <summary>
    /// Represents information about the physical (real) path of a file system object
    /// </summary>
    public class PathInformation
    {
        /// <summary>
        /// Constructor for PathInformation.
        /// </summary>
        /// <param name="member">The<see cref="Member"/> in which the file system object at the designated path is contained</param>
        /// <param name="objectKind">The <see cref="FileSystemObjectKind"/> of file system object</param>
        /// <param name="realPath">The absolute path to the physical file system object</param>
        public PathInformation(Member member, FileSystemObjectKind objectKind, string realPath)
        {
            if (member == null)
            {
                throw new ArgumentNullException("member");
            }
            if (realPath == null)
            {
                throw new ArgumentNullException("realPath");
            }
            this.member = member;
            this.objectKind = objectKind;
            this.realPath = realPath;
        }

        private readonly Member member;
        /// <summary>
        /// The <see cref="Member"/> in which the file system object is contained
        /// </summary>
        public Member ContainingMember
        {
            get
            {
                return this.member;
            }
        }

        private readonly FileSystemObjectKind objectKind;
        /// <summary>
        /// The <see cref="FileSystemObjectKind"/> of the file system object
        /// </summary>
        public FileSystemObjectKind ObjectKind
        {
            get
            {
                return this.objectKind;
            }
        }

        private readonly string realPath;
        /// <summary>
        /// The absolute path of the physical file system object
        /// </summary>
        public string RealPath
        {
            get
            {
                return this.realPath;
            }
        }

        /// <summary>
        /// Returns a string that contains human-readable information concerning the object
        /// </summary>
        /// <returns>Returns a human-readable string which contains information about the object</returns>
        public override string ToString()
        {
            return String.Format("File object kind: {0}, Physical path: {1}", this.objectKind, this.realPath);
        }

        /// <summary>
        /// Overridden GetHashCode
        /// </summary>
        public override int GetHashCode()
        {
            return this.realPath.GetHashCode();
        }

        /// <summary>
        /// Overridden Equals
        /// </summary>
        public override bool Equals(object obj)
        {
            PathInformation otherPathInfo = obj as PathInformation;
            if (otherPathInfo == null)
            {
                return false;
            }
            return (this.realPath == otherPathInfo.realPath);
        }
    }
}