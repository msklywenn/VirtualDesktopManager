# Virtual Desktop Manager

This is a deskband to show and move windows across virtual desktops. It is forked from [VirtualDesktopNameDeskband](https://github.com/lutz/VirtualDesktopNameDeskband) by Daniel Lutz and based on the great projects [SharpShell](https://github.com/dwmkerr/sharpshell/) by dwmkerr and [VirtualDesktop](https://github.com/MScholtes/VirtualDesktop) by MScholtes.

## Functions

This deskband shows miniatures of windows of all the virtual desktops. A window can be moved from one virtual desktop by drag & dropping it inside the deskband view.

<!-- ![Te](assets/taskbar.png) -->

## Installation

Clone the repository. After building you can use the small scripts (you can edit the `*.bat` files for fix the correct path to the _regasm_ tool) to register the assembly. Restart the **Explorer.exe** and add the deskband via contextmenu.

Generate an `*.pfx` file for a strong name signed assembly and enable signing in project options to prevent Register.bat from complaining.

## History

This is work in progress and buggy. There's no release yet.

## To do

* Properly size the picture box (depending on number of desktops and deskband height)
* Window name on hover (with a tooltip)
* Debugging (eg. seems to crash the explorer when removed)

## License

This project is licensed under the [MIT](LICENSE) License
