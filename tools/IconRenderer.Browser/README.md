# IconRenderer Raylib browser

A small desktop test app using Raylib-cs with ImGui.NET through `rlImgui-cs`.

Run it from the repository root:

```sh
dotnet run --project tools/IconRenderer.Browser
```

The left panel provides a searchable, scrollable list of embedded icons. When a preset is selected, its icons sort to the top of the list and the list scrolls to the start; names remain alphabetical within each group. Checkboxes multi-select icons for a preset; clicking an icon name previews it. The top **Preset** dropdown selects one of the built-in categories or a saved custom preset. Enter a name and choose **Save selection** to persist the current multi-selection. Custom presets are stored at `LocalApplicationData/IconRenderer/icon-presets.json`.

**Render preset** renders every icon in the active preset using the current settings, one icon at a time while keeping the app responsive. PNGs are written to `icon-renders/presets/<prefix>-<preset-name>/`; the optional **Preset output folder prefix** setting adds the prefix, and leaving it empty preserves the plain preset-name folder. **Save PNG** still saves only the current preview to `icon-renders/<IconName>.png`.

The preview has a selected-icon tab and a scrollable **Preset preview** tab. The latter shows the active preset's icons in a thumbnail grid using the current appearance settings, creates thumbnails on demand, and never writes output files. Selecting a thumbnail switches the selected-icon preview to it. The settings panel controls icon and canvas colors, background shape and corner radius, independent foreground and background outlines, output size, and tiling. The preview updates as settings change.

## Built-in presets

The 50 thematic presets are generated from exact words in the embedded icon field names, so each category maps to concepts represented in this collection. Categories can overlap.

- Arcane & Magic
- Blades & Melee
- Bows & Projectiles
- Firearms & Explosives
- Armor & Shields
- Fantasy Creatures
- Monsters & Undead
- Anatomy & Body
- Healing & Medicine
- Mammals
- Birds
- Fish & Sea Life
- Reptiles & Insects
- Plants & Fungi
- Elements & Natural Forces
- Weather & Sky
- Sun, Moon & Stars
- Space & Astronomy
- Food & Ingredients
- Cooking & Kitchen
- Drinks & Vessels
- Tools & Workshop
- Crafting & Sewing
- Machines & Mechanisms
- Robots & Cyborgs
- Vehicles & Roads
- Ships & Sailing
- Buildings & Places
- Home & Furniture
- Royalty & Kingdoms
- Military & Warfare
- Crime & Law
- Money & Trade
- Gems & Treasure
- Cards & Dice
- Chess & Board Games
- Sports & Athletics
- Music & Instruments
- Books & Writing
- Science & Chemistry
- Computers & Electronics
- Communication & Media
- Time & Calendars
- Travel & Navigation
- Locks & Security
- Religion & Mythology
- Flags & Heraldry
- Light & Shadow
- Abstract & Geometry
- Clothing & Accessories

Raylib opens a desktop window and SVG rasterization uses SVG.NET/System.Drawing. A desktop graphics environment and compatible GDI+ support are required.
