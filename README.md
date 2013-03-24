# Aufs4Win  
  
## What is Aufs4Win?  
Aufs4Win is a filesystem implementation that provides the ability to merge multiple filesystems into one filesystem. The contents of all member filesystems appear as one large filesystem to all applications. Aufs4Win provides a much more flexible means to create volumes which span multiple physical disks than alternatives such as JBOD and dynamic disks.  
  
Aufs4Win is partially inspired by [Aufs for *nix-based systems](http://aufs.sourceforge.net/) and is built on top of [Dokan](http://dokan-dev.net/en/). The original motivation for developing Aufs4Win was to combine multiple drives containing video files and provide a merged view of all video files as well as store new video files on the hard disk with the most free space.  
  
## Installation  
Aufs4Win requires version 0.6 of Dokan to be installed. The installer can be found [here](http://dokan-dev.net/en/download/). The Aufs4Win executable is a console program written in .NET 4.0 (Client Profile) and can reside in any directory that you desire. However, DokanNet.dll must be located in the same directory as Aufs4Win.exe or installed to the system GAC.  
  
## Configuration  
  Aufs4Win is configured by writing an XML configuration file and supplying the configuration file path as the first command line argument to the Aufs4Win executable. See example_config.xml for an example file from which you can customize for your own machine. A brief summary of the configuration file:  
* All elements within the configuration file must be contained in an "aufsconfig" element (in other words, an "aufsconfig" XML element must be the root node of the XML document).
* The configuration file must contain a "volume" element which provides the drive letter, drive label, as well as the file/folder creation policy (see the "Creating custom policies" section for more details) for the Aufs drive.
* At least one member must be specified within the "members" element.
* Individual drive members can be specified as "read-only" to prevent modifications to them. The following operations are not permitted on a read-only member drive:
  * Writing/appending to files
  * Truncating files
  * Creating files/folders
  * Renaming files/folders
  * Deleting files/folders
  * Copying files/folders to the union drive  
  
## Running Aufs4Win  
After you have created the configuration file, you can start Aufs4Win by calling the executable on the command line and specifying the path to the configuration file as the first command line argument. If there is an error in your configuration file, the executable will print an error detailing the location of the error in the configuration file and then exit. If the executable started successfully, the Aufs drive should appear on your machine with the drive letter and label specified in the configuration file.  
  
## Creating custom policies  
When a file is created on a Aufs4Win volume, the creation policy specified in the configuration file is used to determine the location of the new file. The default creation policy determines the member filesystem with the most available free space and creates the file there. This policy may be better for some situations than others. In the case of large video files (the chief use scenario for which Aufs4Win was originally developed), this is helpful in the case of hard disk failure, as the amount of data potentially lost in a hard disk crash is lessened. This policy may be less than helpful in the case of storing many small files, such as source code.  
The current version of Aufs4Win provides only the "most free space" policy, but additional policies can be created and specified in the configuration file. Creation policies are implemented as .NET classes which inherit from the abstract class *Cbonnell.Aufs4Win.CreationPolicy*. See the source code of *Cbonnell.Aufs4Win.DefaultCreationPolicy* for an example on how to implement your own file/directory creation policies.  
You must create a new .NET project (DLL) and reference Aufs4Win.exe in the project. After you have finished implementing your creation policy, build the DLL and place the DLL in the same directory as Aufs4Win.exe. Then modify your configuration file to specify the name of your policy class name. Specifically, you will need to set the value of the "policy" attribute on the "volume" element to the name of your class that implements the creation policy. Note that if your class name ends in "Policy" or "CreationPolicy", you don't need to specify that part of the class name. For example, if your creation policy class name is "MyCreationPolicy", then it is sufficient to set the "policy" attribute to "My".

## Licensing  
Aufs4Win is licensed under GPL v3.
