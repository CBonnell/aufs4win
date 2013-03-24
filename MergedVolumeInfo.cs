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
using System.Collections.ObjectModel;

namespace Cbonnell.Aufs4Win
{
    internal class MergedVolumeInfo
    {
        public MergedVolumeInfo(IEnumerable<Member> members, char driveLetter, string driveLabel, CreationPolicy policy)
        {
            if (members == null)
            {
                throw new ArgumentNullException("members");
            }
            if (driveLabel == null)
            {
                throw new ArgumentNullException("driveLabel");
            }
            if (policy == null)
            {
                throw new ArgumentNullException("policy");
            }
            if (!Char.IsLetter(driveLetter))
            {
                throw new ArgumentException(String.Format("{0} is not a valid drive letter", driveLetter), "driveLetter");
            }
            this.driveLetter = Char.ToUpperInvariant(driveLetter);

            this.members.AddRange(members);
            if (this.members.Count == 0)
            {
                throw new ArgumentException("Must supply at least one Member", "members");
            }

            if (MergedVolumeInfo.doesDriveExist(this.driveLetter))
            {
                throw new ArgumentException(String.Format("The drive letter \"{0}\" is already in use", this.driveLetter), "driveLetter");
            }

            this.driveLabel = driveLabel;
            this.policy = policy;
        }

        private readonly char driveLetter;
        public char DriveLetter
        {
            get
            {
                return this.driveLetter;
            }
        }

        private readonly string driveLabel;
        public string DriveLabel
        {
            get
            {
                return this.driveLabel;
            }
        }

        private readonly List<Member> members = new List<Member>();
        public ReadOnlyCollection<Member> Members
        {
            get
            {
                return this.members.AsReadOnly();
            }
        }

        private readonly CreationPolicy policy;
        public CreationPolicy Policy
        {
            get
            {
                return this.policy;
            }
        }

        /// <summary>
        /// Determines whether or not a given drive letter is in use (exists) on the local machine
        /// </summary>
        /// <param name="driveLetter">The drive letter to determine if it exists or not</param>
        /// <returns>true if the drive exists already; false if it doesn't</returns>
        private static bool doesDriveExist(char driveLetter)
        {
            string driveLetterString = driveLetter.ToString(); // there's no overload for String.StartsWith that accepts a char, so get the String of the drive letter char once instead of calling ToString() for every element in the drive collection
            foreach (string existingDrive in Environment.GetLogicalDrives())
            {
                if (existingDrive.StartsWith(driveLetterString))
                {
                    return true;
                }
            }
            return false;
        }

    }
}