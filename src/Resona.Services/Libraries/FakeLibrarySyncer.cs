namespace Resona.Services.Libraries
{
#if DEBUG
    public class FakeLibrarySyncer : ILibrarySyncer
    {
        public Action<AudioKind>? LibraryChanged { get; set; }

        public void StartSync()
        {
            throw new NotImplementedException();
        }
    }
#endif
}