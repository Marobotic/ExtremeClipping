# Extreme Clipping

A fork of [NoClippy](https://github.com/UnknownX7/NoClippy) by UnknownX.

Reduces the effects of lag on certain aspects of the game, such as weaving and statuses, and can show GCD clips.

What's different from NoClippy:

- You pick the ping the animation lock simulates: anywhere from 0 to 100 ms, 8 ms by default, with a Reset button.
- The window has two tabs. Overview holds the animation lock switch, the ping slider and the time saved; Settings holds logging, dry run, encounter stats and the rest.
- The window uses the Marochy theme.
- The command is `/xclip` (or `/extremeclipping`).

Turn off NoClippy while you use Extreme Clipping, since both change the same animation lock. As with NoClippy, if XivAlexander is running, the animation lock reduction switches itself off.

## Install

In game, open `/xlsettings`, go to Experimental, and add this custom plugin repository:

```
https://pfa.marobotic.dev/repo
```

Save, then install Extreme Clipping from the plugin installer.

## Build

```
dotnet build NoClippy.csproj -c Release
```

The plugin zip is written to `bin/Release/ExtremeClipping/latest.zip`.
