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
using System.Collections.Generic;

namespace Cbonnell.Aufs4Win
{
    /// <summary>
    /// The base class for file/directory creation policies
    /// </summary>
    public abstract class CreationPolicy
    {
        /// <summary>
        /// A list of <see cref="Member"/> objects that constitute the storage members of the virtual drive
        /// </summary>
        protected readonly List<Member> members = new List<Member>();

        /// <summary>
        /// The constructor for file/directory creation policies. Subclasses should always call this constructor.
        /// </summary>
        /// <param name="members">A collection of storage members</param>
        public CreationPolicy(IEnumerable<Member> members)
        {
            if (members == null)
            {
                throw new ArgumentNullException("members");
            }
            this.members.AddRange(members);
        }

        /// <summary>
        /// Returns the path to the physical location where a new file should be created. Classes inheriting from this class need to provide a concrete implementation of this method.
        /// </summary>
        /// <param name="path">The path relative to the root of the virtual drive</param>
        /// <returns>The absolute path to the physical location of the new file</returns>
        /// <remarks>If no suitable location is determined, this method should return null</remarks>
        public abstract PathInformation GetPathInformationForNewFile(string path);

        /// <summary>
        /// Returns the path to the physical location where a new directory should be created. Classes inheriting from this class need to provide a concrete implementation of this method.
        /// </summary>
        /// <param name="path">The path relative to the root of the virtual drive</param>
        /// <returns>The absolute path to the physical location of the new directory</returns>
        /// <remarks>If no suitable location is determined, this method should return null</remarks>
        public abstract PathInformation GetPathInformationForNewDirectory(string path);
    }
}