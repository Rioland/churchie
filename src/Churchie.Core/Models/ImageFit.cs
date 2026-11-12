namespace Churchie.Core.Models;

/// <summary>
/// How a picture is sized on the projector screen when its shape doesn't match
/// the screen (for example a tall phone photo on a wide screen).
/// The order matters: the app's drop-down lists use these numbers as positions.
/// </summary>
public enum ImageFit
{
    /// <summary>Cover the whole screen. The picture keeps its shape, so its edges may be cut off.</summary>
    Fill = 0,

    /// <summary>Show the whole picture. It keeps its shape, so there may be empty bars beside it.</summary>
    Fit = 1,

    /// <summary>Stretch the picture to the exact screen shape. Nothing is cut off, but it may look squashed.</summary>
    Stretch = 2,
}
