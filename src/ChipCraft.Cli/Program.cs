using System.CommandLine;
using ChipCraft.Cli.Commands;

var root = new RootCommand("ChipCraft - AI-powered music composition engine with MIDI output");

root.AddCommand(RenderCommand.Create());
root.AddCommand(TheoryCommand.Create());

return await root.InvokeAsync(args);
