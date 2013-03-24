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
using System.IO;
using System.Reflection;

namespace Cbonnell.Aufs4Win
{
    internal class PolicyLoader
    {
		private static readonly Dictionary<string, Type> policies = new Dictionary<string, Type>();

        private static readonly string[] POLICY_SUFFIXES = new string[] { "Policy", "CreationPolicy" };

        private PolicyLoader() { }

		static PolicyLoader()
        {
			string applicationDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			Type creationPolicyType = typeof(CreationPolicy);

			foreach(string assemPath in Directory.GetFiles(applicationDir, "*.dll", SearchOption.TopDirectoryOnly))
            {
                Assembly asm;
                try
                {
                    asm = Assembly.LoadFrom(assemPath);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.Message);
                    continue;
                }
                if (asm == null)
                {
                    continue;
                }
				foreach(Type t in asm.GetExportedTypes())
                {
					if(creationPolicyType.IsAssignableFrom(t))
                    {
                        string policyName = PolicyLoader.trimPolicyName(t.Name);
                        if (policies.ContainsKey(policyName))
                        {
                            Console.Error.WriteLine("The creation policy {0} has already been defined", policyName);
                            continue;
                        }
                        policies.Add(policyName, t);
					}
				}				
			}
		}

        public static CreationPolicy GetPolicy(string name, IEnumerable<Member> members)
        {
            Type t;
            name = trimPolicyName(name);
            if (!policies.TryGetValue(name, out t))
            {
                return new DefaultCreationPolicy(members);
            }
            return (CreationPolicy)Activator.CreateInstance(t, new object[] { members });
        }

        private static string trimPolicyName(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException("name");
            }
            foreach (string policySuffix in POLICY_SUFFIXES)
            {
                if (name.EndsWith(policySuffix, StringComparison.InvariantCultureIgnoreCase))
                {
                    name = name.Substring(0, name.LastIndexOf(policySuffix, StringComparison.InvariantCultureIgnoreCase));
                }
            }
            return name;
        }

    }
}

