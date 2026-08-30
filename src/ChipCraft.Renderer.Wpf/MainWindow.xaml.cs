using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;

namespace ChipCraft.Renderer.Wpf;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly AudioExportService _exportService = new();
    private readonly ObservableCollection<AssetOption> _midiFiles = [];
    private readonly ObservableCollection<AssetOption> _soundFonts = [];
    private string _midiPath = "";
    private string _soundFontPath = "";
    private string _outputPath = "";
    private string _midiSearchText = "";
    private string _soundFontSearchText = "";
    private AudioExportFormat _selectedFormat = AudioExportFormat.Wav;
    private int _selectedSampleRate = 44100;
    private bool _isRendering;
    private bool _isRefreshingLibraries;
    private AssetOption? _selectedMidiOption;
    private AssetOption? _selectedSoundFontOption;
    private string _statusText = "Select a MIDI file and a SoundFont to begin.";
    private Brush _statusBrush = Brushes.LightSteelBlue;
    private string _projectRoot = "";
    private string _sampleOutputsRoot = "";
    private string _soundFontsRoot = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();
        Formats = Enum.GetValues<AudioExportFormat>().ToArray();
        SampleRates = [22050, 32000, 44100, 48000];
        FilteredMidiFiles = CollectionViewSource.GetDefaultView(_midiFiles);
        FilteredMidiFiles.Filter = item => item is AssetOption option && option.Matches(MidiSearchText);
        FilteredSoundFonts = CollectionViewSource.GetDefaultView(_soundFonts);
        FilteredSoundFonts.Filter = item => item is AssetOption option && option.Matches(SoundFontSearchText);
        DataContext = this;
    }

    public AudioExportFormat[] Formats { get; }

    public int[] SampleRates { get; }

    public ICollectionView FilteredMidiFiles { get; }

    public ICollectionView FilteredSoundFonts { get; }

    public string MidiLibraryHint =>
        _midiFiles.Count == 0
            ? "Song MIDIs From sample-outputs (none found yet)"
            : $"Song MIDIs From sample-outputs ({_midiFiles.Count} found)";

    public string SoundFontLibraryHint =>
        _soundFonts.Count == 0
            ? "SoundFonts From soundfonts (none found yet)"
            : $"SoundFonts From soundfonts ({_soundFonts.Count} found)";

    public string LibrarySummary =>
        string.IsNullOrWhiteSpace(_projectRoot)
            ? "Library scan has not completed yet."
            : $"Auto-populated from {_sampleOutputsRoot} and {_soundFontsRoot}. Stem MIDIs are excluded from the song dropdown by default. Use the search boxes to filter the dropdowns, or browse to an external file manually.";

    public string MidiPath
    {
        get => _midiPath;
        set
        {
            if (SetField(ref _midiPath, value))
            {
                SyncOutputPathToInputs();
                NotifyRenderStateChanged();
            }
        }
    }

    public string SoundFontPath
    {
        get => _soundFontPath;
        set
        {
            if (SetField(ref _soundFontPath, value))
                NotifyRenderStateChanged();
        }
    }

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            if (SetField(ref _outputPath, value))
                NotifyRenderStateChanged();
        }
    }

    public string MidiSearchText
    {
        get => _midiSearchText;
        set
        {
            if (SetField(ref _midiSearchText, value))
                FilteredMidiFiles.Refresh();
        }
    }

    public string SoundFontSearchText
    {
        get => _soundFontSearchText;
        set
        {
            if (SetField(ref _soundFontSearchText, value))
                FilteredSoundFonts.Refresh();
        }
    }

    public AssetOption? SelectedMidiOption
    {
        get => _selectedMidiOption;
        set
        {
            if (SetField(ref _selectedMidiOption, value) && value != null)
                MidiPath = value.FullPath;
        }
    }

    public AssetOption? SelectedSoundFontOption
    {
        get => _selectedSoundFontOption;
        set
        {
            if (SetField(ref _selectedSoundFontOption, value) && value != null)
                SoundFontPath = value.FullPath;
        }
    }

    public AudioExportFormat SelectedFormat
    {
        get => _selectedFormat;
        set
        {
            if (SetField(ref _selectedFormat, value))
            {
                SyncOutputPathToInputs();
                NotifyRenderStateChanged();
            }
        }
    }

    public int SelectedSampleRate
    {
        get => _selectedSampleRate;
        set => SetField(ref _selectedSampleRate, value);
    }

    public bool CanRender =>
        !_isRendering &&
        File.Exists(MidiPath) &&
        File.Exists(SoundFontPath) &&
        !string.IsNullOrWhiteSpace(OutputPath);

    public bool CanRefreshLibraries => !_isRefreshingLibraries && !_isRendering;

    public string RenderButtonText => _isRendering ? "Rendering..." : "Render Audio";

    public string RefreshButtonText => _isRefreshingLibraries ? "Scanning..." : "Rescan Libraries";

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public Brush StatusBrush
    {
        get => _statusBrush;
        private set => SetField(ref _statusBrush, value);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshLibrariesAsync();
    }

    private void BrowseMidi_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "MIDI files (*.mid;*.midi)|*.mid;*.midi|All files (*.*)|*.*",
            Title = "Select MIDI File"
        };

        if (dialog.ShowDialog(this) == true)
            SelectOrAddMidi(dialog.FileName);
    }

    private void BrowseSoundFont_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "SoundFont files (*.sf2)|*.sf2|All files (*.*)|*.*",
            Title = "Select SoundFont"
        };

        if (dialog.ShowDialog(this) == true)
            SelectOrAddSoundFont(dialog.FileName);
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = SelectedFormat == AudioExportFormat.Wav
                ? "WAV audio (*.wav)|*.wav"
                : "MP3 audio (*.mp3)|*.mp3",
            DefaultExt = GetExtensionForFormat(SelectedFormat),
            AddExtension = true,
            FileName = BuildSuggestedOutputFileName()
        };

        if (dialog.ShowDialog(this) == true)
            OutputPath = dialog.FileName;
    }

    private async void RefreshLibraries_Click(object sender, RoutedEventArgs e)
    {
        await RefreshLibrariesAsync();
    }

    private async void Render_Click(object sender, RoutedEventArgs e)
    {
        if (!CanRender)
            return;

        try
        {
            _isRendering = true;
            NotifyRenderStateChanged();
            StatusBrush = Brushes.Goldenrod;
            StatusText = "Rendering audio file...";

            string midiPath = MidiPath;
            string soundFontPath = SoundFontPath;
            string outputPath = OutputPath;
            AudioExportFormat format = SelectedFormat;
            int sampleRate = SelectedSampleRate;

            await Task.Run(() => _exportService.Render(midiPath, soundFontPath, outputPath, format, sampleRate));

            StatusBrush = Brushes.MediumSeaGreen;
            StatusText = $"Rendered {Path.GetFileName(outputPath)} successfully.";
        }
        catch (Exception ex)
        {
            StatusBrush = Brushes.IndianRed;
            StatusText = ex.Message;
            MessageBox.Show(this, ex.Message, "Render Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isRendering = false;
            NotifyRenderStateChanged();
        }
    }

    private async Task RefreshLibrariesAsync()
    {
        if (_isRefreshingLibraries)
            return;

        try
        {
            _isRefreshingLibraries = true;
            NotifyRenderStateChanged();
            StatusBrush = Brushes.Goldenrod;
            StatusText = "Scanning sample-outputs and soundfonts...";

            var scan = await Task.Run(BuildLibraryScan);
            ApplyLibraryScan(scan);

            StatusBrush = Brushes.LightSteelBlue;
            StatusText = $"Found {_midiFiles.Count} MIDI files and {_soundFonts.Count} SoundFonts.";
        }
        catch (Exception ex)
        {
            StatusBrush = Brushes.IndianRed;
            StatusText = ex.Message;
        }
        finally
        {
            _isRefreshingLibraries = false;
            NotifyRenderStateChanged();
        }
    }

    private void SyncOutputPathToInputs()
    {
        if (string.IsNullOrWhiteSpace(MidiPath))
            return;

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            OutputPath = BuildSuggestedOutputPath();
            return;
        }

        string expectedExtension = GetExtensionForFormat(SelectedFormat);
        if (!OutputPath.EndsWith(expectedExtension, StringComparison.OrdinalIgnoreCase))
            OutputPath = Path.ChangeExtension(OutputPath, expectedExtension);
    }

    private string BuildSuggestedOutputPath()
    {
        string directory = Path.GetDirectoryName(MidiPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(directory, BuildSuggestedOutputFileName());
    }

    private string BuildSuggestedOutputFileName()
    {
        if (string.IsNullOrWhiteSpace(MidiPath))
            return $"rendered-audio{GetExtensionForFormat(SelectedFormat)}";

        string fileName = Path.GetFileNameWithoutExtension(MidiPath);
        return $"{fileName}{GetExtensionForFormat(SelectedFormat)}";
    }

    private static string GetExtensionForFormat(AudioExportFormat format) => format switch
    {
        AudioExportFormat.Mp3 => ".mp3",
        _ => ".wav"
    };

    private void ApplyLibraryScan(LibraryScanResult scan)
    {
        _projectRoot = scan.ProjectRoot;
        _sampleOutputsRoot = scan.SampleOutputsRoot;
        _soundFontsRoot = scan.SoundFontsRoot;

        ReplaceItems(_midiFiles, scan.MidiFiles);
        ReplaceItems(_soundFonts, scan.SoundFonts);

        RestoreMidiSelection();
        RestoreSoundFontSelection();

        FilteredMidiFiles.Refresh();
        FilteredSoundFonts.Refresh();

        OnPropertyChanged(nameof(MidiLibraryHint));
        OnPropertyChanged(nameof(SoundFontLibraryHint));
        OnPropertyChanged(nameof(LibrarySummary));
    }

    private void RestoreMidiSelection()
    {
        if (TrySelectExistingOption(_midiFiles, MidiPath, option => SelectedMidiOption = option))
            return;

        var preferred = _midiFiles.OrderByDescending(option => option.LastWriteTimeUtc).FirstOrDefault();
        if (preferred != null && string.IsNullOrWhiteSpace(MidiPath))
            SelectedMidiOption = preferred;
    }

    private void RestoreSoundFontSelection()
    {
        if (TrySelectExistingOption(_soundFonts, SoundFontPath, option => SelectedSoundFontOption = option))
            return;

        if (!string.IsNullOrWhiteSpace(SoundFontPath))
            return;

        var preferred = _soundFonts.FirstOrDefault(option => option.DisplayName.Equals("GeneralUser GS v1.471.sf2", StringComparison.OrdinalIgnoreCase))
            ?? _soundFonts.FirstOrDefault(option => option.DisplayName.Equals("FluidR3_GM2-2.SF2", StringComparison.OrdinalIgnoreCase))
            ?? _soundFonts.FirstOrDefault();

        if (preferred != null)
            SelectedSoundFontOption = preferred;
    }

    private void SelectOrAddMidi(string path)
    {
        MidiPath = Path.GetFullPath(path);
        SelectedMidiOption = SelectOrAddOption(_midiFiles, MidiPath, _sampleOutputsRoot, "sample-outputs");
        MidiSearchText = "";
    }

    private void SelectOrAddSoundFont(string path)
    {
        SoundFontPath = Path.GetFullPath(path);
        SelectedSoundFontOption = SelectOrAddOption(_soundFonts, SoundFontPath, _soundFontsRoot, "soundfonts");
        SoundFontSearchText = "";
    }

    private static AssetOption SelectOrAddOption(
        ObservableCollection<AssetOption> collection,
        string fullPath,
        string libraryRoot,
        string libraryName)
    {
        var existing = collection.FirstOrDefault(option => option.FullPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            return existing;

        AssetOption option = File.Exists(fullPath) && !string.IsNullOrWhiteSpace(libraryRoot) && fullPath.StartsWith(libraryRoot, StringComparison.OrdinalIgnoreCase)
            ? AssetOption.Create(fullPath, libraryRoot, libraryName)
            : new AssetOption(fullPath, Path.GetFileName(fullPath), string.Empty, "External", File.Exists(fullPath) ? File.GetLastWriteTimeUtc(fullPath) : DateTime.UtcNow);

        collection.Add(option);
        SortCollection(collection);
        return collection.First(item => item.FullPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TrySelectExistingOption(
        IEnumerable<AssetOption> collection,
        string path,
        Action<AssetOption> applySelection)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var option = collection.FirstOrDefault(item => item.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (option == null)
            return false;

        applySelection(option);
        return true;
    }

    private static void ReplaceItems(ObservableCollection<AssetOption> target, IReadOnlyList<AssetOption> source)
    {
        target.Clear();
        foreach (var item in source)
            target.Add(item);
    }

    private static void SortCollection(ObservableCollection<AssetOption> collection)
    {
        var ordered = collection
            .OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.LocationHint, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        collection.Clear();
        foreach (var item in ordered)
            collection.Add(item);
    }

    private static LibraryScanResult BuildLibraryScan()
    {
        string projectRoot = ResolveProjectRoot();
        string sampleOutputsRoot = Path.Combine(projectRoot, "sample-outputs");
        string soundFontsRoot = Path.Combine(projectRoot, "soundfonts");

        var midiFiles = EnumerateFiles(sampleOutputsRoot, ["*.mid", "*.midi"])
            .Where(IsPrimarySongMidi)
            .Select(path => AssetOption.Create(path, sampleOutputsRoot, "sample-outputs"))
            .OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.LocationHint, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var soundFonts = EnumerateFiles(soundFontsRoot, ["*.sf2"])
            .Select(path => AssetOption.Create(path, soundFontsRoot, "soundfonts"))
            .OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.LocationHint, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new LibraryScanResult(projectRoot, sampleOutputsRoot, soundFontsRoot, midiFiles, soundFonts);
    }

    private static IReadOnlyList<string> EnumerateFiles(string root, IReadOnlyList<string> patterns)
    {
        if (!Directory.Exists(root))
            return [];

        return patterns
            .SelectMany(pattern => Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsPrimarySongMidi(string path)
    {
        var fileInfo = new FileInfo(path);
        if (fileInfo.Directory != null &&
            fileInfo.Directory.Name.Equals("stems", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        string[] stemSuffixes =
        [
            ".lead",
            ".rhythm",
            ".harmony",
            ".drums",
            ".bass",
            ".pad",
            ".fx"
        ];

        return !stemSuffixes.Any(suffix => fileNameWithoutExtension.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ChipCraft.slnx")))
                return current.FullName;
            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private void NotifyRenderStateChanged()
    {
        OnPropertyChanged(nameof(CanRender));
        OnPropertyChanged(nameof(CanRefreshLibraries));
        OnPropertyChanged(nameof(RenderButtonText));
        OnPropertyChanged(nameof(RefreshButtonText));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed record LibraryScanResult(
        string ProjectRoot,
        string SampleOutputsRoot,
        string SoundFontsRoot,
        IReadOnlyList<AssetOption> MidiFiles,
        IReadOnlyList<AssetOption> SoundFonts);
}
