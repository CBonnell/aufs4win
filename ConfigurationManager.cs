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
using System.Xml;
using System.Xml.Schema;

namespace Cbonnell.Aufs4Win
{
    internal class ConfigurationManager
    {
        private readonly string configPath;
        private MergedVolumeInfo mergedVolume = null;


        public ConfigurationManager(string configPath)
        {
            if (configPath == null)
            {
                throw new ArgumentNullException("configPath");
            }
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException("Configuration file does not exist", configPath);
            }
            this.configPath = configPath;
        }

        private void readConfiguration()
        {
            bool isValid = true;
            XmlReaderSettings settings = new XmlReaderSettings();
            using (StreamReader schemaReader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(this.GetType(), "configschema.xsd"))) // load the schema embedded resource to validate the user's config file
            {
                settings.Schemas.Add(null, XmlReader.Create(schemaReader));
                settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
                settings.ValidationType = ValidationType.Schema;
                settings.ValidationEventHandler += (sender, e) => // if there's any validation events (errors), then the configuration file is invalid
                {
                    Console.Error.WriteLine(e.Message);
                    isValid = false;
                };
                using (XmlReader reader = XmlReader.Create(this.configPath, settings))
                {
                    XmlDocument configDoc = new XmlDocument();
                    configDoc.Load(reader);
                    if (!isValid)
                    {
                        throw new InvalidDataException("Configuration file format is invalid");
                    }
                    List<Member> members = new List<Member>();
                    foreach (XmlNode memberNode in configDoc.SelectNodes("//aufsconfig/members/member"))
                    {
                        string path = memberNode.Attributes["path"].Value;
                        bool readOnly = false;
                        if (memberNode.Attributes["readonly"] != null)
                        {
                            readOnly = Convert.ToBoolean(memberNode.Attributes["readonly"].Value);
                        }
                        members.Add(new Member(path, readOnly));
                    }
                    CreationPolicy policy = PolicyLoader.GetPolicy(configDoc.SelectSingleNode("//aufsconfig/volume").Attributes["policy"].Value, members);
                    string driveLetter = configDoc.SelectSingleNode("//aufsconfig/volume/letter").InnerText;
                    string driveLabel = configDoc.SelectSingleNode("//aufsconfig/volume/label").InnerText;
                    this.mergedVolume = new MergedVolumeInfo(members, driveLetter.ToCharArray()[0], driveLabel, policy);
                }
            }
        }

        public MergedVolumeInfo ReadConfiguration()
        {
            if (this.mergedVolume == null) // only read in the configuration if it hasn't been validated/read before
            {
                this.readConfiguration();
            }
            return this.mergedVolume;
        }

    }
}

