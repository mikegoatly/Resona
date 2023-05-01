using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;

using ReactiveUI;
using ReactiveUI.Fody.Helpers;

using Resona.Services.Audio;

namespace Resona.UI.ViewModels
{
    public class AudioSettingsViewModel : ReactiveObject
    {
#if DEBUG
        [Obsolete("Do not use outside of design time")]
        public AudioSettingsViewModel()
            : this(new DevAudioOutputService())
        {
        }
#endif

        public AudioSettingsViewModel(IAudioOutputService audioOutputService)
        {
            audioOutputService.AudioOutputs
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x => this.AudioDevices = x);

            this.ConnectDeviceCommand = ReactiveCommand.CreateFromTask<AudioDevice>(
                x => audioOutputService.SetActiveDeviceAsync(x, default));
        }

        [Reactive]
        public IReadOnlyList<AudioDevice>? AudioDevices { get; set; }
        public ReactiveCommand<AudioDevice, Unit> ConnectDeviceCommand { get; }
    }
}
