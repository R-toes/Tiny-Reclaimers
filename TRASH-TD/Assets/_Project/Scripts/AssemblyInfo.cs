using System.Runtime.CompilerServices;

// UiKit and a few other helpers are internal to the game assembly; the EditMode tests exercise them directly.
[assembly: InternalsVisibleTo("TrashTD.Tests.EditMode")]
