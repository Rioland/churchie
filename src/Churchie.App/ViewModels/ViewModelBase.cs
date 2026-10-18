using CommunityToolkit.Mvvm.ComponentModel;

namespace Churchie.App.ViewModels;

/// <summary>
/// Base class for view models. <see cref="ObservableObject"/> tells the UI when a
/// property changes, so bindings like {Binding Title} update automatically.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
}
