# SymDirs
C# console application to manage symbolic links

## Intention
I have a video library on my PC I wanna share with my tablet and phone for on the go watching. To do this I use SyncThing.

Thus I want to have a folder on my PC that contains all the videos I want to share. Then also folders which I sync to my phone and tablet.

SymDirs allows to manage links from my video folder to my syncing folders with low afford.

I use hard links for the files themselves as those allow to be synced via SyncThing. Symbolic links of directories are not supported by it.

### Why use SymDirs?
- No need to copy files
- You can add an entire folder to be synced with a few commands
- You can remove a folder from syncing with a few commands
- You can delete files on the client device without deleting them on the pc

## Usage
Run the program. UI is self explenatory.

### Important notes:
- When applying a configuration the current state of the source folder is copied to a target folder. Directories are created and files are linked via hard links. Thus the state of the source folder won't automatically be synced with the target folder. To fix this apply the configuration or run the program with `--apply`.