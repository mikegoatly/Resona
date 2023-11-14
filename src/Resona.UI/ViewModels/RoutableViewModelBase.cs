using System;

using ReactiveUI;

namespace Resona.UI.ViewModels
{
    public abstract class RoutableViewModelBase(RoutingState router, IScreen hostScreen, string urlPathSegment) : ReactiveObject, IRoutableViewModel
    {
        public string? UrlPathSegment { get; } = urlPathSegment;
        public RoutingState Router { get; } = router;
        public IScreen HostScreen { get; } = hostScreen;

        public bool CurrentlyViewingViewModel<T>(Func<T, bool> predicate) where T : class
        {
            var result = false;

            if (this.Router.CurrentViewModel is IObservable<IReactiveObject> currentViewModelObservable)
            {
                // We have a concatenated observable, so we need to subscribe to it to get the actual view model
                currentViewModelObservable.Subscribe(vm =>
                {
                    result = this.CheckViewModelTypeAndPredicate(vm, predicate);
                });
            }
            else
            {
                // We have a regular view model, so we can directly check its type
                result = this.CheckViewModelTypeAndPredicate(this.Router.CurrentViewModel, predicate);
            }

            return result;
        }

        private bool CheckViewModelTypeAndPredicate<T>(object viewModel, Func<T, bool> predicate) where T : class
        {
            return viewModel is T typedViewModel && predicate(typedViewModel);
        }
    }
}
