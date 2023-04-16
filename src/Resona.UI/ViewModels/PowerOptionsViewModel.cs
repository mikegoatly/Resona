using System.Reactive;

using ReactiveUI;

using Resona.Services.OS;

namespace Resona.UI.ViewModels
{
    public class PowerOptionsViewModel : ReactiveObject
    {
        private readonly IOsCommandExecutor osCommandExecutor;

#if DEBUG
        public PowerOptionsViewModel()
            : this(new FakeOsCommandExecutor())
        {
        }
#endif
        public PowerOptionsViewModel(IOsCommandExecutor osCommandExecutor)
        {
            this.osCommandExecutor = osCommandExecutor;
            this.ShutDownCommand = ReactiveCommand.Create(this.osCommandExecutor.Shutdown);
            this.RestartCommand = ReactiveCommand.Create(this.osCommandExecutor.Restart);
        }

        public ReactiveCommand<Unit, Unit> ShutDownCommand { get; }
        public ReactiveCommand<Unit, Unit> RestartCommand { get; }
    }
}
