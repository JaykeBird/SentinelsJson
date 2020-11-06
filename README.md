# SentinelsJson

A sheet reader/writer for Sentinels RPG JSON character sheets

[Take a look at some screenshots!](https://github.com/JaykeBird/PathfinderJson/wiki/Screenshots)

## How to "install"
PathfinderJson requires Windows 7 with SP1, Windows 8.1, or Windows 10 with Anniversary Update or later. As long as you've installed all your updates, you should be good. You should be just able to double-click the EXE file in the zip folder you download and it'll open right up. There is no actual installation process, just download and go. (This also means this program is portable as well!)

For **64-bit** computers, use the x64 download. For **32-bit** computers, use the x86 download. If you're not sure what you have, you probably have a 64-bit computer, but you can go to [WhatsMyOS.com](http://whatsmyos.com/) to get a better idea.

### Issues running the program?

If it does not open or an error occurs (may take a few moments), you may need to install the Visual Studio C++ Redistributable package. I've included this for your convenience in the MSVC folder here, and in the release downloads. See [Microsoft's website](https://www.microsoft.com/en-us/download/details.aspx?id=52685) for more details. If there's still problems, make sure you have all updates installed; see the purple Note box on [this Microsoft webpage](https://docs.microsoft.com/en-us/dotnet/core/windows-prerequisites?tabs=netcore2x#net-core-dependencies) for more details.

If you're still having troubles - especially if it's a situation where it opens once or twice but then no longer opens - you may have to disable the Startup Optimization feature. I'll write a proper help page about this later, but if you have this issue, try downloading a release other than the one you're currently using, opening it and going to "Tools > Options". On the Advanced tab, turn off the "Use Startup Optimization" feature. Click OK and then close the program. You should now be able to go back to the latest release and use it just fine. Reach out to me if you have any issues or difficulties with this process!

You can reach out to me if you have any questions by [finding me on Twitter](https://twitter.com/JaykeBird) or [opening an issue report](https://github.com/JaykeBird/SentinelsJson/issues/new/choose) here on GitHub.

## How to use
To get started, you can either create a new character from the File menu, or you can open a file by selecting File > Open or dropping the JSON file into the program. You can create, view, and edit sheets.

You have three ways to view a file:
- a tabbed sheet view, where all the info is laid out via different tabs
- a continuous scroll view, where the info is still laid out but you can scroll through all of it
- a JSON text editor view, for basic raw text editing

## How to build
This section is for programmers who want to build this program, not regular users.

You must have Visual Studio 2019 version 16.3 or later to properly open the project and build it, as it is a .NET Core 3.0 WPF app. As a WPF app, that does mean this is Windows-only. I do wanna experiment with Avalonia later for a cross-platform UI.

Fun note: the main window seems to lag Visual Studio pretty bad, due to how many controls that's in it (especially after I added in the Spells tab and its 20+ text boxes). Although it is annoying, it hasn't bothered me to the point that I've done much about it though. This may be more of an issue for a computer that isn't the most powerful; my machine is about a mid-tier build.

## Roadmap
Roadmap:

1.0 release - match features of PathfinderJson 1.2 (late 2020)

## License
**SentinelsJson is released under the [MIT License](License.md).**

Do note that the UI library and icon set that SentinelsJson uses are not yet released as open source. The UI library will be released soon under the MIT License, and the full icon set will probably be released in 2020 under some form of Creative Commons license. The icons included in this repository are also released under the MIT license until the full icon set is released. I'll update this section as I go along.

For more info about the third-party libraries used in this project, open the Help > About > Third-Party Credits window.