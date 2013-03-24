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
using System.Text;
using System.Collections.ObjectModel;
using System.IO;
using System.Diagnostics;

namespace Cbonnell.Aufs4Win
{
    internal class DefaultCreationPolicy : CreationPolicy
    {
        private readonly List<Member> writeableMembers = new List<Member>();

        public DefaultCreationPolicy(IEnumerable<Member> members)
            : base(members)
        {
            if (this.members.Count == 0)
            {
                throw new ArgumentException("You must supply at least one member", "members");
            }

            foreach (Member m in this.members)
            {
                if (!m.ReadOnly)
                {
                    this.writeableMembers.Add(m);
                }
            }
        }

        public override PathInformation GetPathInformationForNewFile(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }
            Member member = this.findMostFreeSpaceMember();
            if (member == null)
            {
                return null;
            }

            string realDirPath = Path.GetDirectoryName(member.GetRootedPath(path));

            Trace.TraceInformation(String.Format("Checking for directory existance at {0}", realDirPath));
            try
            {
                if (!Directory.Exists(realDirPath))
                {
                    Directory.CreateDirectory(realDirPath);
                    Trace.TraceInformation(String.Format("Created directory at {0} for new file", realDirPath));
                }
                else
                {
                    Trace.TraceInformation(String.Format("Directory at {0} already exists, no need to create", realDirPath));
                }
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);
                return null;
            }

            string fileName = Path.GetFileName(path);
            return new PathInformation(member, FileSystemObjectKind.File, Path.Combine(realDirPath, fileName));
        }

        public override PathInformation GetPathInformationForNewDirectory(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }
            Member member = this.findMostFreeSpaceMember();
            if (member == null)
            {
                return null;
            }

            return new PathInformation(member, FileSystemObjectKind.Directory, member.GetRootedPath(path));
        }

        private Member findMostFreeSpaceMember()
        {
            Trace.TraceInformation("Determining member with most free space");
            Member bestMember = null;
            long mostFreeAvailableSpace = Int64.MinValue;
            foreach (Member m in this.writeableMembers)
            {
                long availableFreeSpace = m.MemberDriveInfo.AvailableFreeSpace;
                Trace.TraceInformation(String.Format("Member at {0} has {1} bytes free and available", m.MemberPath, availableFreeSpace));
                if (availableFreeSpace > mostFreeAvailableSpace)
                {
                    bestMember = m;
                    mostFreeAvailableSpace = availableFreeSpace;
                }
            }
            if (bestMember != null)
            {
                Trace.TraceInformation(String.Format("Member at {0} has the most free space", bestMember.MemberPath));
            }
            else
            {
                Trace.TraceWarning("No writeable members with free space were found");
            }
            return bestMember;
        }

    }
}