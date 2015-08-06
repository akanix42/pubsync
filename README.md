# pubsync
**Project Description**

PubSync (short for Publish/Sync) is a command-line application I developed to ease the pain of publishing my Visual Studio projects. Although I developed it for use with Visual Studio, it can easily be used for syncing any directory structure.  
I created PubSync to solve 3 issues:

1. Visual Studio's Publish feature would often fail to overwrite changed files, forcing me to manually delete the destination files and re-publish. I hate manual steps.
2. Visual Studio's option to remove the files on the destination that don't exist on the source is not configurable. For example, I couldn't choose to have it not wipe out my uploads folder containing user-created content.
3. I sometimes work from home, and publishing through a VPN across the Internet was extremely slow, despite the file sizes being low. In my experience, PubSync is much faster.

**PubSync's Main Features:** 

- Simple XML schema for configuring file sync
- Support for profiles to publish to different servers
- High-speed syncing
    - Easily exclude files/folders from syncing using Regular Expressions
    - Delete files on the destination that do not exist on the source
    - Easily sync a single file

**Simple XML schema for configuring file sync**

I wanted to make configuring PubSync simple but flexible, so I made up a simple XML schema. The schema and a sample config file are included with PubSync.

**Support for profiles to publish to different servers**

At work, we use at least separate servers for development: development, staging, and live. I'm all about automating repetitive tasks, so Pubsync needed to support easily publishing to all 3.

**High-speed syncing**

At the first release, PubSync simply called Robocopy with the proper arguments to perform the sync. This turned out to be quite a bit faster than Visual Studio, but it was still pretty slow when copying across the VPN from home, so I added a feature in PubSync to perform the sync internally. The Robocopy option is now deprecated and will likely be removed in the near future.

To determine if a file should be copied, PubSync uses a few simple checks:

1. Is the file in the excluded list?
2. Does the file exist on the destination?
3. Do the source and destination files have the same size and date modified?

I'm unsure of all the criteria Robocopy uses to decide what to copy, but my method turned out to be much faster, especially when dealing with many small files.

**Delete files on the destination that do not exist on the source**

If a file exists on the destination but not on the source, PubSync will remove it. Simple and sweet.

**Easily exclude files/folders from syncing using Regular Expressions**

I needed PubSync to ignore the user uploads folder, as it only exists on the destination and the files should never be erased. I also required it to skip certain files (all the CSharp files, for example).

PubSync can be configured with regular expressions to exclude certain files and folders.

**Easily sync a single file**

I was working on a web app across a VPN and got sick of waiting for the entire directory structure to sync (it took about 20 seconds) when I had only changed 1 file, so I added an option to specify a single file to be synced.

**How To Use**

1. Copy the pubsync.xml file from the folder where you installed PubSync to the root of your Project. Open the XML file and configure it your heart's content.
2. Add an External Tool to Visual Studio as shown in the following screenshot. The argument is the name of the profile you wish this tool to use. To use a different profile, add another external tool. I use 3 profiles: Dev, Staging, and Live.
3. To make your life simpler, configure a keyboard shortcut for the command (see second screenshot).

![External Tools Screenshot](https://github.com/nathantreid/pubsync/blob/images/external-tools.png)

![Keyboard Shortcut Screenshot](https://github.com/nathantreid/pubsync/blob/images/shortcut.png)
