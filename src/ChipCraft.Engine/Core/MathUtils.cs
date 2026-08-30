namespace ChipCraft.Engine.Core;

public static class MathUtils
{
    public static double MidiToFrequency(int midiNumber)
    {
        return Constants.A4Frequency * Math.Pow(2.0, (midiNumber - Constants.A4MidiNumber) / 12.0);
    }

    public static int FrequencyToMidi(double frequency)
    {
        return (int)Math.Round(Constants.A4MidiNumber + 12.0 * Math.Log2(frequency / Constants.A4Frequency));
    }

    public static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }
}
