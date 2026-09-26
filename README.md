# TF2 Decal Vault

### A user interface to upload and download UGC images for TF2's decal system

## Download:
**[View Releases](https://github.com/Batfoxkid/Freak-Fortress-2-Rewrite/releases)**

## Building:
- [Godot Engine v4.6.2.stable.mono.official](https://godotengine.org/download/archive/4.6.2-stable) (newer versions may work too)
- [GodotSteam](https://godotsteam.com/getting_started/what_are_you_making/#singleplayer-or-multiplayer-games) (GDScript) or [Facepunch.Steamworks](https://github.com/Facepunch/Facepunch.Steamworks) (C#)

## Notes:
This is meant to only handle decals for custom server content, you can not use this to apply decals on personal TF2 items.
```squirrel
item.AddAttribute("custom texture lo", casti2f(lowint32))
item.AddAttribute("custom texture hi", casti2f(highint32))
```
