using Microsoft.Extensions.DependencyInjection;

using ReactiveUI;

using Resona.UI.ViewModels;
using Resona.UI.Views;
using Resona.UI.Views.Settings;

namespace Resona.UI.ViewModels
{
    public static class ServiceCollectionExtensions
    {
        public static void AddViewModels(this IServiceCollection services)
        {
            services.AddSingleton<AudioSelectionViewModel>();
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<PlayerControlsViewModel>();
            services.AddSingleton<LibraryViewModel>();
            services.AddSingleton<TrackListViewModel>();
            services.AddSingleton<PowerOptionsViewModel>();
            services.AddSingleton<SleepOptionsViewModel>();
            services.AddSingleton<BluetoothSettingsViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<AudioSettingsViewModel>();
            services.AddSingleton<IScreen, MainWindowViewModel>(x => x.GetRequiredService<MainWindowViewModel>());
        }

        public static void AddViews(this ServiceCollection services)
        {
            services.AddScoped<IViewFor<PlayerControlsViewModel>, PlayerControlsView>();
            services.AddScoped<IViewFor<AudioSelectionViewModel>, AudioSelectionView>();
            services.AddScoped<IViewFor<LibraryViewModel>, LibraryView>();
            services.AddScoped<IViewFor<TrackListViewModel>, TrackListView>();
            services.AddScoped<IViewFor<SettingsViewModel>, SettingsView>();
        }
    }
}
