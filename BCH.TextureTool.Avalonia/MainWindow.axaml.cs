using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using BCH.TextureCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace BCH.TextureTool.Avalonia;

public partial class MainWindow : Window
{
    private TextureSession? _session;
    private string _saveExt = ".bch";

    public MainWindow()
    {
        InitializeComponent();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void EnableFileControls()
    {
        SaveMenu.IsEnabled = true;
        ExportAllMenu.IsEnabled = true;
        BtnImport.IsEnabled = true;
        BtnImportSplit.IsEnabled = true;
    }

    private void EnableSelectionControls(bool enabled)
    {
        BtnReplace.IsEnabled = enabled;
        BtnRemove.IsEnabled = enabled;
        BtnRename.IsEnabled = enabled;
        BtnExport.IsEnabled = enabled;
        BtnExportSplit.IsEnabled = enabled;
    }

    private void Reset()
    {
        _session = null;
        TextureList.Items.Clear();
        RgbPreview.Source = null;
        AlphaPreview.Source = null;
        FileNameLabel.Text = "";
        TextureNameLabel.Text = "";
        SaveMenu.IsEnabled = false;
        ExportAllMenu.IsEnabled = false;
        BtnImport.IsEnabled = false;
        BtnImportSplit.IsEnabled = false;
        EnableSelectionControls(false);
    }

    private void PopulateList()
    {
        TextureList.Items.Clear();
        if (_session == null) return;
        foreach (var name in _session.TextureNames)
            TextureList.Items.Add(name);
        FileNameLabel.Text = _session.FileName;
    }

    private void ShowPreview(int index)
    {
        if (_session == null) return;
        var (rgb, alpha, _, _) = _session.GetPreview(index);
        RgbPreview.Source = PngToBitmap(rgb);
        AlphaPreview.Source = alpha.Length > 0 ? PngToBitmap(alpha) : null;
        TextureNameLabel.Text = _session.TextureNames[index];
    }

    private static Bitmap PngToBitmap(byte[] png)
    {
        using var ms = new MemoryStream(png);
        return new Bitmap(ms);
    }

    private async Task ShowError(string message)
    {
        var dlg = new Window
        {
            Title = "Error",
            Width = 360,
            Height = 130,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false
        };
        var ok = new Button { Content = "OK", Width = 80, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(8) };
        ok.Click += (_, _) => dlg.Close();
        dlg.Content = new StackPanel
        {
            Margin = new Thickness(12),
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) },
                ok
            }
        };
        await dlg.ShowDialog(this);
    }

    private static FilePickerFileType[] ArcFilter() =>
    [
        new FilePickerFileType("Compatible Files") { Patterns = ["*.bch", "*.lz", "*.arc"] },
        new FilePickerFileType("BCH File")         { Patterns = ["*.bch"] },
        new FilePickerFileType("LZ File")          { Patterns = ["*.lz"] },
        new FilePickerFileType("ARC File")         { Patterns = ["*.arc"] },
        new FilePickerFileType("All Files")        { Patterns = ["*"] }
    ];

    // ── Menu ───────────────────────────────────────────────────────────────────

    private async void Open_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open File",
            FileTypeFilter = ArcFilter()
        });
        if (files.Count == 0) return;

        var file = files[0];
        var path = file.Path.LocalPath;
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var bytes = await File.ReadAllBytesAsync(path);

        Reset();
        _saveExt = ext;

        try
        {
            if (ext == ".arc")
                _session = FileOperations.OpenArc(bytes, file.Name);
            else
                _session = FileOperations.OpenFile(bytes, file.Name);
        }
        catch (Exception ex)
        {
            await ShowError($"Could not open '{file.Name}'. The file may be corrupt or in an unsupported format.\n\n{ex.Message}");
            return;
        }

        if (_session == null)
        {
            FileNameLabel.Text = "Error: could not open file (may contain models or use unsupported format)";
            return;
        }

        PopulateList();
        EnableFileControls();
    }

    private void New_Click(object? sender, RoutedEventArgs e)
    {
        Reset();
        _session = new TextureSession(new SPICA.Formats.CtrH3D.H3D(), "New BCH File");
        _saveExt = ".bch";
        FileNameLabel.Text = "New BCH File";
        EnableFileControls();
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (_session == null) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save File",
            SuggestedFileName = Path.GetFileNameWithoutExtension(_session.FileName),
            FileTypeChoices = ArcFilter()
        });
        if (file == null) return;

        var path = file.Path.LocalPath;
        var ext = Path.GetExtension(path).ToLowerInvariant();

        byte[] output = ext == ".arc"
            ? FileOperations.SaveArc(_session)
            : FileOperations.Save(_session, ext == ".lz" ? SaveFormat.Lz : SaveFormat.Bch);

        await File.WriteAllBytesAsync(path, output);
        _session.FileName = file.Name;
        FileNameLabel.Text = file.Name;
        _saveExt = ext;
    }

    private async void ExportAll_Click(object? sender, RoutedEventArgs e)
    {
        if (_session == null) return;

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Export All Textures"
        });
        if (folders.Count == 0) return;

        var dir = folders[0].Path.LocalPath;
        for (int i = 0; i < _session.Count; i++)
        {
            var png = _session.ExportTexturePng(i);
            var name = _session.TextureNames[i];
            await File.WriteAllBytesAsync(Path.Combine(dir, $"{name}.png"), png);
        }
    }

    // ── Selection ──────────────────────────────────────────────────────────────

    private void TextureList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var idx = TextureList.SelectedIndex;
        if (idx < 0 || _session == null)
        {
            EnableSelectionControls(false);
            RgbPreview.Source = null;
            AlphaPreview.Source = null;
            TextureNameLabel.Text = "";
            return;
        }
        EnableSelectionControls(true);
        ShowPreview(idx);
    }

    // ── Buttons ────────────────────────────────────────────────────────────────

    private async void Import_Click(object? sender, RoutedEventArgs e)
    {
        if (_session == null) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open PNG Image Texture",
            FileTypeFilter = [new FilePickerFileType("PNG Image") { Patterns = ["*.png"] }]
        });
        if (files.Count == 0) return;

        var bytes = await File.ReadAllBytesAsync(files[0].Path.LocalPath);
        var name = Path.GetFileNameWithoutExtension(files[0].Name);
        var added = _session.ImportTexture(bytes, name);
        TextureList.Items.Add(added);
    }

    private async void ImportSplit_Click(object? sender, RoutedEventArgs e)
    {
        if (_session == null) return;

        var rgbFiles = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open RGB Texture",
            FileTypeFilter = [new FilePickerFileType("PNG Image") { Patterns = ["*.png"] }]
        });
        if (rgbFiles.Count == 0) return;

        var alphaFiles = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Alpha Texture",
            FileTypeFilter = [new FilePickerFileType("PNG Image") { Patterns = ["*.png"] }]
        });
        if (alphaFiles.Count == 0) return;

        var rgbBytes = await File.ReadAllBytesAsync(rgbFiles[0].Path.LocalPath);
        var alphaBytes = await File.ReadAllBytesAsync(alphaFiles[0].Path.LocalPath);
        var name = Path.GetFileNameWithoutExtension(rgbFiles[0].Name).Replace("_RGB", "");
        var added = _session.ImportSplitTexture(rgbBytes, alphaBytes, name);
        TextureList.Items.Add(added);
    }

    private async void Replace_Click(object? sender, RoutedEventArgs e)
    {
        if (_session == null || TextureList.SelectedIndex < 0) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open PNG Image Texture",
            FileTypeFilter = [new FilePickerFileType("PNG Image") { Patterns = ["*.png"] }]
        });
        if (files.Count == 0) return;

        var bytes = await File.ReadAllBytesAsync(files[0].Path.LocalPath);
        var idx = TextureList.SelectedIndex;
        _session.ReplaceTexture(idx, bytes);
        ShowPreview(idx);
    }

    private void Remove_Click(object? sender, RoutedEventArgs e)
    {
        if (_session == null || TextureList.SelectedIndex < 0) return;
        var idx = TextureList.SelectedIndex;
        _session.RemoveTexture(idx);
        TextureList.Items.RemoveAt(idx);
        EnableSelectionControls(false);
        RgbPreview.Source = null;
        AlphaPreview.Source = null;
        TextureNameLabel.Text = "";
    }

    private async void Rename_Click(object? sender, RoutedEventArgs e)
    {
        if (_session == null || TextureList.SelectedIndex < 0) return;
        var idx = TextureList.SelectedIndex;
        var current = _session.TextureNames[idx];

        var newName = await InputDialog.ShowAsync(this, "New Texture Name", current);
        if (string.IsNullOrEmpty(newName)) return;

        var result = _session.RenameTexture(idx, newName);
        TextureList.Items[idx] = result;
        TextureNameLabel.Text = result;
    }

    private async void Export_Click(object? sender, RoutedEventArgs e)
    {
        if (_session == null || TextureList.SelectedIndex < 0) return;
        var idx = TextureList.SelectedIndex;
        var name = _session.TextureNames[idx];

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Texture Image",
            SuggestedFileName = name,
            FileTypeChoices = [new FilePickerFileType("PNG") { Patterns = ["*.png"] }]
        });
        if (file == null) return;

        var png = _session.ExportTexturePng(idx);
        await File.WriteAllBytesAsync(file.Path.LocalPath, png);
    }

    private async void ExportSplit_Click(object? sender, RoutedEventArgs e)
    {
        if (_session == null || TextureList.SelectedIndex < 0) return;
        var idx = TextureList.SelectedIndex;
        var name = _session.TextureNames[idx];

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Texture Image",
            SuggestedFileName = name,
            FileTypeChoices = [new FilePickerFileType("PNG") { Patterns = ["*.png"] }]
        });
        if (file == null) return;

        var dir = Path.GetDirectoryName(file.Path.LocalPath) ?? ".";
        var stem = Path.GetFileNameWithoutExtension(file.Path.LocalPath);

        var (rgb, alpha) = _session.ExportTextureSplit(idx);
        await File.WriteAllBytesAsync(Path.Combine(dir, $"{stem}_RGB.png"), rgb);
        await File.WriteAllBytesAsync(Path.Combine(dir, $"{stem}_A.png"), alpha);
    }
}
