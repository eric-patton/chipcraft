# ChipCraft

AI-assisted music composition engine exposed via MCP server. Outputs standard MIDI files and renders to WAV using SoundFont (.sf2) files.

## Build

```bash
dotnet build
dotnet test
```

## Project Structure

- `src/ChipCraft.Engine/` - Core composition library (theory, generators, MIDI export, audio rendering)
  - `Theory/` - Scales, keys, chords, chord progression database
  - `Generation/` - Melody, bass line, harmony, arpeggio, pad, drum, accent generators
  - `Midi/` - MIDI exporter, GM program catalog, audio renderer (MeltySynth + soundfont)
  - `Sequencer/` - Song, Pattern, tracker grid cells, and expressive Part/automation data model
  - `Models/` - Note (MIDI-native), enums
- `src/ChipCraft.Mcp/` - MCP server with composition, theory, song/part/program, export, and review tools
- `src/ChipCraft.Cli/` - CLI tool (render MIDI to WAV with soundfont)
- `tests/ChipCraft.Engine.Tests/` - Unit tests
- `soundfonts/` - Place .sf2 soundfont files here for audio rendering

## Architecture

The AI composes manually through the toolbox. ChipCraft provides:
1. **Theory tools** - scale/chord/key reference for the AI to query
2. **Generator tools** - optional building blocks (melody, bass, drums, harmony, arpeggio, pad) the AI can use as starting points
3. **Pattern tools** - the tracker-grid canvas where the AI places step notes, reads them back, copies/transposes them, and builds variations
4. **Part tools** - the expressive event layer for note durations, automation, patch overrides, and targeted refinements
5. **Song tools** - assemble patterns into playback order, edit arrangement order, and control real transport metadata such as tempo, meter, grid resolution, and loop points
6. **Program tools** - assign soundfont-aware channel patches and channel mix defaults
7. **Export/review tools** - output MIDI/WAV artifacts, stems, loop previews, delivery bundles, and musical/render analysis

There is no one-shot MCP composition endpoint. The intended workflow is to create or generate material, inspect it, arrange it, preview it, and export it.

## Conventions

- Use raw string literals for multi-line strings
- .NET 10 (net10.0) with nullable reference types enabled
- Engine library must have zero MCP/CLI dependencies
- Notes are MIDI-native (int MidiNumber is the canonical representation)
- Patterns use a tracker-style row/channel grid (16 rows = 1 bar at 4 rows/beat)
