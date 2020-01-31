# MCCTexturePackDumper
A command-line tool for dumping texture pack files from Halo The Master Chief Collection

*should* be future-proof but who knows.

## Use
1. Copy the executable and DLL to the \data\ui\texturepacks folder inside your MCC installation.

2. Run the executable either by double-clicking or via command line.
  * A double-click will prompt the input of a texture pack file in the folder, but without any extension, such as `emblemstexturepack`
  * Command line works much the same, just passing the texture pack as an argument, such as `mcctexturepackdumper.exe emblemstexturepack`

3. If the texture pack name is valid the images will be dumped to a subfolder inside \texturepacks, with the same name as the pack, along with a \_fileindex.txt file giving the format, size, and original offset of the images in case you wish to replace anything.
