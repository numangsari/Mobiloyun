using System.Runtime.CompilerServices;

// PlayMode tests drive the game through narrow internal hooks on GameController.
[assembly: InternalsVisibleTo("JellyMold.PlayTests")]
[assembly: InternalsVisibleTo("JellyMold.Editor")]
