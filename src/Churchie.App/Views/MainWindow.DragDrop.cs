using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Churchie.App.ViewModels;

namespace Churchie.App.Views;

// Drag and drop with the mouse — the window's half of it.
// This file notices when a drag starts, shows where it will land, and reads files
// dropped from Finder / Explorer. What a drop *does* is decided by the view model
// (ViewModels/MainWindowViewModel.DragDrop.cs, which also lists what can go where).
public partial class MainWindow
{
    // A drag inside Churchie carries this marker. The dragged object itself is kept in
    // _dragged: a drag can only carry text or files between programs, and these
    // objects never leave Churchie anyway.
    private static readonly DataFormat<string> ChurchieItem = DataFormat.CreateStringApplicationFormat("churchie.item");

    // How far (in pixels) the mouse must move with the button held down before it
    // counts as a drag, so ordinary clicks and double-clicks keep working.
    private const double DragDistance = 6;

    private object? _pressedItem; // the list item the mouse button went down on
    private Point _pressedAt;
    private object? _dragged;     // what is being dragged right now (null when nothing is)

    /// <summary>Called once from the constructor.</summary>
    private void SetUpDragAndDrop()
    {
        // "Tunnel" so we see the mouse before the lists do (they use it for selecting).
        AddHandler(PointerPressedEvent, OnPointerPressedForDrag, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnPointerMovedForDrag, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerReleasedForDrag, RoutingStrategies.Tunnel);

        // The places things can be dropped on.
        foreach (var target in new Control[] { ScheduleList, PreviewPanel, LivePanel, MediaList, SongList })
            DragDrop.SetAllowDrop(target, true);

        DragDrop.AddDragOverHandler(ScheduleList, OnScheduleDragOver);
        DragDrop.AddDragLeaveHandler(ScheduleList, (_, _) => ScheduleDropLine.IsVisible = false);
        DragDrop.AddDropHandler(ScheduleList, OnScheduleDrop);

        DragDrop.AddDragOverHandler(PreviewPanel, OnPreviewDragOver);
        DragDrop.AddDragLeaveHandler(PreviewPanel, (_, _) => Highlight(PreviewPanel, false));
        DragDrop.AddDropHandler(PreviewPanel, OnPreviewDrop);

        DragDrop.AddDragOverHandler(LivePanel, OnLiveDragOver);
        DragDrop.AddDragLeaveHandler(LivePanel, (_, _) => Highlight(LivePanel, false));
        DragDrop.AddDropHandler(LivePanel, OnLiveDrop);

        DragDrop.AddDragOverHandler(MediaList, OnFileListDragOver);
        DragDrop.AddDragLeaveHandler(MediaList, (_, _) => Highlight(MediaList, false));
        DragDrop.AddDropHandler(MediaList, OnMediaDrop);

        DragDrop.AddDragOverHandler(SongList, OnFileListDragOver);
        DragDrop.AddDragLeaveHandler(SongList, (_, _) => Highlight(SongList, false));
        DragDrop.AddDropHandler(SongList, OnSongsDrop);
    }

    // ---------------------------------------------------------------
    // Starting a drag
    // ---------------------------------------------------------------

    private void OnPointerPressedForDrag(object? sender, PointerPressedEventArgs e)
    {
        _pressedItem = null;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || e.Source is not Visual source) return;

        // Only items in these lists can be dragged.
        var list = source.FindAncestorOfType<ListBox>(includeSelf: true);
        if (list != ScheduleList && list != SongList && list != MediaList && list != VerseList && list != PreviewList)
            return;

        // The row (ListBoxItem) under the mouse; its DataContext is the song, picture, slide...
        if (source.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is not { } item) return;

