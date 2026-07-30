using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;

namespace DestinationPlanner.Helpers;

// Shared drag/position behavior for an airport info box hosted on a Canvas overlay, used
// identically by MapView (Map tab) and TripMapWindow (Trip Plan map) — see CLAUDE.md's
// "Map info box parity" rule: the two must behave the same for airport click/drag/zoom
// interactions, and sharing this one implementation is what actually guarantees that rather
// than relying on two hand-kept-in-sync copies.
//
// The box is a Canvas-hosted Border (not a WPF Popup): a Popup is a separate top-level
// window, which is why earlier versions of both callers needed manual workarounds for
// window-minimize, window-move, and tab/window-switch (see BUG-08, US17.7/US17.8) — a
// Canvas-hosted Border is part of the same visual tree as the rest of the map, so all of
// that becomes automatic.
public sealed class DraggableInfoBox
{
    private static readonly Vector DefaultOffset = new(10, 10);

    private readonly Border _box;
    private readonly Line _leaderLine;
    private readonly Canvas _overlay;
    private readonly Func<double> _mapWidth;
    private readonly Func<double> _mapHeight;
    private readonly Func<(double X, double Y)?> _anchorScreenPoint;

    // Offset from the airport's screen point to the box's top-left corner. Null = not yet
    // dragged, so Reposition() uses DefaultOffset instead of a remembered one.
    private Vector? _manualOffset;
    private bool _dragging;
    private Point _dragMouseStart;
    private double _dragStartLeft;
    private double _dragStartTop;

    // anchorScreenPoint returns the currently-assigned airport's screen position, or null if
    // no airport is assigned to this slot right now (the box is then hidden).
    public DraggableInfoBox(Border box, Line leaderLine, Canvas overlay,
        Func<double> mapWidth, Func<double> mapHeight, Func<(double X, double Y)?> anchorScreenPoint)
    {
        _box = box;
        _leaderLine = leaderLine;
        _overlay = overlay;
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;
        _anchorScreenPoint = anchorScreenPoint;

        _box.MouseLeftButtonDown += OnMouseLeftButtonDown;
        _box.MouseMove += OnMouseMove;
        _box.MouseLeftButtonUp += OnMouseLeftButtonUp;
        _box.LostMouseCapture += (_, _) => _dragging = false;
    }

    // Call when a new airport is selected into this slot, so the box starts back at the
    // default anchor-relative spot rather than wherever a previous airport's box was dragged to.
    public void ResetOffset()
    {
        _manualOffset = null;
        _dragging = false;
    }

    public void Hide()
    {
        _box.Visibility = Visibility.Collapsed;
        _leaderLine.Visibility = Visibility.Collapsed;
    }

    // Call once on open, and again on every viewport change (pan/zoom) — recomputes the box's
    // screen position from the airport's current screen position plus whatever offset (default
    // or dragged) applies, and hides the box once its airport pans off the visible map area.
    public void Reposition()
    {
        var anchor = _anchorScreenPoint();
        if (anchor is null)
        {
            Hide();
            return;
        }

        var (ax, ay) = anchor.Value;
        if (ax < 0 || ay < 0 || ax > _mapWidth() || ay > _mapHeight())
        {
            Hide();
            return;
        }

        var offset = _manualOffset ?? DefaultOffset;
        var bx = ax + offset.X;
        var by = ay + offset.Y;

        Canvas.SetLeft(_box, bx);
        Canvas.SetTop(_box, by);
        _box.Visibility = Visibility.Visible;

        if (_manualOffset.HasValue)
        {
            _leaderLine.X1 = ax;
            _leaderLine.Y1 = ay;
            _leaderLine.X2 = bx;
            _leaderLine.Y2 = by;
            _leaderLine.Visibility = Visibility.Visible;
        }
        else
        {
            _leaderLine.Visibility = Visibility.Collapsed;
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _dragMouseStart = e.GetPosition(_overlay);
        _dragStartLeft = Canvas.GetLeft(_box);
        _dragStartTop = Canvas.GetTop(_box);
        _box.CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;

        var anchor = _anchorScreenPoint();
        if (anchor is null) return;
        var (ax, ay) = anchor.Value;

        var pos = e.GetPosition(_overlay);
        var newLeft = _dragStartLeft + (pos.X - _dragMouseStart.X);
        var newTop = _dragStartTop + (pos.Y - _dragMouseStart.Y);

        // Keep at least a corner of the box reachable within the map area, so it can never be
        // dragged somewhere the user can't get it back from.
        var maxLeft = Math.Max(0, _mapWidth() - _box.ActualWidth);
        var maxTop = Math.Max(0, _mapHeight() - _box.ActualHeight);
        newLeft = Math.Clamp(newLeft, 0, maxLeft);
        newTop = Math.Clamp(newTop, 0, maxTop);

        Canvas.SetLeft(_box, newLeft);
        Canvas.SetTop(_box, newTop);

        _manualOffset = new Vector(newLeft - ax, newTop - ay);

        _leaderLine.X1 = ax;
        _leaderLine.Y1 = ay;
        _leaderLine.X2 = newLeft;
        _leaderLine.Y2 = newTop;
        _leaderLine.Visibility = Visibility.Visible;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        _box.ReleaseMouseCapture();
        e.Handled = true;
    }
}
