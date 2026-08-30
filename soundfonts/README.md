# SoundFonts

Drop `.sf2` SoundFont files in this directory. ChipCraft uses them to render MIDI to audio.

**No SoundFonts are committed to this repository.** They are large (a good general-MIDI bank
runs to hundreds of megabytes, and a full pack into the gigabytes) and each one carries its own
redistribution terms. Everything in this folder except this file is gitignored.

## Getting one

Any general-MIDI `.sf2` will work. Two that are widely used and freely available are
**GeneralUser GS** and **FluidR3 GM**; both are easy to find and each ships its own license
file. Read the terms of whichever you pick before you redistribute anything rendered with it,
especially if you are shipping audio commercially. ChipCraft neither bundles nor endorses a
particular bank.

Once you have one, the path is passed in explicitly, so the file can live anywhere; this folder
is just a convenient default.

## Usage

```bash
# CLI: render an existing MIDI file to WAV
dotnet run --project src/ChipCraft.Cli -- render \
  --input song.mid \
  --output song.wav \
  --soundfont soundfonts/GeneralUser-GS.sf2

# MCP: the render_audio and render_pattern_audio tools take a soundfont path
```

The CLI exposes `render` and `theory`. There is no `compose` command; composition happens
through the MCP tools.
