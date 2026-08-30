namespace ChipCraft.Engine.Midi;

/// <summary>
/// Complete General MIDI Level 1 program catalog (128 instruments).
/// Provides lookup by number, name, and category.
/// </summary>
public static class GeneralMidi
{
    private static readonly MidiProgram[] Programs = BuildCatalog();

    public static MidiProgram GetProgram(byte number) => Programs[number];

    public static IReadOnlyList<MidiProgram> GetByCategory(string category) =>
        Programs.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();

    public static MidiProgram? FindByName(string name)
    {
        var lower = name.ToLowerInvariant();
        return Programs.FirstOrDefault(p => p.Name.Contains(lower, StringComparison.OrdinalIgnoreCase))
            ?? Programs.FirstOrDefault(p => lower.Split(' ').Any(w =>
                p.Name.Contains(w, StringComparison.OrdinalIgnoreCase)));
    }

    public static IReadOnlyList<string> Categories =>
        Programs.Select(p => p.Category).Distinct().ToList();

    public static IReadOnlyList<MidiProgram> All => Programs;

    private static MidiProgram[] BuildCatalog()
    {
        var p = new MidiProgram[128];

        // Piano (0-7)
        p[0] = new(0, "Acoustic Grand Piano", "Piano");
        p[1] = new(1, "Bright Acoustic Piano", "Piano");
        p[2] = new(2, "Electric Grand Piano", "Piano");
        p[3] = new(3, "Honky-tonk Piano", "Piano");
        p[4] = new(4, "Electric Piano 1", "Piano");
        p[5] = new(5, "Electric Piano 2", "Piano");
        p[6] = new(6, "Harpsichord", "Piano");
        p[7] = new(7, "Clavinet", "Piano");

        // Chromatic Percussion (8-15)
        p[8] = new(8, "Celesta", "Chromatic Percussion");
        p[9] = new(9, "Glockenspiel", "Chromatic Percussion");
        p[10] = new(10, "Music Box", "Chromatic Percussion");
        p[11] = new(11, "Vibraphone", "Chromatic Percussion");
        p[12] = new(12, "Marimba", "Chromatic Percussion");
        p[13] = new(13, "Xylophone", "Chromatic Percussion");
        p[14] = new(14, "Tubular Bells", "Chromatic Percussion");
        p[15] = new(15, "Dulcimer", "Chromatic Percussion");

        // Organ (16-23)
        p[16] = new(16, "Drawbar Organ", "Organ");
        p[17] = new(17, "Percussive Organ", "Organ");
        p[18] = new(18, "Rock Organ", "Organ");
        p[19] = new(19, "Church Organ", "Organ");
        p[20] = new(20, "Reed Organ", "Organ");
        p[21] = new(21, "Accordion", "Organ");
        p[22] = new(22, "Harmonica", "Organ");
        p[23] = new(23, "Tango Accordion", "Organ");

        // Guitar (24-31)
        p[24] = new(24, "Acoustic Guitar Nylon", "Guitar");
        p[25] = new(25, "Acoustic Guitar Steel", "Guitar");
        p[26] = new(26, "Electric Guitar Jazz", "Guitar");
        p[27] = new(27, "Electric Guitar Clean", "Guitar");
        p[28] = new(28, "Electric Guitar Muted", "Guitar");
        p[29] = new(29, "Overdriven Guitar", "Guitar");
        p[30] = new(30, "Distortion Guitar", "Guitar");
        p[31] = new(31, "Guitar Harmonics", "Guitar");

        // Bass (32-39)
        p[32] = new(32, "Acoustic Bass", "Bass");
        p[33] = new(33, "Electric Bass Finger", "Bass");
        p[34] = new(34, "Electric Bass Pick", "Bass");
        p[35] = new(35, "Fretless Bass", "Bass");
        p[36] = new(36, "Slap Bass 1", "Bass");
        p[37] = new(37, "Slap Bass 2", "Bass");
        p[38] = new(38, "Synth Bass 1", "Bass");
        p[39] = new(39, "Synth Bass 2", "Bass");

        // Strings (40-47)
        p[40] = new(40, "Violin", "Strings");
        p[41] = new(41, "Viola", "Strings");
        p[42] = new(42, "Cello", "Strings");
        p[43] = new(43, "Contrabass", "Strings");
        p[44] = new(44, "Tremolo Strings", "Strings");
        p[45] = new(45, "Pizzicato Strings", "Strings");
        p[46] = new(46, "Orchestral Harp", "Strings");
        p[47] = new(47, "Timpani", "Strings");

        // Ensemble (48-55)
        p[48] = new(48, "String Ensemble 1", "Ensemble");
        p[49] = new(49, "String Ensemble 2", "Ensemble");
        p[50] = new(50, "Synth Strings 1", "Ensemble");
        p[51] = new(51, "Synth Strings 2", "Ensemble");
        p[52] = new(52, "Choir Aahs", "Ensemble");
        p[53] = new(53, "Voice Oohs", "Ensemble");
        p[54] = new(54, "Synth Voice", "Ensemble");
        p[55] = new(55, "Orchestra Hit", "Ensemble");

        // Brass (56-63)
        p[56] = new(56, "Trumpet", "Brass");
        p[57] = new(57, "Trombone", "Brass");
        p[58] = new(58, "Tuba", "Brass");
        p[59] = new(59, "Muted Trumpet", "Brass");
        p[60] = new(60, "French Horn", "Brass");
        p[61] = new(61, "Brass Section", "Brass");
        p[62] = new(62, "Synth Brass 1", "Brass");
        p[63] = new(63, "Synth Brass 2", "Brass");

        // Reed (64-71)
        p[64] = new(64, "Soprano Sax", "Reed");
        p[65] = new(65, "Alto Sax", "Reed");
        p[66] = new(66, "Tenor Sax", "Reed");
        p[67] = new(67, "Baritone Sax", "Reed");
        p[68] = new(68, "Oboe", "Reed");
        p[69] = new(69, "English Horn", "Reed");
        p[70] = new(70, "Bassoon", "Reed");
        p[71] = new(71, "Clarinet", "Reed");

        // Pipe (72-79)
        p[72] = new(72, "Piccolo", "Pipe");
        p[73] = new(73, "Flute", "Pipe");
        p[74] = new(74, "Recorder", "Pipe");
        p[75] = new(75, "Pan Flute", "Pipe");
        p[76] = new(76, "Blown Bottle", "Pipe");
        p[77] = new(77, "Shakuhachi", "Pipe");
        p[78] = new(78, "Whistle", "Pipe");
        p[79] = new(79, "Ocarina", "Pipe");

        // Synth Lead (80-87)
        p[80] = new(80, "Lead 1 Square", "Synth Lead");
        p[81] = new(81, "Lead 2 Sawtooth", "Synth Lead");
        p[82] = new(82, "Lead 3 Calliope", "Synth Lead");
        p[83] = new(83, "Lead 4 Chiff", "Synth Lead");
        p[84] = new(84, "Lead 5 Charang", "Synth Lead");
        p[85] = new(85, "Lead 6 Voice", "Synth Lead");
        p[86] = new(86, "Lead 7 Fifths", "Synth Lead");
        p[87] = new(87, "Lead 8 Bass+Lead", "Synth Lead");

        // Synth Pad (88-95)
        p[88] = new(88, "Pad 1 New Age", "Synth Pad");
        p[89] = new(89, "Pad 2 Warm", "Synth Pad");
        p[90] = new(90, "Pad 3 Polysynth", "Synth Pad");
        p[91] = new(91, "Pad 4 Choir", "Synth Pad");
        p[92] = new(92, "Pad 5 Bowed", "Synth Pad");
        p[93] = new(93, "Pad 6 Metallic", "Synth Pad");
        p[94] = new(94, "Pad 7 Halo", "Synth Pad");
        p[95] = new(95, "Pad 8 Sweep", "Synth Pad");

        // Synth Effects (96-103)
        p[96] = new(96, "FX 1 Rain", "Synth Effects");
        p[97] = new(97, "FX 2 Soundtrack", "Synth Effects");
        p[98] = new(98, "FX 3 Crystal", "Synth Effects");
        p[99] = new(99, "FX 4 Atmosphere", "Synth Effects");
        p[100] = new(100, "FX 5 Brightness", "Synth Effects");
        p[101] = new(101, "FX 6 Goblins", "Synth Effects");
        p[102] = new(102, "FX 7 Echoes", "Synth Effects");
        p[103] = new(103, "FX 8 Sci-fi", "Synth Effects");

        // Ethnic (104-111)
        p[104] = new(104, "Sitar", "Ethnic");
        p[105] = new(105, "Banjo", "Ethnic");
        p[106] = new(106, "Shamisen", "Ethnic");
        p[107] = new(107, "Koto", "Ethnic");
        p[108] = new(108, "Kalimba", "Ethnic");
        p[109] = new(109, "Bag Pipe", "Ethnic");
        p[110] = new(110, "Fiddle", "Ethnic");
        p[111] = new(111, "Shanai", "Ethnic");

        // Percussive (112-119)
        p[112] = new(112, "Tinkle Bell", "Percussive");
        p[113] = new(113, "Agogo", "Percussive");
        p[114] = new(114, "Steel Drums", "Percussive");
        p[115] = new(115, "Woodblock", "Percussive");
        p[116] = new(116, "Taiko Drum", "Percussive");
        p[117] = new(117, "Melodic Tom", "Percussive");
        p[118] = new(118, "Synth Drum", "Percussive");
        p[119] = new(119, "Reverse Cymbal", "Percussive");

        // Sound Effects (120-127)
        p[120] = new(120, "Guitar Fret Noise", "Sound Effects");
        p[121] = new(121, "Breath Noise", "Sound Effects");
        p[122] = new(122, "Seashore", "Sound Effects");
        p[123] = new(123, "Bird Tweet", "Sound Effects");
        p[124] = new(124, "Telephone Ring", "Sound Effects");
        p[125] = new(125, "Helicopter", "Sound Effects");
        p[126] = new(126, "Applause", "Sound Effects");
        p[127] = new(127, "Gunshot", "Sound Effects");

        return p;
    }
}