        _pressedItem = item;
        _pressedAt = e.GetPosition(this);
    }

    private void OnPointerReleasedForDrag(object? sender, PointerReleasedEventArgs e) => _pressedItem = null;

    private async void OnPointerMovedForDrag(object? sender, PointerEventArgs e)
    {
        if (_pressedItem is null || _dragged is not null) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressedItem = null;
            return;
        }

        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _pressedAt.X) < DragDistance && Math.Abs(position.Y - _pressedAt.Y) < DragDistance)
            return; // not far enough yet: maybe it's just a click

        _dragged = _pressedItem;
        _pressedItem = null;
        try
        {
            var data = new DataTransfer();
            data.Add(DataTransferItem.Create(ChurchieItem, "item"));
            // Waits here until the mouse button is let go (the drop handlers below run meanwhile).
            await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move | DragDropEffects.Copy);
        }
        catch
        {
            // A drag that fails to start simply does nothing.
        }
        finally
        {
            _dragged = null;
            ClearDropHints();
        }
    }

    /// <summary>What is being dragged inside Churchie, or null for a drag from another program.</summary>
    private object? DraggedItem(DragEventArgs e) => e.DataTransfer.Contains(ChurchieItem) ? _dragged : null;

    /// <summary>The files dragged in from Finder / Explorer (their full paths), or an empty list.</summary>
    private static List<string> DroppedFiles(DragEventArgs e) =>
        e.DataTransfer.TryGetFiles()?.Select(file => file.TryGetLocalPath()).OfType<string>().ToList() ?? new List<string>();

    private static bool HasFiles(DragEventArgs e) => e.DataTransfer.Contains(DataFormat.File);

    // ---------------------------------------------------------------
    // The schedule: reorder, or add at the spot it's dropped
    // ---------------------------------------------------------------

    private void OnScheduleDragOver(object? sender, DragEventArgs e)
    {
        var item = DraggedItem(e);
        var allowed = item is not null ? _viewModel?.CanDropInSchedule(item) == true : HasFiles(e);

        // Moving within the schedule is a "move"; anything else adds a copy.
        e.DragEffects = !allowed ? DragDropEffects.None
            : item is ScheduleEntryViewModel ? DragDropEffects.Move
            : DragDropEffects.Copy;

        if (allowed)
        {
            ScheduleGapAt(e, out var lineY);
            ShowScheduleDropLine(lineY);
        }
        else
        {
            ScheduleDropLine.IsVisible = false;
        }
        e.Handled = true;
    }

    private async void OnScheduleDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        ScheduleDropLine.IsVisible = false;
        if (_viewModel is not { } vm) return;

        var gap = ScheduleGapAt(e, out _);
        if (DraggedItem(e) is { } item)
        {
            vm.DropInSchedule(item, gap);
            return;
        }

        var files = DroppedFiles(e);
        if (files.Count > 0) await vm.DropFilesInScheduleAsync(files, gap);
    }

    /// <summary>
    /// Which gap between schedule items the mouse is over: 0 = above the first item,
    /// Count = below the last. <paramref name="lineY"/> is where to draw the line for it.
    /// </summary>
    private int ScheduleGapAt(DragEventArgs e, out double lineY)
    {
        var mouseY = e.GetPosition(ScheduleDropLayer).Y;
        var count = _viewModel?.ScheduleEntries.Count ?? 0;
        lineY = 0;

        for (var i = 0; i < count; i++)
        {
            // Rows scrolled far out of view don't exist on screen; skip them.
            if (ScheduleList.ContainerFromIndex(i) is not Control row) continue;
            var top = row.TranslatePoint(new Point(0, 0), ScheduleDropLayer)?.Y ?? 0;
            var height = row.Bounds.Height;

            // Above the middle of this row: the gap is just before it.
            if (mouseY < top + height / 2)
            {
                lineY = top;
                return i;
            }
            lineY = top + height; // otherwise, after it (unless a later row matches)
        }
        return count;
    }

    private void ShowScheduleDropLine(double y)
    {
        ScheduleDropLine.Width = Math.Max(0, ScheduleDropLayer.Bounds.Width - 8);
        Canvas.SetLeft(ScheduleDropLine, 4);
        Canvas.SetTop(ScheduleDropLine, Math.Max(0, y - 1.5));
        ScheduleDropLine.IsVisible = true;
    }

    // ---------------------------------------------------------------
    // Preview and Live panels
    // ---------------------------------------------------------------

    private void OnPreviewDragOver(object? sender, DragEventArgs e)
    {
        var allowed = DraggedItem(e) is { } item && _viewModel?.CanDropOnPreview(item) == true;
        e.DragEffects = allowed ? DragDropEffects.Copy : DragDropEffects.None;
        Highlight(PreviewPanel, allowed);
        e.Handled = true;
    }

    private void OnPreviewDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        Highlight(PreviewPanel, false);
        if (DraggedItem(e) is { } item) _viewModel?.DropOnPreview(item);
    }

    private void OnLiveDragOver(object? sender, DragEventArgs e)
    {
        var allowed = DraggedItem(e) is { } item && _viewModel?.CanDropOnLive(item) == true;
        e.DragEffects = allowed ? DragDropEffects.Copy : DragDropEffects.None;
        Highlight(LivePanel, allowed);
        e.Handled = true;
    }

    private void OnLiveDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        Highlight(LivePanel, false);
        if (DraggedItem(e) is { } item) _viewModel?.DropOnLive(item);
    }

    // ---------------------------------------------------------------
    // Files from the computer onto the Media tab and the Songs list
    // ---------------------------------------------------------------

    private void OnFileListDragOver(object? sender, DragEventArgs e)
    {
        // Only files from outside Churchie; dragging a song onto the song list means nothing.
        var allowed = DraggedItem(e) is null && HasFiles(e);
        e.DragEffects = allowed ? DragDropEffects.Copy : DragDropEffects.None;
        if (sender is Control list) Highlight(list, allowed);
        e.Handled = true;
    }

    private void OnMediaDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        Highlight(MediaList, false);
        var files = DroppedFiles(e);
        if (files.Count > 0) _viewModel?.DropFilesInMedia(files);
    }

    private async void OnSongsDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        Highlight(SongList, false);
        var files = DroppedFiles(e);
        if (files.Count > 0 && _viewModel is { } vm) await vm.DropFilesInSongsAsync(files);
    }

    // ---------------------------------------------------------------

    /// <summary>Tints a panel or list while something droppable is over it (style in App.axaml).</summary>
    private static void Highlight(Control target, bool on) => target.Classes.Set("droptarget", on);

    private void ClearDropHints()
    {
        ScheduleDropLine.IsVisible = false;
        foreach (var target in new Control[] { PreviewPanel, LivePanel, MediaList, SongList })
            Highlight(target, false);
    }
}
